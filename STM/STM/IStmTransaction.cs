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
        object GetTransactionMemoryValue(IInnerTransactionStmMemory memoryRef);
        void   SetMemoryVersionsToCurrent();
        void   FixMemoryVersion(IInnerTransactionStmMemory memoryRef, MemoryTuple memoryTuple);
        bool   ValidateMemoryVersion(IInnerTransactionStmMemory memoryRef);
    }

}