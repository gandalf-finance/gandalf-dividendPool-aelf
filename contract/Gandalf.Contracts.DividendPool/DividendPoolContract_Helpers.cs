using AElf.Types;

namespace Gandalf.Contracts.DividendPoolContract
{
    public partial class DividendPoolContract
    {
        private BigIntValue GetAccPerShare(long pid, string token, bool isInitialIfNull = true)
        {
            var accPerShare = State.AccPerShare[pid][token];
            if (accPerShare == null && isInitialIfNull)
            {
                State.AccPerShare[pid][token] = new BigIntValue(0);
            }

            return accPerShare;
        }

        private BigIntValue GetRewardDebt(long pid, Address address, string token, bool isInitialIfNull = true)
        {
            var rewardDebt = State.RewardDebt[pid][address][token];
            if (rewardDebt == null && isInitialIfNull)
            {
                State.RewardDebt[pid][address][token] = new BigIntValue(0);
            }

            return rewardDebt;
        }
    }
}