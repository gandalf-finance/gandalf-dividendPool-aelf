using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Cryptography.ECDSA;
using AElf.Types;
using Awaken.Contracts.DividendPoolContract;
using Shouldly;

namespace Awaken.Contracts.DividendPool
{
    public partial class DividendPoolContractTests
    {
        public Address Owner;
        public ECKeyPair OwnerKeyPair;
        public Address Tom;
        public ECKeyPair TomKeyPair;
        public Address Kitty;
        public ECKeyPair KittyKeyPair;
        public ECKeyPair PolyKeyPair;
        
        
        public const string LockedToken = "ISTAR";
        public const string RewardToken1 = "USDT";
        public const string RewardToken2 = "AAAE";
        public const string RewardToken3 = "SLP";

        private async Task<DividendPoolContractContainer.DividendPoolContractStub> Initialize()
        {
            OwnerKeyPair = SampleAccount.Accounts.First().KeyPair;
            Owner = Address.FromPublicKey(OwnerKeyPair.PublicKey);
            TomKeyPair = SampleAccount.Accounts[1].KeyPair;
            Tom = Address.FromPublicKey(TomKeyPair.PublicKey);
            KittyKeyPair = SampleAccount.Accounts[2].KeyPair;
            Kitty = Address.FromPublicKey(KittyKeyPair.PublicKey);
            var stub = GetDividendPoolContractStub(OwnerKeyPair);
            PolyKeyPair = SampleAccount.Accounts[3].KeyPair;
                
            // initialize contract.
            await stub.Initialize.SendAsync(new InitializeInput
            {
                Owner = Owner,
                Cycle = 100
            });
            await CreateToken();
            return stub;
        }

        private async Task CreateToken(string symbol, ECKeyPair issurerKeyPair, long totalSupply,int decimals)
        {
            var tokenContractStub = GetTokenContractStub(issurerKeyPair);
            var issurer = Address.FromPublicKey(issurerKeyPair.PublicKey);
            await tokenContractStub.Create.SendAsync(new CreateInput
            {   
                Decimals = decimals,
                Symbol = symbol,
                Issuer = issurer,
                IsBurnable = true,
                TokenName = $"{symbol}-token",
                TotalSupply = totalSupply
            });
            await tokenContractStub.Issue.SendAsync(new IssueInput
            {
                Amount = totalSupply,
                Symbol = symbol,
                To = issurer
            });
            
        }

        private async Task CreateToken()
        {
            var tokenStub = GetTokenContractStub(OwnerKeyPair);

            await CreateToken(LockedToken, OwnerKeyPair, 100000000000,5);
            
            await tokenStub.Transfer.SendAsync(new TransferInput
            {
                Amount = 60000000000,
                Symbol = LockedToken,
                To = Tom
            });

            await tokenStub.Transfer.SendAsync(new TransferInput
            {
                Amount = 20000000000,
                Symbol = LockedToken,
                To = Kitty
            });

            var tomBalance = await tokenStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Tom,
                Symbol = LockedToken
            });
            tomBalance.Balance.ShouldBe(60000000000);

            var kittyBalance = await tokenStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Kitty,
                Symbol = LockedToken
            });
            kittyBalance.Balance.ShouldBe(20000000000);

            await CreateToken(RewardToken1, OwnerKeyPair, 10000000000,5);
            
            var usdt = await tokenStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Owner,
                Symbol = RewardToken1
            });
            usdt.Balance.ShouldBe(10000000000);
            
            await CreateToken(RewardToken2, OwnerKeyPair, 10000000000, 5);
            await CreateToken(RewardToken3, OwnerKeyPair, 10000000000, 5);
        }
      
    }
}