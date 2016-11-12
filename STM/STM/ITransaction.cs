using System.Collections.Generic;

namespace STM
{

    public enum I_TRANSACTION_STATE { COMMITED, CONFLICT, PARENT_CONFLICT, ON_EXECUTE, ROLLBACKED_BY_SUBTRANSACTION, GOING_TO_COMMIT };

    public interface ITransaction
    {
        //Properties
        I_TRANSACTION_STATE   State { get; }
        object                SubTransactionsLocker { get; }
        int                   CountSubtransactions { get; }
        string                Name { get; }
        int                   Number { get; }
        int                   Imbrication { get; }
        //Transactions relations
        ITransaction          ParentTransaction { get; }
        List<ITransaction>    SubTransactions { get; }
        //Methods
        void SetParentTransaction(ITransaction parentTransaction);
        void AddSubTransaction(ITransaction subTransaction);
        void Begin();
        bool TryCommit();
        void Rollback();
        T    Get<T>(IStmMemory memoryRef) where T : struct;
        void Set<T>(IStmMemory memoryRef, object value, MemoryTuple<T> memoryTuple = null) where T : struct;
    }

}