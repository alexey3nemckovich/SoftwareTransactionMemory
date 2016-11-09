using TransactionalAction = System.Action<STM.IStmTransaction>;

namespace STM
{

    public static class Stm
    {

        public static object commitLock = new object();
        
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
            }
        }

    }

}