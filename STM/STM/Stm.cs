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
                    if(!commited)
                    {
                        transaction.Rollback();
                    }
                }
            }
            else
            {
                transaction.Begin();
                transaction.TryCommit();
                if(transaction.State == I_STM_TRANSACTION_STATE.CONFLICT)
                {
                    transaction.Rollback();
                }
            }
        }

    }

}