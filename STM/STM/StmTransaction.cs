using System;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using TransactionalAction = System.Action<STM.IStmTransaction>;

namespace STM
{
    
    public class StmTransaction : IStmTransaction
    {

        //transaction properties
        private string name;
        private TransactionalAction action;
        private I_STM_TRANSACTION_STATE state;
        private int number;
        private int imbrication;
        private static int transactionNumber = 0;

        //refs to stmRefts, that have changes in transaction
        private HashSet<object> memoryRefsToUpdate;
        private Dictionary<object, object> memoryChanges;
        private Dictionary<object, int> memoryStartVersions;

        //refs to parent/sub transactions
        private IStmTransaction parentTransaction;
        private List<IStmTransaction> subTransactions;

        //lockers
        private object subTransactionsLocker = new object();
        private static object incrementLocker = new object();

        public StmTransaction(TransactionalAction action)
        {
            this.action = action;
            parentTransaction = null;
            subTransactions = new List<IStmTransaction>();
            memoryRefsToUpdate = new HashSet<object>();
            memoryChanges = new Dictionary<object, object>();
            memoryStartVersions = new Dictionary<object, int>();
            imbrication = 0;
            lock(incrementLocker)
            {
                transactionNumber++;
                number = transactionNumber;
            }
            InitName();
        }
        
        private void InitName()
        {
            StringBuilder stringBuilder = new StringBuilder();
            for(int i = 0; i < imbrication; i++)
            {
                stringBuilder.Append('\t');
            }
            stringBuilder.Append("TRANSACTION ");
            stringBuilder.Append(number);
            if(parentTransaction != null)
            {
                stringBuilder.Append(string.Format("(parent - {0})", parentTransaction.Number));
            }
            name = stringBuilder.ToString();
        }

        public object SubTransactionsLocker
        {
            get
            {
                return subTransactionsLocker;
            }
        }

        public int CountSubtransactions
        {
            get
            {
                return subTransactions.Count;
            }
        }

        public string Name
        {
            get
            {
                return name;
            }
        }
        
        public int Imbrication
        {
            get
            {
                return imbrication;
            }
        }

        public TransactionalAction Action
        {
            get
            {
                return action;
            }
        }

        public I_STM_TRANSACTION_STATE State
        {
            get
            {
                return state;
            }
            set
            {
                state = value;
            }
        }

        public IStmTransaction ParentTransaction
        {
            get
            {
                return parentTransaction;
            }
        }

        public int Number
        {
            get
            {
                return number;
            }            
        }

        List<IStmTransaction> Subtransactions
        {
            get
            {
                return subTransactions;
            }
        }

        public void AddSubTransaction(IStmTransaction child)
        {
            lock(subTransactionsLocker)
            {
                subTransactions.Add(child);
            }
        }

        public void SetParentTransaction(IStmTransaction parentTransactionToSet)
        {
            if(parentTransaction == null)
            {
                parentTransaction = parentTransactionToSet;
                lock (parentTransaction.SubTransactionsLocker)
                {
                    number = parentTransaction.CountSubtransactions + 1;
                    parentTransaction.AddSubTransaction(this);
                }
                imbrication = parentTransaction.Imbrication + 1;
                InitName();
            }
        }

        public void Begin()
        {
            state = I_STM_TRANSACTION_STATE.ON_EXECUTE;
            action.Invoke(this);
            state = I_STM_TRANSACTION_STATE.READY_TO_TRY_TO_COMMIT;
        }

        public void SetMemoryVersion(object stmRef)
        {
            int[] memoryVersion = (int[])stmRef.GetType().GetProperty("Version").GetValue(stmRef);
            memoryVersion[imbrication] = number;
        }

        public void FixMemoryVersion<T>(StmMemory<T> memoryRef, MemoryTuple<T> memoryTuple) where T : struct
        {
            lock(subTransactionsLocker)
            {
                if (!memoryStartVersions.ContainsKey(memoryRef))
                {
                    memoryStartVersions.Add(memoryRef, memoryTuple.version[imbrication]);
                }
                if (parentTransaction != null)
                {
                    parentTransaction.FixMemoryVersion(memoryRef, memoryTuple);
                }
            }
        }

