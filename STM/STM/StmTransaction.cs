using System.Threading;
using System.Collections.Generic;
using TransactionalAction = System.Action<STM.IStmTransaction>;

namespace STM
{
    
    public class StmTransaction : Transaction, IStmTransaction
    {

        //transaction properties values
        private TransactionalAction action;
   
        //memoryRefs, witch appear or have changes in transaction
        private HashSet<IStmMemory> memoryRefsToUpdate;
        private Dictionary<IStmMemory, object> memoryChanges;
        private Dictionary<IStmMemory, int> memoryStartVersions;

        public StmTransaction(TransactionalAction action) : base()
        {
            this.action = action;
            memoryRefsToUpdate = new HashSet<IStmMemory>();
            memoryChanges = new Dictionary<IStmMemory, object>();
            memoryStartVersions = new Dictionary<IStmMemory, int>();
        }

        //PROPERTIES
        public TransactionalAction Action
        {
            get
            {
                return action;
            }
        }

        public new IStmTransaction ParentTransaction
        {
            get
            {
                return (IStmTransaction)base.ParentTransaction;
            }
        }

        public new List<IStmTransaction> SubTransactions
        {
            get
            {
                List<IStmTransaction> stmSubTransactions = new List<IStmTransaction>();
                List<ITransaction> subTransactions = base.subTransactions;
                foreach(ITransaction subTransaction in subTransactions)
                {
                    stmSubTransactions.Add((IStmTransaction)subTransaction);
                }
                return stmSubTransactions;
            }
        }

        public override void Begin()
        {
            state = I_TRANSACTION_STATE.ON_EXECUTE;
            action.Invoke(this);
            state = I_TRANSACTION_STATE.GOING_TO_COMMIT;
        }

        public override bool TryCommit()
        {
            Monitor.Enter(Stm.commitLock[Imbrication]);
            try
            {
                if (!IsMemoryVersionCorrect())
                {
                    return false;
                }
                foreach (object memoryRef in memoryRefsToUpdate)
                {
                    UpdateMemory(memoryRef);
                }
                state = I_TRANSACTION_STATE.COMMITED;
                return true;
            }
            finally
            {
                Monitor.Exit(Stm.commitLock[Imbrication]);
            }
        }

        public void Set(IStmMemory memoryRef, object value, MemoryTuple memoryTuple = null)//ok
        {
            if (memoryTuple == null)
            {
                memoryTuple = MemoryTuple.Get(value, memoryRef.Version);
            }
            FixMemoryChange(memoryRef, memoryTuple);
            if (!memoryRefsToUpdate.Contains(memoryRef))
            {
                memoryRefsToUpdate.Add(memoryRef);
            }
        }

        public T Get<T>(StmMemory<T> memoryRef)
        {
            ImbricationMemoryTuple<T> memoryTuple = GetCurrentImbricationMemoryTuple<T>(memoryRef);
            if (!memoryChanges.ContainsKey(memoryRef))
            {
                FixMemoryChange(memoryRef, memoryTuple);
            }
            return memoryTuple.value;
        }

        public override void Rollback()
        {
            if (state == I_TRANSACTION_STATE.PARENT_CONFLICT)
            {
                Monitor.Enter(parentTransaction.SubTransactionsLocker);
                try
                {
                    if (parentTransaction.State != I_TRANSACTION_STATE.ROLLBACKED_BY_SUBTRANSACTION)
                    {
                        ParentTransaction.SetMemoryVersionsToCurrent();
                    }
                }
                finally
                {
                    Monitor.Exit(parentTransaction.SubTransactionsLocker);
                }
            }
            if (state == I_TRANSACTION_STATE.COMMITED)
            {
                //
                //set memory to previous version
                //
            }
            memoryRefsToUpdate.Clear();
            memoryChanges.Clear();
            memoryStartVersions.Clear();
        }

        public void SetMemoryVersionsToCurrent()
        {
            int countMemoryRefVersioned = memoryStartVersions.Count;
            object[] memoryRefs = new object[countMemoryRefVersioned];
            memoryStartVersions.Keys.CopyTo(memoryRefs, 0);
            for (int i = 0; i < countMemoryRefVersioned; i++)
            {
                object memoryRef = memoryRefs[i];
                memoryStartVersions[memoryRef] = GetCurrentImbricationMemoryVersion(memoryRef);
            }
            state = I_TRANSACTION_STATE.ROLLBACKED_BY_SUBTRANSACTION;
        }

