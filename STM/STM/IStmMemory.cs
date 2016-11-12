using System;

namespace STM
{

    public interface IStmMemory
    {
        //Methods
        object Get(ITransaction context);
        void   Set(ITransaction context, object value);
        Type   GetMemoryType();
    }

}