        public bool ValidateMemoryVersion(object memoryRef)
        {
            if (parentTransaction != null)
            {
                if (!parentTransaction.ValidateMemoryVersion(memoryRef))
                {
                    state = I_STM_TRANSACTION_STATE.PARENT_CONFLICT;
                    return false;
                }
            }
            int[] memoryVersion = (int[])memoryRef.GetType().GetProperty("Version").GetValue(memoryRef);
            if (memoryVersion[imbrication] != memoryStartVersions[memoryRef])
            {
                state = I_STM_TRANSACTION_STATE.CONFLICT;
                return false;
            }
            return true;
        }

        private bool IsMemoryVersionCorrect()
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

        public virtual bool TryCommit()
        {
            Monitor.Enter(Stm.commitLock[Imbrication]);
            try
            {
                ///Validate self
                if (!IsMemoryVersionCorrect())
                {
                    return false;
                }
                ///Update memory
                foreach (object memoryRef in memoryRefsToUpdate)
                {
                    SetMemoryVersion(memoryRef);
                    memoryRef.GetType().GetMethod("set_Value").Invoke(memoryRef, new object[] { memoryChanges[memoryRef] });
                }
                state = I_STM_TRANSACTION_STATE.COMMITED;
                return true;
            }
            finally
            {
                Monitor.Exit(Stm.commitLock[Imbrication]);
            }
        }

        public virtual void Set<T>(StmMemory<T> memoryRef, object value, MemoryTuple<T> memoryTuple = null) where T : struct
        {
            if (memoryTuple == null)
            {
                memoryTuple = MemoryTuple<T>.Get(memoryRef.Value, memoryRef.Version);
            }
            FixMemoryChange(memoryRef, memoryTuple);
            if(!memoryRefsToUpdate.Contains(memoryRef))
            {
                memoryRefsToUpdate.Add(memoryRef);
            }
        }

        public virtual T Get<T>(StmMemory<T> memoryRef, MemoryTuple<T> memoryTuple = null) where T : struct
        {
            if(memoryTuple == null)
            {
                memoryTuple = MemoryTuple<T>.Get(memoryRef.Value, memoryRef.Version);
            }
            if (!memoryChanges.ContainsKey(memoryRef))
            {
                FixMemoryChange(memoryRef, memoryTuple);
            }
            return (T)memoryChanges[memoryRef];
        }

        private void FixMemoryChange<T>(StmMemory<T> memoryRef, MemoryTuple<T> memoryTyple) where T : struct
        {
            memoryChanges.Add(memoryRef, memoryTyple.value);
            if (!memoryStartVersions.ContainsKey(memoryRef))
            {
                FixMemoryVersion(memoryRef, memoryTyple);
            }
        }

        public List<IStmTransaction> SubTransactions
        {
            get
            {
                return subTransactions;
            }
        }

        public Dictionary<object, int> GetMemoryStartVersions()
        {
            return memoryStartVersions;
        }

        public virtual void Rollback()
        {
            if (state == I_STM_TRANSACTION_STATE.PARENT_CONFLICT)
            {
                Monitor.Enter(parentTransaction.SubTransactionsLocker);
                try
                {
                    if (parentTransaction.State != I_STM_TRANSACTION_STATE.ROLLBACKED_BY_SUBTRANSACTION)
                    {
                        Dictionary<object, int> parentMemoryVersions = parentTransaction.GetMemoryStartVersions();
                        int countMemoryVersions = parentMemoryVersions.Count;
                        object[] memoryRefs = new object[countMemoryVersions];
                        parentMemoryVersions.Keys.CopyTo(memoryRefs, 0);
                        for (int i = 0; i < countMemoryVersions; i++)
                        {
                            object memoryRef = memoryRefs[i];
                            int[] memoryVersion = ((int[])memoryRef.GetType().GetProperty("Version").GetValue(memoryRef));
                            parentMemoryVersions[memoryRef] = memoryVersion[parentTransaction.Imbrication];
                        }
                        parentTransaction.State = I_STM_TRANSACTION_STATE.ROLLBACKED_BY_SUBTRANSACTION;
                    }
                }
                finally
                {
                    Monitor.Exit(parentTransaction.SubTransactionsLocker);
                }
            }
            if (state == I_STM_TRANSACTION_STATE.COMMITED)
            {
                //
                //set memory to previous version
                //
            }
            memoryRefsToUpdate.Clear();
            memoryChanges.Clear();
            memoryStartVersions.Clear();
        }

    }

}