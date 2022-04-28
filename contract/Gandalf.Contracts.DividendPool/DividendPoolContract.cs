using AElf;
using AElf.Sdk.CSharp;
using AElf.Sdk.CSharp.State;
using Google.Protobuf.WellKnownTypes;

namespace Gandalf.Contracts.DividendPoolContract
{
    /// <summary>
    /// The C# implementation of the contract defined in dividend_pool_contract.proto that is located in the "protobuf"
    /// folder.
    /// Notice that it inherits from the protobuf generated code. 
    /// </summary>
    public partial class DividendPoolContract : DividendPoolContractContainer.DividendPoolContractBase
    {   
        /// <summary>
        /// Initialize the contract.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Empty Initialize(InitializeInput input)
        {
            Assert(State.Owner.Value==null,"Already initialized.");
            State.Owner.Value = input.Owner == null || input.Owner.Value.IsNullOrEmpty() ? Context.Sender : input.Owner;
            State.Cycle.Value = input.Cycle;
            Context.Fire(new SetCycle
            {
                Cycle = input.Cycle
            });

            State.PoolInfoList.Value = new PoolInfoList();
            State.TokenList.Value = new TokenList();
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            return new Empty();
        }
    }
}