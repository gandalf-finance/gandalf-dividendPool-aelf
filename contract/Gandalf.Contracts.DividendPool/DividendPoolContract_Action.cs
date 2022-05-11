using System;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Gandalf.Contracts.DividendPoolContract
{
    public partial class DividendPoolContract
    {
        /// <summary>
        /// Add tokens to dividend.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Empty AddToken(Token input)
        {
            AssertSenderIsOwner();
            Assert(input.Value != null, "Invalid token symbol.");
            var tokenList = State.TokenList.Value;
            Assert(!tokenList.Value.Contains(input.Value), "Token has Added.");
            State.TokenList.Value.Value.Add(input.Value);
            State.PerBlock[input.Value] = new BigIntValue(0);
            Context.Fire(new AddToken
            {
                TokenSymbol = input.Value,
                Index = State.TokenList.Value.Value.Count.Mul(1)
            });
            return new Empty();
        }

        
        /// <summary>
        /// Add new reward.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Empty NewReward(NewRewardInput input)
        {
            AssertSenderIsOwner();
            var endBlock = State.EndBlock.Value;
            Assert(input.StartBlock > Context.CurrentHeight, $"Invalid start block {input.StartBlock}.");
            Assert(Context.CurrentHeight > endBlock && input.StartBlock > endBlock, "Not finished.");

            MassUpdatePools();
            ResetPerBlock();
            var tokenLength = input.Tokens.Count;
            for (int i = 0; i < tokenLength; i++)
            {
                var token = input.Tokens[i];
                Assert(State.TokenList.Value.Value.Contains(token), "Token not exist.");
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

        private void ResetPerBlock()
        {
            foreach (var token in State.TokenList.Value.Value)
            {
                State.PerBlock[token] = new BigIntValue
                {
                    Value = "0"
                };
            }
        }

        /// <summary>
        /// Set reward cycle by owner.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Empty SetCycle(Int32Value input)
        {
            AssertSenderIsOwner();
            SetCycle(input.Value);
            return new Empty();
        }
        
        private void SetCycle(int cycle)
        {
            State.Cycle.Value = cycle;
            Context.Fire(new SetCycle
            {
                Cycle = cycle
            });
        }
        
        /// <summary>
        ///  Add pool.
        ///  Add a new lp to the pool. Can only be called by the owner.
        /// </summary>
        /// <param name="input"></param>
        /// <returns>Empty</returns>
        public override Empty Add(AddPoolInput input)
        {
            AssertSenderIsOwner();
            Assert(input.TokenSymbol != null, "Token can not be null.");
            if (input.WithUpdate)
            {
                MassUpdatePools();
            }

            var lastRewardBlock = Context.CurrentHeight > State.StartBlock.Value
                ? Context.CurrentHeight
                : State.StartBlock.Value;
            State.TotalAllocPoint.Value = State.TotalAllocPoint.Value.Add(input.AllocationPoint);
            var count = State.PoolInfoList.Value.Value.Count;
            State.PoolInfoList.Value.Value.Add(new Pool
            {
                LpToken = input.TokenSymbol,
                AllocPoint = input.AllocationPoint,
                TotalAmount = 0,
                LastRewardBlock = lastRewardBlock
            });

            Context.Fire(new AddPool
            {
                Token = input.TokenSymbol,
                AllocPoint = input.AllocationPoint,
                LastRewardBlock = lastRewardBlock,
                Pid = count
            });
            return new Empty();
        }

        /// <summary>
        ///  Update the given pool's  allocation point.
        /// </summary>
        /// <remarks>Can only be called by the owner.</remarks>
        /// <param name="input"></param>
        /// <returns>Empty</returns>
        public override Empty Set(SetPoolInput input)
        {
            AssertSenderIsOwner();
            if (input.WithUpdate)
            {
                MassUpdatePools();
            }

            State.TotalAllocPoint.Value = State.TotalAllocPoint.Value
                .Sub(State.PoolInfoList.Value.Value[input.Pid].AllocPoint).Add(input.AllocationPoint);
            State.PoolInfoList.Value.Value[input.Pid].AllocPoint = input.AllocationPoint;
            Context.Fire(new SetPool
            {
                Pid = input.Pid,
                AllocationPoint = input.AllocationPoint
            });
            return new Empty();
        }

        /// <summary>
        /// Update reward variables of the given pool to be up-to-date.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Empty UpdatePool(Int32Value input)
        {
            UpdatePool(input.Value);
            return new Empty();
        }

        /// <summary>
        /// deposit lp token.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Empty Deposit(TokenOptionInput input)
        {
            Assert(input != null, "Invalid parameter.");
            var pool = State.PoolInfoList.Value.Value[input.Pid];
            var user = State.UserInfo[input.Pid][Context.Sender] ?? new User
            {
                Value = "0"
            };
            UpdatePool(input.Pid);
            if (user.Value >= 0)
            {
                var tokenList = State.TokenList.Value.Value;
                for (int i = 0; i < tokenList.Count; i++)
                {
                    var token = tokenList[i];
                    var tokenMultiplier = GetMultiplier(token);
                    
                    State.AccPerShare[input.Pid][token] = State.AccPerShare[input.Pid][token] ?? new BigIntValue(0);
                    State.RewardDebt[input.Pid][Context.Sender][token] =
                        State.RewardDebt[input.Pid][Context.Sender][token] ?? new BigIntValue(0);

                    var pendingAmount = user.Value
                        .Mul(State.AccPerShare[input.Pid][token])
                        .Div(tokenMultiplier)
                        .Sub(State.RewardDebt[input.Pid][Context.Sender][token]);

                    if (pendingAmount > 0)
                    {
                        var realAmount = SafeTransfer(Context.Sender, pendingAmount, token,
                            pool.LpToken.Equals(token) ? pool.TotalAmount : new BigIntValue(0));

                        if (realAmount>0)
                        {
                            Context.Fire(new Harvest
                            {
                                Amount = realAmount,
                                To = Context.Sender,
                                Token = token,
                                Pid = input.Pid
                            });
                        }
                    }

                    State.RewardDebt[input.Pid][Context.Sender][token] = user.Value
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

                user.Value = user.Value.Add(input.Amount);
                pool.TotalAmount = pool.TotalAmount.Add(input.Amount);
            }

            State.PoolInfoList.Value.Value[input.Pid] = pool;
            State.UserInfo[input.Pid][Context.Sender] = user;
            Context.Fire(new Deposit
            {
                Pid = input.Pid,
                Amount = input.Amount,
                User = Context.Sender
            });
            return new Empty();
        }

        /// <summary>
        /// Withdraw lp token.
        /// </summary>
        /// <param name="input"></param>
        /// <returns>Empty</returns>
        public override Empty Withdraw(TokenOptionInput input)
        {
            Assert(input.Amount != null, "Invalid parameter.");
            var pool = State.PoolInfoList.Value.Value[input.Pid];
            var user = State.UserInfo[input.Pid][Context.Sender];
            Assert(user.Value >= input.Amount, "Withdraw: insufficient balance");
            UpdatePool(input.Pid);
            if (user.Value > 0)
            {
                var tokenList = State.TokenList.Value.Value;
                foreach (var token in tokenList)
                {
                    var tokenMultiplier = GetMultiplier(token);
                    var pendingAmount = user.Value
                        .Mul(State.AccPerShare[input.Pid][token])
                        .Div(tokenMultiplier)
                        .Sub(State.RewardDebt[input.Pid][Context.Sender][token]);

                    if (pendingAmount > 0)
                    {
                        var realAmount = SafeTransfer(Context.Sender, pendingAmount, token,
                            pool.LpToken.Equals(token) ? pool.TotalAmount : new BigIntValue(0));

                        if (realAmount>0)
                        {
                            Context.Fire(new Harvest
                            {
                                Amount = realAmount,
                                To = Context.Sender,
                                Token = token,
                                Pid = input.Pid
                            });
                        }
                    }

                    State.RewardDebt[input.Pid][Context.Sender][token] = user.Value
                        .Sub(input.Amount)
                        .Mul(State.AccPerShare[input.Pid][token])
                        .Div(tokenMultiplier);
                }
            }

            if (input.Amount > 0)
            {
                user.Value = user.Value.Sub(input.Amount);
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

        /// <summary>
        /// Mass Update Pools
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Empty MassUpdatePools(Empty input)
        {
            MassUpdatePools();
            return new Empty();
        }
        
        private void MassUpdatePools()
        {
            var length = State.PoolInfoList.Value.Value.Count;
            for (int pid = 0; pid < length; pid++)
            {
                UpdatePool(pid);
            }
        }

        private BigIntValue SafeTransfer(
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

            if (realAmount > 0)
            {
                State.TokenContract.Transfer.Send(new TransferInput
                {
                    To = to,
                    Amount = Convert.ToInt64(realAmount.Value),
                    Symbol = token
                });
            }

            return realAmount;
        }

        /// <summary>
        /// Update pool accpershare by pool id.
        /// </summary>
        /// <param name="pid"></param>
        private void UpdatePool(int pid)
        {
            var pool = State.PoolInfoList.Value.Value[pid];
            var number = Context.CurrentHeight > State.EndBlock.Value
                ? State.EndBlock.Value
                : Context.CurrentHeight;
            if (number <= pool.LastRewardBlock)
            {
                return ;
            }

            var totalAmount = pool.TotalAmount;
            if (totalAmount.Equals(0))
            {
                pool.LastRewardBlock = number;
                State.PoolInfoList.Value.Value[pid] = pool;
                return ;
            }
            
            var multiplier = number.Sub(pool.LastRewardBlock);
            foreach (var token in State.TokenList.Value.Value)
            {
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
                State.AccPerShare[pid][token] = State.AccPerShare[pid][token] ?? new BigIntValue(0);
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
                    BlockHeight = number
                });
            }

            pool.LastRewardBlock = number;
            State.PoolInfoList.Value.Value[pid] = pool;
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
            var length = State.PoolInfoList.Value.Value.Count;
            for (int i = 0; i < length; i++)
            {
                State.PoolInfoList.Value.Value[i].LastRewardBlock = lastRewardBlock;
            }
        }

        private void AssertSenderIsOwner()
        {
            Assert(State.Owner.Value != null, "Contract not initialized.");
            Assert(Context.Sender == State.Owner.Value, "Not Owner.");
        }
    }
}