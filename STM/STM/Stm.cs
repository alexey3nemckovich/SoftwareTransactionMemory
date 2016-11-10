using TransactionalAction = System.Action<STM.IStmTransaction>;

namespace STM
{

    public static class Stm
    {

        private static bool initialized = false;
        private static int maxTransactionsImbrication = 10;
        public static object[] commitLock = new object[maxTransactionsImbrication];

        public static int MaxTransactionsImbrication
        {
            get
            {
                return maxTransactionsImbrication;
            }
        }

        public static void Init()
        {
            if(!initialized)
            {
                for (int i = 0; i < maxTransactionsImbrication; i++)
                {
                    commitLock[i] = new object();
                }
                initialized = true;
            }
        }

        public static void Do(TransactionalAction action, bool repeatOnFail = true)
        {
            IStmTransaction transaction = new LoggingStmTransaction(new StmTransaction(action));
            if(repeatOnFail)
            {
                bool commited = false;
                while(!commited)
                {
                    transaction.Begin();
                    commited = transaction.TryCommit();
                    if (!commited)
                    {
                        transaction.Rollback();
                    }
                    else
                    {
                        bool parentTransactionConflictOccured = WaitOtherSubtransactionsToCommit(transaction);
                        if (parentTransactionConflictOccured)
                        {
                            transaction.Rollback();
                            commited = false;
                        }
                    }
                }
            }
            else
            {
                transaction.Begin();
                bool commited = transaction.TryCommit();
                if(!commited)
                {
                    transaction.Rollback();
                }
                else
                {
                    bool parentTransactionConflictOccured = WaitOtherSubtransactionsToCommit(transaction);
                    if (parentTransactionConflictOccured)
                    {
                        transaction.Rollback();
                    }   
                }
            }
        }

        private static bool WaitOtherSubtransactionsToCommit(IStmTransaction transaction)
        {
            if (transaction.ParentTransaction != null)
            {
                int countSubTransactionsCommited = 0;
                while (countSubTransactionsCommited != transaction.ParentTransaction.CountSubtransactions)
                {
                    countSubTransactionsCommited = 1;
                    bool wasParentConflict = false;
                    foreach (IStmTransaction subTransaction in transaction.ParentTransaction.SubTransactions)
                    {
                        if (subTransaction.Number != transaction.Number)
                        {
                            switch (subTransaction.State)
                            {
                                case I_STM_TRANSACTION_STATE.COMMITED:
                                    countSubTransactionsCommited++;
                                    break;
                                case I_STM_TRANSACTION_STATE.PARENT_CONFLICT:
                                    wasParentConflict = true;
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    if (wasParentConflict)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

    }

}