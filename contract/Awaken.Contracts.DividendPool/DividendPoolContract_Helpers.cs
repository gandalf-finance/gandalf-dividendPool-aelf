using AElf.Types;

namespace Awaken.Contracts.DividendPoolContract
{
    public partial class DividendPoolContract
    {
        private BigIntValue GetAccPerShare(long pid, string token, bool isInitialIfNull = true)
        {
            var accPerShare = State.AccPerShare[pid][token];
            if (accPerShare != null || !isInitialIfNull) return accPerShare;
            accPerShare = new BigIntValue(0);
            State.AccPerShare[pid][token] = accPerShare;
            return accPerShare;
        }

        private BigIntValue GetRewardDebt(long pid, Address address, string token, bool isInitialIfNull = true)
        {
            var rewardDebt = State.RewardDebt[pid][address][token];
            if (rewardDebt != null || !isInitialIfNull) return rewardDebt;
            rewardDebt = new BigIntValue(0);
            State.RewardDebt[pid][address][token] = rewardDebt;
            return rewardDebt;
        }
    }
}