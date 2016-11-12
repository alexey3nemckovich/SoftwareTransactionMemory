namespace STM
{

    public sealed class MemoryTuple
    {

        public object value;
        public int[] version;

        private MemoryTuple(object value, int[] version)
        {
            this.value = value;
            this.version = version;
        }

        public static MemoryTuple Get(object value, int[] version)
        {
            return new MemoryTuple(value, version);
        }

    }

}