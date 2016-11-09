namespace STM
{

    public class StmMemory<T> where T : struct
    {

        MemoryTuple<T> memoryTyple;

        public T Value
        {
            get
            {
                return memoryTyple.value;
            }
            set
            {
                memoryTyple.value = value;
            }
        }

        public int[] Version
        {
            get
            {
                return memoryTyple.version;
            }
            set
            {
                memoryTyple.version = value;
            }
        }

        public StmMemory() : this(default(T))
        {
            
        }

        public StmMemory(T value)
        {
            memoryTyple = MemoryTuple<T>.Get(value, new int[100]);
        }

        public T Get(IStmTransaction transaction)
        {
            return transaction.Get(this);
        }

        public void Set(T value, IStmTransaction transaction)
        {
            transaction.Set(this, value);
        }

    }

}