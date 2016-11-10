using System.IO;
using System.Threading;
using System.Collections.Generic;
using TransactionalAction = System.Action<STM.IStmTransaction>;

namespace STM
{

    public class LoggingStmTransaction : IStmTransaction
    {

        private IStmTransaction baseTransaction;
        private static object LogLocker = new object();

        public LoggingStmTransaction(IStmTransaction stmTransaction)
        {
            baseTransaction = stmTransaction;
        }

        public object SubTransactionsLocker
        {
            get
            {
                return baseTransaction.SubTransactionsLocker;
            }
        }

        public int CountSubtransactions
        {
            get
            {
                return baseTransaction.CountSubtransactions;
            }
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
            set
            {
                baseTransaction.State = value;
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
            baseTransaction.State = I_STM_TRANSACTION_STATE.ON_EXECUTE;
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

        public void SetMemoryVersion(object stmRef)
        {
            baseTransaction.SetMemoryVersion(stmRef);
        }

        List<IStmTransaction> Subtransactions
        {
            get
            {
                return baseTransaction.SubTransactions;
            }
        }

        public void FixMemoryVersion<T>(StmMemory<T> memoryRef, MemoryTuple<T> memoryTuple) where T : struct
        {
            baseTransaction.FixMemoryVersion(memoryRef, memoryTuple);
        }

        public bool ValidateMemoryVersion(object memoryRef)
        {
            return baseTransaction.ValidateMemoryVersion(memoryRef);
        }

        public void Rollback()
        {
            LogAction(baseTransaction.Name + " Rollback");
            baseTransaction.Rollback();
        }

        public bool TryCommit()
        {
            Monitor.Enter(Stm.commitLock[Imbrication]);
            try
            {
                LogAction(baseTransaction.Name + " TryCommit");
                bool commitResult = baseTransaction.TryCommit();
                LogAction(baseTransaction.Name + " " + baseTransaction.State);
                return commitResult;
            }
            finally
            {
                Monitor.Exit(Stm.commitLock[Imbrication]);
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
            baseTransaction.Set(memoryRef, value, memoryTuple);
            string log = baseTransaction.Name + " set value to " + memoryRef.ToString() + " version = " + memoryTuple.version[Imbrication];
            if (baseTransaction.ParentTransaction != null)
            {
                log += " parent version = " + baseTransaction.ParentTransaction.GetMemoryStartVersions()[memoryRef];
            }
            LogAction(log);
        }

        public void SetParentTransaction(IStmTransaction parentTransaction)
        {
            baseTransaction.SetParentTransaction(parentTransaction);
        }

        public void AddSubTransaction(IStmTransaction subTransaction)
        {
            baseTransaction.AddSubTransaction(subTransaction);
        }

        public List<IStmTransaction> SubTransactions
        {
            get
            {
                return baseTransaction.SubTransactions;
            }
        }

        public Dictionary<object, int> GetMemoryStartVersions()
        {
            return baseTransaction.GetMemoryStartVersions();
        }

    }

}