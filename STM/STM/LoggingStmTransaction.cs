using System.IO;
using System.Threading;
using System.Collections.Generic;
using TransactionalAction = System.Action<STM.IStmTransaction>;

namespace STM
{

    public class LoggingStmTransaction : Transaction, ITransaction
    {

        private IStmTransaction baseTransaction;
        private static object LogLocker = new object();

        public LoggingStmTransaction(IStmTransaction stmTransaction)
        {
            baseTransaction = stmTransaction;
        }

        public TransactionalAction Action
        {
            get
            {
                return baseTransaction.Action;
            }
        }

        public override void Begin()
        {
            LogAction(Name + " start");
            //baseTransaction.State = I_STM_TRANSACTION_STATE.ON_EXECUTE;
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

        public T Get<T>(StmMemory<T> memoryRef) where T : struct
        {
            T value = baseTransaction.Get(memoryRef);
            LogAction(baseTransaction.Name + " get value from " + memoryRef.ToString() + " value = " + value.ToString());
            return value;
        }

        public void Set<T>(StmMemory<T> memoryRef, object value, MemoryTuple<T> memoryTuple = null) where T : struct
        {
            if(memoryTuple == null)
            {
                memoryTuple = MemoryTuple<T>.Get((T)value, memoryRef.Version[Imbrication]);
            }
            baseTransaction.Set(memoryRef, value, memoryTuple);
            string log = baseTransaction.Name + " set value to " + memoryRef.ToString() + " value = " + memoryTuple.value;
            if (baseTransaction.ParentTransaction != null)
            {
                log += " parent value = " + baseTransaction.ParentTransaction.GetMemoryTupleValue<T>(memoryRef).value;
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

    }

}