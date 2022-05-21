using AElf.CSharp.Core;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Gandalf.Contracts.DividendPoolContract
{
    public partial class DividendPoolContract
    {

        /// <summary>
        ///  Get pool length.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Int32Value PoolLength(Empty input)
        {
            return new Int32Value
            {
                Value = State.PoolInfoList.Value.Value.Count
            };
        }

        /// <summary>
        /// Get pending reward.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override PendingOutput Pending(PendingInput input)
        {
            var pool = State.PoolInfoList.Value.Value[input.Pid];
            var user = State.UserInfo[input.Pid][input.User];
            var tokenList = State.TokenList.Value.Value;

            var pendingOutput = new PendingOutput();

            var number = Context.CurrentHeight > State.EndBlock.Value
                ? State.EndBlock.Value
                : Context.CurrentHeight;
            if (number >= pool.LastRewardBlock && !pool.TotalAmount.Equals(0))
            {
                var multiplier = number.Sub(pool.LastRewardBlock);
                for (int i = 0; i < tokenList.Count; i++)
                {
                    var tokenSymbol = tokenList[i];
                    var amount = GetUserReward(input.Pid, pool, user, tokenSymbol, multiplier, input.User);
                    pendingOutput.Tokens.Add(tokenSymbol);
                    pendingOutput.Amounts.Add(amount);
                }
            }
            else
            {
                for (int i = 0; i < tokenList.Count; i++)
                {
                    pendingOutput.Tokens.Add(tokenList[i]);
                    pendingOutput.Amounts.Add(new BigIntValue(0));
                }
            }

            return pendingOutput;
        }

        /// <summary>
        /// Get token list length.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Int32Value GetTokenListLength(Empty input)
        {
            return new Int32Value
            {
                Value = State.TokenList.Value.Value.Count
            };
        }

        /// <summary>
        /// Judge whether the token is in the reward token list.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override BoolValue IsTokenList(Token input)
        {
            return new BoolValue
            {
                Value = State.TokenList.Value.Value.Contains(input.Value)
            };
        }

        /// <summary>
        /// Get the owner of the contract.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Address Owner(Empty input)
        {
            return State.Owner.Value;
        }


        /// <summary>
        ///  Get token by index from token list.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override StringValue TokenList(Int32Value input)
        {
            return new StringValue
            {
                Value = State.TokenList.Value.Value[input.Value]
            };
        }

        /// <summary>
        ///  Get perBlock from state
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override BigIntValue PerBlock(StringValue input)
        {
            return State.PerBlock[input.Value];
        }

        /// <summary>
        /// Get poolInfo by pid  address form state.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Pool PoolInfo(Int32Value input)
        {
            return State.PoolInfoList.Value.Value[input.Value];
        }

        /// <summary>
        /// Get user Info
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override User UserInfo(UserInfoInput input)
        {
            return State.UserInfo[input.Pid][input.User];
        }

        /// <summary>
        ///  Get totalAllocPoint of the contract.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Int64Value TotalAllocPoint(Empty input)
        {
            return new Int64Value
            {
                Value = State.TotalAllocPoint.Value
            };
        }

        /// <summary>
        /// Get startBlock.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Int64Value StartBlock(Empty input)
        {
            return new Int64Value
            {
                Value = State.StartBlock.Value
            };
        }

        /// <summary>
        ///  Get end block.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Int64Value EndBlock(Empty input)
        {
            return new Int64Value
            {
                Value = State.EndBlock.Value
            };
        }

        /// <summary>
        /// Get Cycle.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Int64Value Cycle(Empty input)
        {
            return new Int64Value
            {
                Value = State.Cycle.Value
            };
        }

        /// <summary>
        /// Get reward debt.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override BigIntValue RewardDebt(RewardDebtInput input)
        {
            return State.RewardDebt[input.Pid][input.User][input.Token];
        }

        /// <summary>
        /// Get AccPerShare
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override BigIntValue AccPerShare(AccPerShareInput input)
        {
            return State.AccPerShare[input.Pid][input.Token];
        }

        private BigIntValue GetUserReward(int pid, Pool pool,
            User user,
            string token,
            long multiplier, Address userAddress)
        {
            var reward = State.PerBlock[token]
                .Mul(multiplier)
                .Mul(pool.AllocPoint)
                .Div(State.TotalAllocPoint.Value);
            var tokenMultiplier = GetMultiplier(token);

            var accPerShare = GetAccPerShare(pid, token).Add(
                tokenMultiplier.Mul(reward).Div(pool.TotalAmount)
            );

            var amount = user.Value.Mul(accPerShare).Div(tokenMultiplier)
                .Sub(State.RewardDebt[pid][userAddress][token]);
            return amount;
        }
    }
}