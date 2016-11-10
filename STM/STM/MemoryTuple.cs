namespace STM
{

    public sealed class MemoryTuple<T>
    {

        public T value;
        public volatile int[] version;

        public MemoryTuple(T value, int[] version)
        {
            this.value = value;
            this.version = version;
        }

        public static MemoryTuple<T> Get(T value, int[] version)
        {
            return new MemoryTuple<T>(value, (int[])version.Clone());
        }

    }

}