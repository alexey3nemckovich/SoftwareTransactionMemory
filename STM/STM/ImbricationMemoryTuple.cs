using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STM
{

    public class ImbricationMemoryTuple<T>
    {

        public T value;
        public int imbricationVersion;

        private ImbricationMemoryTuple(T value, int version)
        {
            this.value = value;
            imbricationVersion = version;
        }

        public static ImbricationMemoryTuple<T> Get(T value, int transactionVerion)
        {
            return new ImbricationMemoryTuple<T>(value, transactionVerion);
        }

    }

}