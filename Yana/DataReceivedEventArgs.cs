using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yana
{
    public class DataReceivedEventArgs : EventArgs
    {
        public String DataCount { get; private set; }
        public DataReceivedEventArgs(String dataCount)
        {
            DataCount = dataCount;
        }
    }
}
