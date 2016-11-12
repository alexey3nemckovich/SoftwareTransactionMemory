using System.Collections.Generic;
using System.Text;

namespace STM
{

    public abstract class Transaction : ITransaction
    {

        public interface IInnerTransactionStmMemory : IStmMemory
        {
            //Properties
            int[] Version { get; }
            //Methods
            void   SetValue(object value);
            object GetValue();
            void   SetVersionForImbrication(int imbrication, int imbrVersion);
            int    GetVersionForImbrication(int imbrication);
        }

        //transaction properties values
        protected I_TRANSACTION_STATE state;
        protected string name;
        protected int number;
        protected int imbrication;
        private static int transactionNumber = 0;
       
        //refs to parent/sub transactions
        protected ITransaction parentTransaction;
        protected List<ITransaction> subTransactions;

        //lockers
        protected object subTransactionsLocker = new object();
        protected static object incrementLocker = new object();

        public Transaction()
        {
            parentTransaction = null;
            subTransactions = new List<ITransaction>();
            imbrication = 0;
            lock (incrementLocker)
            {
                transactionNumber++;
                number = transactionNumber;
            }
            InitName();
        }

        private void InitName()
        {
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < imbrication; i++)
            {
                stringBuilder.Append('\t');
            }
            stringBuilder.Append("TRANSACTION ");
            stringBuilder.Append(number);
            if (parentTransaction != null)
            {
                stringBuilder.Append(string.Format("(parent - {0})", parentTransaction.Number));
            }
            name = stringBuilder.ToString();
        }

        public I_TRANSACTION_STATE State
        {
            get
            {
                return state;
            }
            protected set
            {
                state = value;
            }
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

        public ITransaction ParentTransaction
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

        public List<ITransaction> SubTransactions
        {
            get
            {
                return subTransactions;
            }
        }

        public virtual void AddSubTransaction(ITransaction child)
        {
            lock (subTransactionsLocker)
            {
                subTransactions.Add(child);
            }
        }

        public void SetParentTransaction(ITransaction parentTransactionToSet)
        {
            if (parentTransaction == null)
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

        public abstract void Begin();

        public abstract bool TryCommit();

        public abstract void Rollback();

        public abstract object Get(IStmMemory memoryRef);

        public abstract void Set(IStmMemory memoryRef, object value, MemoryTuple memoryTuple = null);

    }

}