        private bool IsMemoryVersionCorrect()//ok
        {
            foreach (object memoryRef in memoryChanges.Keys)
            {
                if (!ValidateMemoryVersion(memoryRef))
                {
                    return false;
                }
            }
            return true;
        }

        public bool ValidateMemoryVersion(object memoryRef)//ok
        {
            if (parentTransaction != null)
            {
                if (!ParentTransaction.ValidateMemoryVersion(memoryRef))
                {
                    state = I_TRANSACTION_STATE.PARENT_CONFLICT;
                    return false;
                }
            }
            int[] memoryVersion = (int[])memoryRef.GetType().GetProperty("Version").GetValue(memoryRef);
            if (memoryVersion[Imbrication] != memoryStartVersions[memoryRef])
            {
                state = I_TRANSACTION_STATE.CONFLICT;
                return false;
            }
            return true;
        }

        public void FixMemoryVersion<T>(StmMemory<T> memoryRef, MemoryTuple<T> memoryTuple) where T : struct//ok
        {
            lock (subTransactionsLocker)
            {
                if (!memoryStartVersions.ContainsKey(memoryRef))
                {
                    memoryStartVersions.Add(memoryRef, memoryTuple.version[imbrication]);
                    if (parentTransaction != null)
                    {
                        ParentTransaction.FixMemoryVersion(memoryRef, memoryTuple);
                    }
                }
            }
        }

        private void FixMemoryChange<T>(StmMemory<T> memoryRef, MemoryTuple<T> memoryTyple) where T : struct//ok
        {
            if (memoryChanges.ContainsKey(memoryRef))
            {
                memoryChanges[memoryRef] = memoryTyple.value;
            }
            else
            {
                memoryChanges.Add(memoryRef, memoryTyple.value);
                FixMemoryVersion(memoryRef, memoryTyple);
            }
        }

        private void FixMemoryChange<T>(StmMemory<T> memoryRef, ImbricationMemoryTuple<T> transactionMemoryTyple) where T : struct//ok
        {
            if (memoryChanges.ContainsKey(memoryRef))
            {
                memoryChanges[memoryRef] = transactionMemoryTyple.value;
            }
            else
            {
                memoryChanges.Add(memoryRef, transactionMemoryTyple.value);
                MemoryTuple<T> memoryTuple = MemoryTuple<T>.Get(transactionMemoryTyple.value, memoryRef.Version);
                FixMemoryVersion(memoryRef, memoryTuple);
            }
        }

        private void UpdateMemory(object memoryRef)//ok
        {
            int[] memoryVersion = (int[])memoryRef.GetType().GetProperty("Version").GetValue(memoryRef);
            memoryVersion[Imbrication] = number;
            memoryRef.GetType().GetProperty("Value").SetValue(memoryRef, memoryChanges[memoryRef]);
        }

        public ImbricationMemoryTuple<T> GetCurrentImbricationMemoryTuple<T>(object memoryRef) where T : struct
        {
            if (memoryChanges.ContainsKey(memoryRef))
            {
                return ImbricationMemoryTuple<T>.Get((T)memoryChanges[memoryRef], memoryStartVersions[memoryRef]);
            }
            else
            {
                if (parentTransaction != null)
                {
                    return ParentTransaction.GetCurrentImbricationMemoryTuple<T>(memoryRef);
                }
                else
                {
                    StmMemory<T> stmMemory = (StmMemory<T>)memoryRef;
                    return ImbricationMemoryTuple<T>.Get(stmMemory.Value, stmMemory.GetVersionForImbrication(imbrication));
                }
            }
        }

        private int GetCurrentImbricationMemoryVersion (object memoryRef)//ok
        {
            return (int)(memoryRef.GetType().GetMethod("GetVersionForImbrication").Invoke(memoryRef, new object[] { imbrication }));
            // GetProperty("Version").GetValue(memoryRef))[imbrication];
        }

    }

}