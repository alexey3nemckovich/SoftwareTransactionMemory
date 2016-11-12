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
        private HashSet<IInnerTransactionStmMemory> memoryRefsToUpdate;
        private Dictionary<IInnerTransactionStmMemory, object> memoryChanges;
        private Dictionary<IInnerTransactionStmMemory, int> memoryStartVersions;

        public StmTransaction(TransactionalAction action) : base()
        {
            this.action = action;
            memoryRefsToUpdate = new HashSet<IInnerTransactionStmMemory>();
            memoryChanges = new Dictionary<IInnerTransactionStmMemory, object>();
            memoryStartVersions = new Dictionary<IInnerTransactionStmMemory, int>();
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
                foreach (IInnerTransactionStmMemory memoryRef in memoryRefsToUpdate)
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

        public override void Set(IStmMemory memoryRef, object value, int[] memoryVersion = null)
        {
            IInnerTransactionStmMemory innerMemoryRef = (IInnerTransactionStmMemory)memoryRef;
            MemoryTuple memoryTuple = GenerateMemoryTuple(innerMemoryRef, memoryVersion, value);
            FixMemoryChange(innerMemoryRef, memoryTuple);
            if (!memoryRefsToUpdate.Contains(innerMemoryRef))
            {
                memoryRefsToUpdate.Add(innerMemoryRef);
            }
        }

        public override object Get(IStmMemory memoryRef, int[] memoryVersion = null)
        {
            IInnerTransactionStmMemory innerMemoryRef = (IInnerTransactionStmMemory)memoryRef;
            MemoryTuple memoryTuple = GenerateMemoryTuple(innerMemoryRef, memoryVersion);
            if (!memoryChanges.ContainsKey(innerMemoryRef))
            {
                FixMemoryChange(innerMemoryRef, memoryTuple);
            }
            return memoryTuple.value;
        }

        private MemoryTuple GenerateMemoryTuple(IInnerTransactionStmMemory memoryRef, int[] memoryVersion, object memoryValue = null)
        {
            int[] currentMemVersion = null;
            if (memoryVersion == null)
            {
                currentMemVersion = memoryRef.Version;
            }
            if (memoryValue == null)
            {
                return MemoryTuple.Get(GetTransactionMemoryValue(memoryRef), currentMemVersion);
            }
            else
            {
                return MemoryTuple.Get(memoryValue, currentMemVersion);
            }
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
            IInnerTransactionStmMemory[] memoryRefs = new IInnerTransactionStmMemory[countMemoryRefVersioned];
            memoryStartVersions.Keys.CopyTo(memoryRefs, 0);
            for (int i = 0; i < countMemoryRefVersioned; i++)
            {
                IInnerTransactionStmMemory memoryRef = memoryRefs[i];
                memoryStartVersions[memoryRef] = memoryRef.GetVersionForImbrication(imbrication);
            }
            state = I_TRANSACTION_STATE.ROLLBACKED_BY_SUBTRANSACTION;
        }

        private bool IsMemoryVersionCorrect()
        {
            foreach (IInnerTransactionStmMemory memoryRef in memoryChanges.Keys)
            {
                if (!ValidateMemoryVersion(memoryRef))
                {
                    return false;
                }
            }
            return true;
        }

        public bool ValidateMemoryVersion(IInnerTransactionStmMemory memoryRef)//ok
        {
            if (parentTransaction != null)
            {
                if (!ParentTransaction.ValidateMemoryVersion(memoryRef))
                {
                    state = I_TRANSACTION_STATE.PARENT_CONFLICT;
                    return false;
                }
            }
            int currentImbricationMemoryVersion = memoryRef.GetVersionForImbrication(imbrication);
            if (currentImbricationMemoryVersion != memoryStartVersions[memoryRef])
            {
                state = I_TRANSACTION_STATE.CONFLICT;
                return false;
            }
            return true;
        }

        public void FixMemoryVersion(IInnerTransactionStmMemory memoryRef, MemoryTuple memoryTuple)
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

        private void FixMemoryChange(IInnerTransactionStmMemory memoryRef, MemoryTuple memoryTyple)
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

        private void UpdateMemory(IInnerTransactionStmMemory memoryRef)
        {
            memoryRef.SetVersionForImbrication(imbrication, number);
            memoryRef.SetValue(memoryChanges[memoryRef]);
        }

        public object GetTransactionMemoryValue(IInnerTransactionStmMemory memoryRef)
        {
            if (memoryChanges.ContainsKey(memoryRef))
            {
                return memoryChanges[memoryRef];
            }
            else
            {
                if (parentTransaction != null)
                {
                    return ParentTransaction.GetTransactionMemoryValue(memoryRef);
                }
                else
                {
                    return memoryRef.GetValue();
                }
            }
        }

    }

}