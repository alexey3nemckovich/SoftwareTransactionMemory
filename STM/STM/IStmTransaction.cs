using System.Collections.Generic;
using TransactionalAction = System.Action<STM.IStmTransaction>;

namespace STM
{
    
    public interface IStmTransaction : ITransaction
    {
        //Properties
        TransactionalAction     Action { get; }
        //Transactions relations
        new IStmTransaction ParentTransaction { get; }
        new List<IStmTransaction> SubTransactions { get; }
        //Transactions interaction methods
        void SetMemoryVersionsToCurrent();
        ImbricationMemoryTuple<T> GetCurrentImbricationMemoryTuple<T>(object memoryRef) where T : struct;
        void FixMemoryVersion<T>(StmMemory<T> memoryRef, MemoryTuple<T> memoryTuple) where T : struct;
        bool ValidateMemoryVersion(object memoryRef);
    }

}