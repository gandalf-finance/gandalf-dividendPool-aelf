using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.Types;
using Gandalf.Contracts.DividendPoolContract;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Threading;
using Xunit;

namespace Gandalf.Contracts.DividendPool
{
    public partial class DividendPoolContractTests : DividendPoolContractTestBase
    {
        [Fact]
        public async Task Test()
        {
            await Initialize();
        }

        [Fact]
        public async Task Test_And_Verify_Bugs()
        {
            var adminStub = await Initialize();
            var allocPoint = 20;
            var dividendAmount = 100000;
            var perBlock = 1000;
            var firstDeposit = 100;
            int firstEndBlocks = dividendAmount.Div(perBlock);
            var tomDividendPoolContractStub = GetDividendPoolContractStub(TomKeyPair);
            var kittyDividendPoolContractStub = GetDividendPoolContractStub(KittyKeyPair);
            var adminTokenStub = GetTokenContractStub(OwnerKeyPair);
            var tomTokenContractStub = GetTokenContractStub(TomKeyPair);
            var kittyTokenContractStub = GetTokenContractStub(KittyKeyPair);

            await adminTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = dividendAmount,
                Spender = DAppContractAddress,
                Symbol = RewardToken1
            });

            await adminTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = dividendAmount,
                Spender = DAppContractAddress,
                Symbol = RewardToken2
            });

            await AddPool(adminStub, allocPoint);
            await AddToken(adminStub, RewardToken1);
            await AddToken(adminStub, RewardToken2);

            var currentBlockHeight = await GetCurrentBlockHeight();
            var newRewardInputFirst = new NewRewardInput();
            newRewardInputFirst.Tokens.Add(RewardToken1);
            newRewardInputFirst.Tokens.Add(RewardToken2);
            newRewardInputFirst.Amounts.Add(dividendAmount);
            newRewardInputFirst.Amounts.Add(dividendAmount);
            newRewardInputFirst.PerBlocks.Add(perBlock);
            newRewardInputFirst.PerBlocks.Add(perBlock);

            newRewardInputFirst.StartBlock = currentBlockHeight.Add(2);
            newRewardInputFirst.StartBlock.ShouldBe(19L);
            await adminStub.NewReward.SendAsync(newRewardInputFirst);
            {
                var kittybalanceRewardToken2 = await tomTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = Kitty,
                    Symbol = RewardToken2
                });
                kittybalanceRewardToken2.Balance.ShouldBe(0l);
            }
            await tomTokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Amount = firstDeposit,
                Spender = DAppContractAddress,
                Symbol = LockedToken
            });
            await kittyTokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Amount = firstDeposit,
                Spender = DAppContractAddress,
                Symbol = LockedToken
            });

            await tomDividendPoolContractStub.Deposit.SendAsync(new TokenOptionInput
            {
                Amount = firstDeposit,
                Pid = 0
            });
           
            await kittyDividendPoolContractStub.Deposit.SendAsync(new TokenOptionInput
            {
                Amount = firstDeposit,
                Pid = 0
            });

            var skipBlock = await SkipBlock(firstEndBlocks.Div(2).Add(2));
            skipBlock.ShouldBe(74L);
            {
                var balanceCallAsync = await tomTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = Tom,
                    Symbol = RewardToken1
                });
                balanceCallAsync.Balance.ShouldBe(0);
                var callAsync = await tomTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = Tom,
                    Symbol = RewardToken2
                });
                callAsync.Balance.ShouldBe(0);
            }
            
            await tomDividendPoolContractStub.Withdraw.SendAsync(new TokenOptionInput
            {
                Amount = firstDeposit,
                Pid = 0
            });

            {
                var callAsync = await tomTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = Tom,
                    Symbol = RewardToken2
                });
                callAsync.Balance.ShouldBe(27500L);
            }
            // Go to first round1 end block.
            {
                var currentBlock = await GetCurrentBlockHeight();
                var endBlockCallAsync = await adminStub.EndBlock.CallAsync(new Empty());
                var skipBlocks = endBlockCallAsync.Value.Sub(currentBlock).Add(1);
                var block = await SkipBlock(int.Parse(skipBlocks.ToString()));
                block.ShouldBe(endBlockCallAsync.Value.Add(1));
            }
           
            {
                await kittyDividendPoolContractStub.Withdraw.SendAsync(new TokenOptionInput
                {
                    Amount = 50,
                    Pid = 0
                });

                var balanceCallAsync = await kittyTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = Kitty,
                    Symbol = RewardToken2
                });

                balanceCallAsync.Balance.ShouldBe(70500L);
            }
            var secondPerBlock = 50;
            {
                // round1 finished ,check surplus

                var balanceRewardToken1 = await tomTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = DAppContractAddress,
                    Symbol = RewardToken1
                });
                balanceRewardToken1.Balance.ShouldBe(2000);
                var tomBalanceRewardToken1 = await tomTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = Tom,
                    Symbol = RewardToken1
                });
                tomBalanceRewardToken1.Balance.ShouldBe(27500L);

                var kittyBalanceRewardToken1 = await tomTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = Kitty,
                    Symbol = RewardToken1
                });
                kittyBalanceRewardToken1.Balance.ShouldBe(70500L);
                balanceRewardToken1.Balance.Add(tomBalanceRewardToken1.Balance).Add(kittyBalanceRewardToken1.Balance).ShouldBe(dividendAmount);
            }
            {
                var contractBalanceRewardToken2 = await tomTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = DAppContractAddress,
                    Symbol = RewardToken2
                });
                contractBalanceRewardToken2.Balance.ShouldBe(2000);

                var kittyBalanceRewardToken2 = await tomTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = Kitty,
                    Symbol = RewardToken2,
                });
                kittyBalanceRewardToken2.Balance.ShouldBe(70500L);
                var tomBalanceRewardToken2 = await tomTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = Tom,
                    Symbol = RewardToken2
                });

                tomBalanceRewardToken2.Balance.ShouldBe(27500L);
                contractBalanceRewardToken2.Balance.Add(tomBalanceRewardToken2.Balance).Add(kittyBalanceRewardToken2.Balance).ShouldBe(dividendAmount);
            }
            {
                // round2
                await adminTokenStub.Approve.SendAsync(new ApproveInput
                {
                    Amount = dividendAmount,
                    Spender = DAppContractAddress,
                    Symbol = RewardToken1
                });

                await adminTokenStub.Approve.SendAsync(new ApproveInput
                {
                    Amount = dividendAmount,
                    Spender = DAppContractAddress,
                    Symbol = RewardToken2
                });

                await adminTokenStub.Approve.SendAsync(new ApproveInput
                {
                    Amount = dividendAmount,
                    Spender = DAppContractAddress,
                    Symbol = RewardToken3
                });

                var newRewardInputSecond = new NewRewardInput();
                newRewardInputSecond.Tokens.Add(RewardToken1);
                newRewardInputSecond.Tokens.Add(RewardToken2);
                newRewardInputSecond.Tokens.Add(RewardToken3);
                newRewardInputSecond.Amounts.Add(dividendAmount);
                newRewardInputSecond.Amounts.Add(dividendAmount);
                newRewardInputSecond.Amounts.Add(dividendAmount);
                newRewardInputSecond.PerBlocks.Add(secondPerBlock);
                newRewardInputSecond.PerBlocks.Add(secondPerBlock);
                newRewardInputSecond.PerBlocks.Add(secondPerBlock);
                newRewardInputSecond.StartBlock = GetCurrentBlockHeight().Result.Add(10);

                await AddToken(adminStub, RewardToken3);
                await adminStub.NewReward.SendAsync(newRewardInputSecond);
            }
        }

        [Fact]
        public async Task Add_Pool_Should_Works()
        {
            var adminStub = await Initialize();
            var allocPoint = 10;
            await AddPool(adminStub, allocPoint);

            var poolInfo = await adminStub.PoolInfo.CallAsync(new Int32Value
            {
                Value = 0
            });
            poolInfo.AllocPoint.ShouldBe(allocPoint);
            poolInfo.LpToken.ShouldBe(LockedToken);
        }

        [Fact]
        public async Task Add_Token_Should_Work()
        {
            var adminStub = await Initialize();
            var allocPoint = 10;
            await AddPool(adminStub, allocPoint);
            await AddToken(adminStub, RewardToken1);

            var b = await adminStub.IsTokenList.CallAsync(new Token
            {
                Value = RewardToken1
            });
            b.Value.ShouldBe(true);
        }


        [Fact]
        public async Task NewReward_Should_Work()
        {
            var adminStub = await Initialize();
            var allocPoint = 10;
            await AddPool(adminStub, allocPoint);
            await AddToken(adminStub, RewardToken1);
            var amount = 100000000;
            var perBlockAmount = 1000;
            var adminTokenStub = GetTokenContractStub(OwnerKeyPair);
            await adminTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = amount,
                Spender = DAppContractAddress,
                Symbol = RewardToken1
            });
            var chain = await GetChain();
            var blockHeigh = chain.BestChainHeight;

            var input = new NewRewardInput();
            input.Tokens.Add(RewardToken1);
            input.Amounts.Add(amount);
            input.PerBlocks.Add(perBlockAmount);

            var startBlock = blockHeigh.Add(2);
            input.StartBlock = startBlock;
            await adminStub.NewReward.SendAsync(input);
        }

        [Fact]
        public async Task Deposit_And_Withdraw_Sould_Work()
        {
            await NewReward_Should_Work();
            var adminStub = GetDividendPoolContractStub(OwnerKeyPair);
            await adminStub.AddToken.SendAsync(new Token
            {
                Value = LockedToken
            });
            var tomTokenStub = GetTokenContractStub(TomKeyPair);
            var tomBalanceLockedToken = await tomTokenStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Tom,
                Symbol = LockedToken
            });
            tomBalanceLockedToken.Balance.ShouldBe(60000000000);

            await tomTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 100000,
                Spender = DAppContractAddress,
                Symbol = LockedToken
            });
            var tomDivStub = GetDividendPoolContractStub(TomKeyPair);
            await tomDivStub.Deposit.SendAsync(new TokenOptionInput
            {
                Pid = 0,
                Amount = 50000
            });
            tomBalanceLockedToken = await tomTokenStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Tom,
                Symbol = LockedToken
            });
            tomBalanceLockedToken.Balance.ShouldBe(60000000000 - 50000);


            var tomUserInfo = await tomDivStub.UserInfo.CallAsync(new UserInfoInput
            {
                Pid = 0,
                User = Tom
            });
            tomUserInfo.Value.Value.ShouldBe("50000");
            var poolInfo = await tomDivStub.PoolInfo.CallAsync(new Int32Value
            {
                Value = 0
            });
            poolInfo.TotalAmount.Value.ShouldBe("50000");
            // withdraw
            await tomDivStub.Withdraw.SendAsync(new TokenOptionInput
            {
                Pid = 0,
                Amount = 30000
            });

            tomBalanceLockedToken = await tomTokenStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Tom,
                Symbol = LockedToken
            });
            tomBalanceLockedToken.Balance.ShouldBe(60000000000 - 50000 + 30000);

            poolInfo = await tomDivStub.PoolInfo.CallAsync(new Int32Value
            {
                Value = 0
            });
            poolInfo.TotalAmount.Value.ShouldBe("20000");
            poolInfo.LastRewardBlock.ShouldBe(20L);
            tomUserInfo = await tomDivStub.UserInfo.CallAsync(new UserInfoInput
            {
                Pid = 0,
                User = Tom
            });

            tomUserInfo.Value.Value.ShouldBe("20000");
        }

        /**
         * Add Two Tokens,new reward one token
         */
        [Fact]
        public async Task Case_1_Should_Work()
        {
            var perBlockAmount = 1000;
            var adminStub = GetDividendPoolContractStub(OwnerKeyPair);
            await NewReward_Should_Work();
            await AddToken(adminStub, LockedToken);

            var tomDivStub = GetDividendPoolContractStub(TomKeyPair);
            var tomTokenStub = GetTokenContractStub(TomKeyPair);
            await tomTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 50000,
                Spender = DAppContractAddress,
                Symbol = LockedToken
            });
            await tomDivStub.Deposit.SendAsync(new TokenOptionInput
            {
                Amount = 50000,
                Pid = 0
            });

            var depositBlockHeight = await GetCurrentBlockHeight();
            var skipBlockHeight = await SkipBlock(50);
            var pending = await tomDivStub.Pending.CallAsync(new PendingInput
            {
                Pid = 0,
                User = Tom
            });

            (await adminStub.IsTokenList.CallAsync(new Token
            {
                Value = pending.Tokens[0]
            })).Value.ShouldBe(true);
            (await adminStub.IsTokenList.CallAsync(new Token
            {
                Value = pending.Tokens[1]
            })).Value.ShouldBe(true);
            var pendingRewardToken1Expect = skipBlockHeight.Sub(depositBlockHeight).Add(1).Mul(perBlockAmount);
            pending.Amounts[0].ShouldBe(pendingRewardToken1Expect);

            await tomDivStub.Withdraw.SendAsync(new TokenOptionInput
            {
                Amount = 50000,
                Pid = 0
            });
            var withdrawBlockHeight = await GetCurrentBlockHeight();
            var rewardTokenBalance = await tomTokenStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Tom,
                Symbol = RewardToken1
            });
            var withdrawRewardToken1Expect = withdrawBlockHeight.Sub(depositBlockHeight).Mul(perBlockAmount);
            withdrawRewardToken1Expect.ShouldBe(rewardTokenBalance.Balance);
        }

        /**
         *  add one token, new reward one token without deposit
         */
        [Fact]
        public async Task Case_2_Should_Work()
        {
            var adminStub = GetDividendPoolContractStub(OwnerKeyPair);
            await NewReward_Should_Work();
            var tomDivStub = GetDividendPoolContractStub(TomKeyPair);
            var tomTokenStub = GetTokenContractStub(TomKeyPair);

            var depositBlockHeight = await GetCurrentBlockHeight();
            var skipBlockHeight = await SkipBlock(50);
            var pending = await tomDivStub.Pending.CallAsync(new PendingInput
            {
                Pid = 0,
                User = Tom
            });

            (await adminStub.IsTokenList.CallAsync(new Token
            {
                Value = pending.Tokens[0]
            })).Value.ShouldBe(true);
            pending.Amounts[0].ShouldBe(0);
            pending.Tokens[0].ShouldBe(RewardToken1);
        }

        /**
         * add another token during dividend period.
         */
        [Fact]
        public async Task Case_3_Should_Work()
        {
            var perBlockAmount = 1000;
            await NewReward_Should_Work();
            var adminDivStub = GetDividendPoolContractStub(OwnerKeyPair);
            var startBlock = await adminDivStub.StartBlock.CallAsync(new Empty());
            var endBlock = await adminDivStub.EndBlock.CallAsync(new Empty());

            await SkipBlock(10);

            var currentBlockHeight = await GetCurrentBlockHeight();
            currentBlockHeight.ShouldBeGreaterThan(startBlock.Value);
            currentBlockHeight.ShouldBeLessThan(endBlock.Value);
            await AddToken(adminDivStub, LockedToken);

            var tokenTomStub = GetTokenContractStub(TomKeyPair);
            var dividendPoolTomStub = GetDividendPoolContractStub(TomKeyPair);
            var before = await tokenTomStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Tom,
                Symbol = LockedToken
            });
            before.Balance.ShouldBe(60000000000L);
            await tokenTomStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 50000,
                Spender = DAppContractAddress,
                Symbol = LockedToken
            });
            await dividendPoolTomStub.Deposit.SendAsync(new TokenOptionInput
            {
                Amount = 50000,
                Pid = 0
            });

            var depositHeight = await GetCurrentBlockHeight();
            (await tokenTomStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Tom,
                Symbol = LockedToken
            })).Balance.ShouldBe(60000000000L - 50000);
            var skipBlocks = await SkipBlock(50);

            var pending = await dividendPoolTomStub.Pending.CallAsync(new PendingInput
            {
                Pid = 0,
                User = Tom
            });

            var pendingExpext = skipBlocks.Sub(depositHeight).Add(1).Mul(perBlockAmount);
            pending.Amounts[0].ShouldBe(pendingExpext);

            await tokenTomStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 50000,
                Spender = DAppContractAddress,
                Symbol = RewardToken1
            });
            await SkipBlock(100);
            currentBlockHeight = await GetCurrentBlockHeight();
            currentBlockHeight.ShouldBeGreaterThan(endBlock.Value);

            // reward again
            var amount = 100000000;
            var adminTokenStub = GetTokenContractStub(OwnerKeyPair);
            await adminTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = amount,
                Spender = DAppContractAddress,
                Symbol = RewardToken1
            });

            var input = new NewRewardInput();
            input.Tokens.Add(RewardToken1);
            input.Amounts.Add(amount);
            input.PerBlocks.Add(perBlockAmount);
            input.StartBlock = currentBlockHeight.Add(3);
            await adminDivStub.NewReward.SendAsync(input);
        }

        [Fact]
        public async Task Usdt_Dividend_Single_Person_Test()
        {
            var allocPoint = 10;
            var adminDivStub = await Initialize();
            var tomTokenStub = GetTokenContractStub(TomKeyPair);
            var tomDivStub = GetDividendPoolContractStub(TomKeyPair);

            await adminDivStub.Add.SendAsync(new AddPoolInput
            {
                AllocationPoint = allocPoint,
                TokenSymbol = LockedToken,
                WithUpdate = false
            });
            await adminDivStub.AddToken.SendAsync(new Token
            {
                Value = RewardToken1,
            });

            var amount = 600000000;
            var perBlockAmount = 1000;
            var adminTokenStub = GetTokenContractStub(OwnerKeyPair);
            await adminTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = amount,
                Spender = DAppContractAddress,
                Symbol = RewardToken1
            });

            var newRewardInput = new NewRewardInput();
            newRewardInput.Tokens.Add(RewardToken1);
            newRewardInput.Amounts.Add(amount);
            newRewardInput.PerBlocks.Add(perBlockAmount);

            var currentBlockHeight = await GetCurrentBlockHeight();
            var startBlock = currentBlockHeight.Add(2);
            newRewardInput.StartBlock = startBlock;
            await adminDivStub.NewReward.SendAsync(newRewardInput);

            await tomTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 50000,
                Spender = DAppContractAddress,
                Symbol = LockedToken
            });

            await tomDivStub.Deposit.SendAsync(new TokenOptionInput
            {
                Amount = 50000,
                Pid = 0
            });

            var depositBlockHeight = await GetCurrentBlockHeight();
            currentBlockHeight = await SkipBlock(50);

            // check pending
            var pending = await tomDivStub.Pending.CallAsync(new PendingInput
            {
                Pid = 0,
                User = Tom
            });
            (await adminDivStub.IsTokenList.CallAsync(new Token
            {
                Value = pending.Tokens[0]
            })).Value.ShouldBe(true);

            var reward = currentBlockHeight.Sub(depositBlockHeight).Add(1).Mul(perBlockAmount);
            pending.Amounts[0].ShouldBe(reward);
        }

        [Fact]
        public async Task Dividend_Single_Person_Multi_Tokens()
        {
            var token1PerBlockAmount = 1000;
            var token2PerBlockAmount = 2000;
            var adminDivStub = await Initialize();
            var tomTokenStub = GetTokenContractStub(TomKeyPair);
            var tomDivStub = GetDividendPoolContractStub(TomKeyPair);
            await AddPool(adminDivStub, 10);
            await AddToken(adminDivStub, RewardToken1);
            await AddToken(adminDivStub, RewardToken2);

            var newRewardInput = new NewRewardInput();
            newRewardInput.Tokens.Add(RewardToken1);
            newRewardInput.Amounts.Add(600000000);
            newRewardInput.PerBlocks.Add(token1PerBlockAmount);

            newRewardInput.Tokens.Add(RewardToken2);
            newRewardInput.Amounts.Add(100000000);
            newRewardInput.PerBlocks.Add(token2PerBlockAmount);
            newRewardInput.StartBlock = await GetCurrentBlockHeight() + 6;

            var tokenOptionInput = new TokenOptionInput
            {
                Amount = 50000,
                Pid = 0
            };


            await deposit(tomTokenStub, tomDivStub, tokenOptionInput, LockedToken);
            var depositHeight = await GetCurrentBlockHeight();

            await NewReward(newRewardInput);
            await SkipBlock(50);

            var currentBlockHeight = await GetCurrentBlockHeight();

            var pending = await tomDivStub.Pending.CallAsync(new PendingInput
            {
                Pid = 0,
                User = Tom
            });

            var token1Expect = currentBlockHeight.Sub(newRewardInput.StartBlock).Add(1).Mul(token1PerBlockAmount);
            var token2Expect = currentBlockHeight.Sub(newRewardInput.StartBlock).Add(1).Mul(token2PerBlockAmount);
            pending.Amounts[0].ShouldBe(token1Expect);
            pending.Amounts[1].ShouldBe(token2Expect);
        }


        [Fact]
        public async Task Dividend_Multi_Person_Single_Pool()
        {
            var perBlockAmount = 1000;
            var adminDivStub = await Initialize();
            var tomTokenStub = GetTokenContractStub(TomKeyPair);
            var tomDivStub = GetDividendPoolContractStub(TomKeyPair);

            await AddPool(adminDivStub, 10);
            await AddToken(adminDivStub, RewardToken1);
            var newRewardInput = new NewRewardInput();
            newRewardInput.Amounts.Add(600000000);
            newRewardInput.Tokens.Add(RewardToken1);
            newRewardInput.PerBlocks.Add(perBlockAmount);
            var currentBlockHeight = await GetCurrentBlockHeight();
            newRewardInput.StartBlock = currentBlockHeight.Add(3);
            await NewReward(newRewardInput);

            var tom = new TokenOptionInput
            {
                Amount = 50000,
                Pid = 0
            };
            await deposit(tomTokenStub, tomDivStub, tom, LockedToken);
            var tomDepositHeight = await GetCurrentBlockHeight();
            var tokenKittyStub = GetTokenContractStub(KittyKeyPair);
            var dividendKittyStub = GetDividendPoolContractStub(KittyKeyPair);
            var kittyOption = new TokenOptionInput
            {
                Amount = 80000,
                Pid = 0
            };
            await SkipBlock(40);
            await deposit(tokenKittyStub, dividendKittyStub, kittyOption, LockedToken);
            var kittyDepositHeight = await GetCurrentBlockHeight();
            currentBlockHeight = await SkipBlock(30);

            // check pending
            var tomPending = await adminDivStub.Pending.CallAsync(new PendingInput
            {
                Pid = 0,
                User = Tom
            });

            var kittyPending = await adminDivStub.Pending.CallAsync(new PendingInput
            {
                Pid = 0,
                User = Kitty
            });

            var tomExpect = kittyDepositHeight.Sub(tomDepositHeight).Mul(perBlockAmount) + currentBlockHeight
                .Sub(kittyDepositHeight).Add(1).Mul(perBlockAmount).Mul(50000).Div(50000 + 80000);
            tomPending.Amounts[0].ShouldBe(tomExpect);
            var kittyExpect = currentBlockHeight.Sub(kittyDepositHeight).Add(1).Mul(perBlockAmount).Mul(80000)
                .Div(80000 + 50000);
            kittyPending.Amounts[0].ShouldBe(kittyExpect);
        }

        private async Task deposit(TokenContractContainer.TokenContractStub tokenStub,
            DividendPoolContractContainer.DividendPoolContractStub divStub, TokenOptionInput tokenOptionInput,
            string lockedToken)
        {
            await tokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = long.Parse(tokenOptionInput.Amount.Value),
                Spender = DAppContractAddress,
                Symbol = lockedToken
            });

            await divStub.Deposit.SendAsync(tokenOptionInput);
        }


        private async Task NewReward(NewRewardInput input)
        {
            var tokenContractStub = GetTokenContractStub(OwnerKeyPair);
            var dividendPoolContractStub = GetDividendPoolContractStub(OwnerKeyPair);
            for (int i = 0; i < input.Tokens.Count; i++)
            {
                await tokenContractStub.Approve.SendAsync(new ApproveInput
                {
                    Amount = long.Parse(input.Amounts[i].Value),
                    Spender = DAppContractAddress,
                    Symbol = input.Tokens[i]
                });
            }

            await dividendPoolContractStub.NewReward.SendAsync(input);
        }

        private async Task<long> SkipBlock(int skipBlocks)
        {
            var tokenStub = GetTokenContractStub(OwnerKeyPair);
            var first = (await GetChain()).BestChainHeight;
            for (int i = 0; i < skipBlocks; i++)
            {
                await tokenStub.Transfer.SendAsync(new TransferInput
                {
                    Symbol = RewardToken2,
                    Amount = 1,
                    To = Address.FromPublicKey(PolyKeyPair.PublicKey)
                });
            }

            var second = (await GetChain()).BestChainHeight;
            second.Sub(first).ShouldBe(skipBlocks);
            return second;
        }


        private async Task<long> GetCurrentBlockHeight()
        {
            return (await GetChain()).BestChainHeight;
        }

        private async Task<Chain> GetChain()
        {
            var blockchainService = await GetBlockService();
            return AsyncHelper.RunSync(blockchainService.GetChainAsync);
        }

        private async Task<IBlockchainService> GetBlockService()
        {
            var blockchainService = Application.ServiceProvider.GetRequiredService<IBlockchainService>();
            return blockchainService;
        }

        private async Task AddToken(DividendPoolContractContainer.DividendPoolContractStub adminStub, string token)
        {
            await adminStub.AddToken.SendAsync(new Token
            {
                Value = token
            });
        }

        private async Task AddPool(DividendPoolContractContainer.DividendPoolContractStub adminStub, int allocPoint)
        {
            await adminStub.Add.SendAsync(new AddPoolInput
            {
                AllocationPoint = allocPoint,
                TokenSymbol = LockedToken,
                WithUpdate = false
            });

            var length = await adminStub.PoolLength.CallAsync(new Empty());
            length.Value.ShouldBe(1);
        }
    }
}