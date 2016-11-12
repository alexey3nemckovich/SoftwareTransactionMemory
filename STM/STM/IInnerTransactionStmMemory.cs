using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STM
{

    public interface IInnerTransactionStmMemory
    {
        //Properties
        int[] Version { get; }
        //Methods
        void SetValue(object value);
        object GetValue();
        void SetVersionForImbrication(int imbrication, int imbrVersion);
        int GetVersionForImbrication(int imbrication);
    }

}