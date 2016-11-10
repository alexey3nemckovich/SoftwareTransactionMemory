using System.Collections.Generic;
using TransactionalAction = System.Action<STM.IStmTransaction>;

namespace STM
{
    
    public enum I_STM_TRANSACTION_STATE {COMMITED, CONFLICT, PARENT_CONFLICT, CANCELLED, ON_EXECUTE, ROLLBACKED_BY_SUBTRANSACTION, READY_TO_TRY_TO_COMMIT};

    public interface IStmTransaction
    {
        //Properties
        object                  SubTransactionsLocker { get; }
        int                     CountSubtransactions { get; }
        List<IStmTransaction>   SubTransactions { get; }
        string                  Name { get; }
        int                     Number { get; }
        int                     Imbrication { get; }
        TransactionalAction     Action { get; }
        I_STM_TRANSACTION_STATE State { get; set; }
        IStmTransaction         ParentTransaction { get; }
        //Methods
        void Begin();
        void Rollback();
        bool TryCommit();
        T    Get<T>(StmMemory<T> memoryRef, MemoryTuple<T> memoryTuple = null) where T : struct;
        void Set<T>(StmMemory<T> memoryRef, object value, MemoryTuple<T> memoryTuple = null) where T : struct;
        void SetParentTransaction(IStmTransaction parentTransaction);
        void AddSubTransaction(IStmTransaction subTransaction);
        //Transactions dialog methods
        Dictionary<object, int> GetMemoryStartVersions();
        void SetMemoryVersion(object memoryRef);
        void FixMemoryVersion<T>(StmMemory<T> memoryRef, MemoryTuple<T> memoryTuple) where T : struct;
        bool ValidateMemoryVersion(object memoryRef);
    }

}