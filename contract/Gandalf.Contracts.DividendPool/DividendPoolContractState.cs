using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Gandalf.Contracts.DividendPoolContract
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public partial class DividendPoolContractState : ContractState
    {   
        public SingletonState<Address> Owner { get; set; }
        public SingletonState<TokenList> TokenList { get; set; }
        // token=> number
        public MappedState<string,BigIntValue> PerBlock { get; set; }
        
        public SingletonState<PoolInfoList> PoolInfoList { get; set; }
        // pid=>user=>userInfo
        public MappedState<int, Address, User> UserInfo { get; set; }
        public Int64State TotalAllocPoint { get; set; }
        public Int64State StartBlock { get; set; }
        public Int64State EndBlock { get; set; }
        public Int64State Cycle { get; set; }
        
        // Pid=> UserAddress => token=> debt
        public MappedState<long, Address, string, BigIntValue> RewardDebt { get; set; }
        public MappedState<long, string, BigIntValue> AccPerShare { get; set; }
    }
}