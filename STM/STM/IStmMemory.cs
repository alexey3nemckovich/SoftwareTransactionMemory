using System;

namespace STM
{

    public interface IStmMemory
    {

        int[] Version { get; }
        //Methods
        void   SetValue(object value);
        object GetValue();
        void   SetVersionForImbrication(int imbrication, int imbrVersion);
        int    GetVersionForImbrication(int imbrication);
        object Get(ITransaction context);
        void   Set(ITransaction context, object value);
        Type   GetMemoryType();
    }

}