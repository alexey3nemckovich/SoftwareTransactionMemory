using System.IO;
using TransactionalAction = System.Action<STM.IStmTransaction>;

namespace STM
{

    public class LoggingStmTransaction : IStmTransaction
    {

        private StmTransaction baseTransaction;
        private static object LogLocker = new object();

        public LoggingStmTransaction(StmTransaction stmTransaction)
        {
            baseTransaction = stmTransaction;
        }

        public string Name
        {
            get
            {
                return baseTransaction.Name;
            }
        }

        public int Number
        {
            get
            {
                return baseTransaction.Number;
            }
        }

        public int Imbrication
        {
            get
            {
                return baseTransaction.Imbrication;
            }
        }

        public TransactionalAction Action
        {
            get
            {
                return baseTransaction.Action;
            }
        }

        public I_STM_TRANSACTION_STATE State
        {
            get
            {
                return baseTransaction.State;
            }
        }

        public IStmTransaction ParentTransaction
        {
            get
            {
                return baseTransaction.ParentTransaction;
            }
        }

        public void Begin()
        {
            LogAction(Name + " start");
            baseTransaction.Action.Invoke(this);
        }

        private void LogAction(string action)
        {
            lock(LogLocker)
            {
                using (StreamWriter streamWriter = new StreamWriter("Log.txt", true))
                {
                    streamWriter.WriteLine(action);
                }
            }
        }

        public void Rollback()
        {
            LogAction(baseTransaction.Name + " Rollback");
            baseTransaction.Rollback();
        }

        public bool TryCommit()
        {
            lock(Stm.commitLock)
            {
                LogAction(baseTransaction.Name + " TryCommit");
                bool commitResult = baseTransaction.TryCommit();
                LogAction(baseTransaction.Name + " " + baseTransaction.State);
                return commitResult;
            }
        }

        public T Get<T>(StmMemory<T> memoryRef, MemoryTuple<T> memoryTuple = null) where T : struct
        {
            if(memoryTuple == null)
            {
                memoryTuple = MemoryTuple<T>.Get(memoryRef.Value, memoryRef.Version);
            }
            LogAction(baseTransaction.Name + " get value from " + memoryRef.ToString() + " version = " + memoryTuple.version[Imbrication]);
            return baseTransaction.Get(memoryRef, memoryTuple);
        }

        public void Set<T>(StmMemory<T> memoryRef, object value, MemoryTuple<T> memoryTuple = null) where T : struct
        {
            if(memoryTuple == null)
            {
                memoryTuple = MemoryTuple<T>.Get((T)value, memoryRef.Version);
            }
            LogAction(baseTransaction.Name + " set value to " + memoryRef.ToString() + " version = " + memoryTuple.version[Imbrication]);
            baseTransaction.Set(memoryRef, value, memoryTuple);
        }

        public void SetParentTransaction(IStmTransaction parentTransaction)
        {
            baseTransaction.SetParentTransaction(parentTransaction);
        }

        public void AddSubTransaction(IStmTransaction subTransaction)
        {
            baseTransaction.AddSubTransaction(subTransaction);
        }

    }

}