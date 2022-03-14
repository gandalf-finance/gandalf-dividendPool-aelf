using System;
using System.Collections.Generic;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Gandalf.Contracts.DividendPoolContract
{
    public partial class DividendPoolContract
    {
        /**
         *  AddToken
         */
        public override Empty AddToken(Token input)
        {
            AssertSenderIsOwner();
            Assert(input.TokenSymbol != null, "Invalid token symbol.");
            var tokenList = State.TokenList.Value;
            Assert(!tokenList.Tokens.Contains(input.TokenSymbol), "Token has Added.");
            tokenList.Tokens.Add(input.TokenSymbol);
            State.TokenList.Value = tokenList;
            State.PerBlock[input.TokenSymbol] = new BigIntValue(0);
            Context.Fire(new AddToken
            {
                TokenSymbol = input.TokenSymbol,
                Index = State.TokenList.Value.Tokens.Count.Mul(1)
            });
            return new Empty();
        }

        /**
         *  NewReward
         */
        public override Empty NewReward(NewRewardInput input)
        {
            AssertSenderIsOwner();
            var endBlock = State.EndBlock.Value;
            Assert(Context.CurrentHeight > endBlock && input.StartBlock > endBlock, "Not finished");
            MassUpdatePools(new Empty());
            var tokenLength = input.Tokens.Count;
            for (int i = 0; i < tokenLength; i++)
            {
                var token = input.Tokens[i];
                Assert(State.TokenList.Value.Tokens.Contains(token), "Token not exist.");
                var amount = input.Amounts[i];
                var perBlock = input.PerBlocks[i];
                if (amount.Equals("0"))
                {
                    State.PerBlock[token] = new BigIntValue
                    {
                        Value = "0"
                    };
                    continue;
                }

                State.TokenContract.TransferFrom.Send(new TransferFromInput
                {
                    Amount = long.Parse(amount.Value),
                    From = Context.Sender,
                    Symbol = token,
                    To = Context.Self
                });

                Assert(amount > 0 && (new BigIntValue
                {
                    Value = State.Cycle.Value.ToString()
                }.Mul(perBlock) <= amount), "Balance not enough");

                State.PerBlock[token] = perBlock;
                Context.Fire(new NewReward
                {
                    Token = token,
                    PerBlocks = perBlock,
                    Amount = amount,
                    StartBlock = input.StartBlock,
                    EndBlock = input.StartBlock.Add(State.Cycle.Value)
                });
            }

            State.StartBlock.Value = input.StartBlock;
            State.EndBlock.Value = input.StartBlock.Add(State.Cycle.Value);
            UpdatePoolLastRewardBlock(input.StartBlock);
            return new Empty();
        }

        /**
         * SetCycle
         */
        public override Empty SetCycle(Int32Value input)
        {
            AssertSenderIsOwner();
            State.Cycle.Value = input.Value;
            Context.Fire(new SetCycle
            {
                Cycle = input.Value
            });
            return new Empty();
        }

        /**
         * Add
         * @Description: Add a new lp to the pool. Can only be called by the owner.
         */
        public override Empty Add(AddPoolInput input)
        {
            AssertSenderIsOwner();
            Assert(input.TokenSymbol != null, "Token can not be null.");
            if (input.WithUpdate)
            {
                MassUpdatePools(new Empty());
            }

            var lastRewadrBlock = Context.CurrentHeight > State.StartBlock.Value
                ? Context.CurrentHeight
                : State.StartBlock.Value;
            State.TotalAllocPoint.Value = State.TotalAllocPoint.Value.Add(input.AllocationPoint);
            var count = State.PoolInfo.Value.PoolList.Count;
            State.PoolInfo.Value.PoolList.Add(new PoolInfoStruct
            {
                LpToken = input.TokenSymbol,
                AllocPoint = input.AllocationPoint,
                TotalAmount = 0,
                LastRewardBlock = lastRewadrBlock
            });

            Context.Fire(new AddPool
            {
                Token = input.TokenSymbol,
                AllocPoint = input.AllocationPoint,
                LastRewardBlock = lastRewadrBlock,
                Pid = count
            });
            return new Empty();
        }

        /**
         * Set
         * @Description: Update the given pool's  allocation point.
         * Can only be called by the owner.
         */
        public override Empty Set(SetPoolInput input)
        {
            AssertSenderIsOwner();
            if (input.WithUpdate)
            {
                MassUpdatePools(new Empty());
            }

            State.TotalAllocPoint.Value = State.TotalAllocPoint.Value
                .Sub(State.PoolInfo.Value.PoolList[input.Pid].AllocPoint).Add(input.AllocationPoint);
            State.PoolInfo.Value.PoolList[input.Pid].AllocPoint = input.AllocationPoint;
            return new Empty();
        }

        /**
         * UpdatePool
         * @Description: Update reward variables of the given pool to be up-to-date.
         */
        public override Empty UpdatePool(Int32Value input)
        {
            Assert(input != null, "Invalid paramter.");
            return UpdatePool(input.Value);
        }


        /**
         * Deposit
         * @Description: deposit lp token.
         */
        public override Empty Deposit(TokenOptionInput input)
        {
            Assert(input != null, "Invalid paramter.");
            var pool = State.PoolInfo.Value.PoolList[input.Pid];
            var user = State.UserInfo[input.Pid][Context.Sender] ?? new UserInfoStruct
            {
                Amount = "0"
            };
            UpdatePool(input.Pid);
            if (user.Amount >= 0)
            {
                var tokenList = State.TokenList.Value.Tokens;
                for (int i = 0; i < tokenList.Count; i++)
                {
                    var token = tokenList[i];
                    var tokenMultiplier = GetMultiplier(token);

                    State.AccPerShare[input.Pid][token] = State.AccPerShare[input.Pid][token] ?? new BigIntValue(0);
                    State.RewardDebt[input.Pid][Context.Sender][token] =
                        State.RewardDebt[input.Pid][Context.Sender][token] ?? new BigIntValue(0);

                    var pendingAmount = user.Amount
                        .Mul(State.AccPerShare[input.Pid][token])
                        .Div(tokenMultiplier)
                        .Sub(State.RewardDebt[input.Pid][Context.Sender][token]);

                    if (pendingAmount > 0)
                    {
                        SafeTransfer(Context.Sender, pendingAmount, token,
                            pool.LpToken.Equals(token) ? pool.TotalAmount : new BigIntValue(0));

                        Context.Fire(new Harvest
                        {
                            Amount = pendingAmount,
                            To = Context.Sender,
                            Token = token,
                            Pid = input.Pid
                        });
                    }

                    State.RewardDebt[input.Pid][Context.Sender][token] = user.Amount
                        .Add(input.Amount)
                        .Mul(State.AccPerShare[input.Pid][token])
                        .Div(tokenMultiplier);
                }
            }

            if (input.Amount > 0)
            {
                State.TokenContract.TransferFrom.Send(new TransferFromInput
                {
                    Symbol = pool.LpToken,
                    From = Context.Sender,
                    To = Context.Self,
                    Amount = Convert.ToInt64(input.Amount.Value)
                });

                user.Amount = user.Amount.Add(input.Amount);
                pool.TotalAmount = pool.TotalAmount.Add(input.Amount);
            }

            State.PoolInfo.Value.PoolList[input.Pid] = pool;
            State.UserInfo[input.Pid][Context.Sender] = user;
            Context.Fire(new Deposit
            {
                Pid = input.Pid,
                Amount = input.Amount,
                User = Context.Sender
            });
            return new Empty();
        }

        /**
         *  Withdraw
         * @Decription: withdraw lp token.
         */
        public override Empty Withdraw(TokenOptionInput input)
        {
            Assert(input != null, "Invalid paramter.");
            var pool = State.PoolInfo.Value.PoolList[input.Pid];
            var user = State.UserInfo[input.Pid][Context.Sender];
            Assert(user.Amount >= input.Amount, "Withdraw: insufficient balance");
            UpdatePool(input.Pid);
            if (user.Amount > 0)
            {
                var tokenList = State.TokenList.Value.Tokens;
                for (int i = 0; i < tokenList.Count; i++)
                {
                    var token = tokenList[i];
                    var tokenMultiplier = GetMultiplier(token);
                    var pendingAmount = user.Amount
                        .Mul(State.AccPerShare[input.Pid][token])
                        .Div(tokenMultiplier)
                        .Sub(State.RewardDebt[input.Pid][Context.Sender][token]);

                    if (pendingAmount > 0)
                    {
                        SafeTransfer(Context.Sender, pendingAmount, token,
                            pool.LpToken.Equals(token) ? pool.TotalAmount : new BigIntValue(0));

                        Context.Fire(new Harvest
                        {
                            Amount = pendingAmount,
                            To = Context.Sender,
                            Token = token,
                            Pid = input.Pid
                        });
                    }

                    State.RewardDebt[input.Pid][Context.Sender][token] = user.Amount
                        .Sub(input.Amount)
                        .Mul(State.AccPerShare[input.Pid][token])
                        .Div(tokenMultiplier);
                }
            }

            if (input.Amount > 0)
            {
                user.Amount = user.Amount.Sub(input.Amount);
                pool.TotalAmount = pool.TotalAmount.Sub(input.Amount);
                State.TokenContract.Transfer.Send(new TransferInput
                {
                    Symbol = pool.LpToken,
                    Amount = Convert.ToInt64(input.Amount.Value),
                    To = Context.Sender
                });
            }

            Context.Fire(new Withdraw
            {
                Amount = input.Amount,
                Pid = input.Pid,
                User = Context.Sender
            });
            return new Empty();
        }

        /**
         * MassUpdatePools
         */
        public override Empty MassUpdatePools(Empty input)
        {
            var length = State.PoolInfo.Value.PoolList.Count;
            for (int pid = 0; pid < length; pid++)
            {
                UpdatePool(pid);
            }

            return new Empty();
        }


        private void SafeTransfer(
            Address to,
            BigIntValue amount,
            string token,
            BigIntValue poolAmount)
        {
            var tokenBalance = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Owner = Context.Self,
                Symbol = token
            }).Balance;
            tokenBalance = tokenBalance.Sub(Convert.ToInt64(poolAmount.Value));

            var realAmount = amount > tokenBalance ? tokenBalance : amount;

            State.TokenContract.Transfer.Send(new TransferInput
            {
                To = to,
                Amount = Convert.ToInt64(realAmount.Value),
                Symbol = token
            });
        }


        private Empty UpdatePool(int pid)
        {
            var pool = State.PoolInfo.Value.PoolList[pid];
            var number = Context.CurrentHeight > State.EndBlock.Value
                ? State.EndBlock.Value
                : Context.CurrentHeight;
            if (number <= pool.LastRewardBlock)
            {
                return new Empty();
            }

            var totalAmount = pool.TotalAmount;
            if (totalAmount.Equals(0))
            {
                pool.LastRewardBlock = number;
                State.PoolInfo.Value.PoolList[pid] = pool;
                return new Empty();
            }

            var multiplier = number.Sub(pool.LastRewardBlock);
            var tokenLength = State.TokenList.Value.Tokens.Count;

            for (int i = 0; i < tokenLength; i++)
            {
                var token = State.TokenList.Value.Tokens[i];
                var tokenPerBlock = State.PerBlock[token];

                var reward = tokenPerBlock
                    .Mul(multiplier)
                    .Mul(pool.AllocPoint)
                    .Div(State.TotalAllocPoint.Value);

                if (reward.Equals(0))
                {
                    continue;
                }

                var tokenMultiplier = GetMultiplier(token);
                State.AccPerShare[pid][token] = State.AccPerShare[pid][token]
                    .Add(
                        reward.Mul(tokenMultiplier).Div(totalAmount)
                    );

                Context.Fire(new UpdatePool
                {
                    Pid = pid,
                    Reward = reward,
                    Token = token,
                    AccPerShare = State.AccPerShare[pid][token],
                    BlockHeigh = number
                });
            }

            pool.LastRewardBlock = number;
            State.PoolInfo.Value.PoolList[pid] = pool;
            return new Empty();
        }

        private BigIntValue GetMultiplier(string token)
        {
            var tokenInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
            {
                Symbol = token
            });
            var decimals = tokenInfo.Decimals;
            int multiples = 30;
            return new BigIntValue
            {
                Value = "10"
            }.Pow(multiples.Sub(decimals));
        }

        private void UpdatePoolLastRewardBlock(long lastRewardBlock)
        {
            var length = State.PoolInfo.Value.PoolList.Count;
            for (int i = 0; i < length; i++)
            {
                State.PoolInfo.Value.PoolList[i].LastRewardBlock = lastRewardBlock;
            }
        }

        private void AssertSenderIsOwner()
        {
            Assert(State.Owner.Value != null, "Contract not initialized.");
            Assert(Context.Sender == State.Owner.Value, "Not Owner.");
        }
    }
}