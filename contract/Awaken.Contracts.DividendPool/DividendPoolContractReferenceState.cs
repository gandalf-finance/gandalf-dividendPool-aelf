using AElf.Contracts.MultiToken;

namespace Awaken.Contracts.DividendPoolContract
{
    public partial class DividendPoolContractState
    {
        internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
    }
}