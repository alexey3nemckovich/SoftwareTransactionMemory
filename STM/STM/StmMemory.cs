using System;

namespace STM
{

    public class StmMemory<T> : IStmMemory, Transaction.IInnerTransactionStmMemory where T : struct
    {

        private T value;
        private int[] version;

        public int[] Version
        {
            get
            {
                return version;
            }
        }

        public StmMemory() : this(default(T))
        {
            
        }

        public StmMemory(T value)
        {
            this.value = value;
            version = new int[100];
        }

        public Type GetMemoryType()
        {
            return typeof(T);
        }

        public object GetValue()
        {
            return value;
        }

        public void SetValue(object newValue)
        {
            CheckSettingValue(newValue);
            value = (T)newValue;
        }

        public int GetVersionForImbrication(int imbrication)
        {
            return version[imbrication];
        }

        public void SetVersionForImbrication(int imbrication, int imbrVersion)
        {
            version[imbrication] = imbrVersion;
        }

        public object Get(ITransaction context)
        {
            return context.Get(this);
        }

        public void Set(ITransaction context, object value)
        {
            CheckSettingValue(value);
            context.Set(this, value);
        }

        private void CheckSettingValue(object value)
        {
            if (!value.GetType().Equals(typeof(T)))
            {
                throw new ArgumentException("Value should be of type " + typeof(T).ToString() + "!");
            }
        }

    }

}