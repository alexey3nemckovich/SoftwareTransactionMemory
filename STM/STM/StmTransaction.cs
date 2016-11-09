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
        private object childsLocker = new object();
        private static object incrementLocker = new object();

        public StmTransaction(TransactionalAction action)
        {
            state = I_STM_TRANSACTION_STATE.ON_EXECUTE;
            this.action = action;
            parentTransaction = null;
            subTransactions = null;
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
            Stack<int> transactionNumbers = new Stack<int>();
            transactionNumbers.Push(number);
            IStmTransaction parentTransaction = ParentTransaction;
            while(parentTransaction != null)
            {
                transactionNumbers.Push(parentTransaction.Number);
            }
            while(transactionNumbers.Count != 0)
            {
                stringBuilder.Append(transactionNumbers.Pop());
                if (transactionNumbers.Count != 0)
                {
                    stringBuilder.Append('.');
                }
            }
            name = stringBuilder.ToString();
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
        }

        public IStmTransaction ParentTransaction
        {
            get
            {
                return parentTransaction;
            }
        }

        private StmTransaction ParentStmTransaction
        {
            get
            {
                return (StmTransaction)parentTransaction;
            }
        }

        public int Number
        {
            get
            {
                return number;
            }            
        }

        public void AddSubTransaction(IStmTransaction child)
        {
            lock(childsLocker)
            {
                if (subTransactions == null)
                {
                    subTransactions = new List<IStmTransaction>();
                }
                subTransactions.Add(child);
            }
        }
        
        public void SetParentTransaction(IStmTransaction parentTransaction)
        {
            this.parentTransaction = parentTransaction;
            StmTransaction parentStmTransaction = ParentStmTransaction;
            imbrication = parentStmTransaction.Imbrication + 1;
            lock (parentStmTransaction.childsLocker)
            {
                if (parentStmTransaction.subTransactions != null)
                {
                    number = parentStmTransaction.subTransactions[parentStmTransaction.subTransactions.Count - 1].Number + 1;
                }
                else
                {
                    number = 0;
                }
                this.parentTransaction.AddSubTransaction(this);
            }
            InitName();
        }

        public void Begin()
        {
            action.Invoke(this);
        }

        private void SetMemoryVersion(object stmRef)
        {
            int[] memoryVersion = (int[])stmRef.GetType().GetProperty("Version").GetValue(stmRef);
            memoryVersion[imbrication] = number;
            if (parentTransaction != null)
            {
                StmTransaction parentStmTransaction = ParentStmTransaction;
                parentStmTransaction.SetMemoryVersion(stmRef);
            }
        }

        private void FixMemoryVersion<T>(StmMemory<T> memoryRef, MemoryTuple<T> memoryTuple) where T : struct
        {
            memoryStartVersions.Add(memoryRef, memoryTuple.version[imbrication]);
            if (parentTransaction != null)
            {
                StmTransaction parentStmTransaction = ParentStmTransaction;
                parentStmTransaction.FixMemoryVersion(memoryRef, memoryTuple);
            }
        }

        private bool ValidateMemoryVersion(object memoryRef)
        {
            int[] memoryVersion = (int[])memoryRef.GetType().GetProperty("Version").GetValue(memoryRef);
            if (memoryVersion[imbrication] != memoryStartVersions[memoryRef])
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private bool IsMemoryVersionCorrect()
        {
            foreach (object memoryRef in memoryChanges.Keys)
            {
                if (!ValidateMemoryVersion(memoryRef))
                {
                    state = I_STM_TRANSACTION_STATE.CONFLICT;
                    return false;
                }
                else
                {
                    if (parentTransaction != null)
                    {
                        if (!ParentStmTransaction.ValidateMemoryVersion(memoryRef))
                        {
                            state = I_STM_TRANSACTION_STATE.PARENT_CONFLICT;
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        public virtual bool TryCommit()
        {
            Monitor.Enter(Stm.commitLock);
            try
            {
                if(!IsMemoryVersionCorrect())
                {
                    return false;
                }
                //if (childTransactions.Count != 0)
                //{
                //    while (!AllChildTransactionsCommited())
                //    {
                //        Monitor.Wait(Stm.commitLock);
                //    }
                //}
                foreach (object memoryRef in memoryRefsToUpdate)
                {
                    SetMemoryVersion(memoryRef);
                    memoryRef.GetType().GetMethod("set_Value").Invoke(memoryRef, new object[] { memoryChanges[memoryRef] });
                }
                state = I_STM_TRANSACTION_STATE.COMMITED;
                //Monitor.Pulse(Stm.commitLock);
                return true;
            }
            finally
            {
                Monitor.Exit(Stm.commitLock);
            }
        }

        //private bool AllChildTransactionsCommited()
        //{
        //    foreach(IStmTransaction childTransaction in childTransactions)
        //    {
        //        if(childTransaction.State != I_STM_TRANSACTION_STATE.COMMITED)
        //        {
        //            return false;
        //        }
        //    }
        //    return true;
        //}

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
        
        public virtual void Rollback()
        {
            memoryRefsToUpdate.Clear();
            memoryChanges.Clear();
            memoryStartVersions.Clear();
            //if(childTransactions != null)
            //{
            //    foreach(StmTransaction childTransaction in childTransactions)
            //    {
            //        childTransaction.Rollback();
            //    }
            //}
            //if(parentTransaction != null)
            //{
            //    parentTransaction.childTransactions.Remove(this);
            //    foreach(object stmRef in stmRefsTransactionStartVersions)
            //    {
            //        parentTransaction.stmRefsTransactionStartVersions.Remove(stmRef);
            //    }
            //}
        }

    }

}