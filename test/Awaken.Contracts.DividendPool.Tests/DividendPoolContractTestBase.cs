using AElf.Boilerplate.TestBase;
using AElf.Contracts.MultiToken;
using AElf.Cryptography.ECDSA;
using Awaken.Contracts.DividendPoolContract;

namespace Awaken.Contracts.DividendPool
{
    public class DividendPoolContractTestBase : DAppContractTestBase<DividendPoolContractTestModule>
    {
        // You can get address of any contract via GetAddress method, for example:
        // internal Address DAppContractAddress => GetAddress(DAppSmartContractAddressNameProvider.StringName);

        internal DividendPoolContractContainer.DividendPoolContractStub GetDividendPoolContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<DividendPoolContractContainer.DividendPoolContractStub>(DAppContractAddress, senderKeyPair);
        }
        
        internal TokenContractContainer.TokenContractStub GetTokenContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<TokenContractContainer.TokenContractStub>(TokenContractAddress, senderKeyPair);
        }
        
    }
}