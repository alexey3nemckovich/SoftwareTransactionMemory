using TransactionalAction = System.Action<STM.IStmTransaction>;

namespace STM
{
    
    public enum I_STM_TRANSACTION_STATE {COMMITED, CONFLICT, PARENT_CONFLICT, CANCELLED, ON_EXECUTE};

    public interface IStmTransaction
    {
        //Properties
        object                  SubTransactionsLocker { get; }
        int                     CountSubtransactions { get; }
        string                  Name { get; }
        int                     Number { get; }
        int                     Imbrication { get; }
        TransactionalAction     Action { get; }
        I_STM_TRANSACTION_STATE State { get; }
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
        void SetMemoryVersion(object stmRef);
        void FixMemoryVersion<T>(StmMemory<T> memoryRef, MemoryTuple<T> memoryTuple) where T : struct;
        bool ValidateMemoryVersion(object memoryRef);
    }

}