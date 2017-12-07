using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LensTests
{
    public class DebugExException : Exception
    {
        public DebugExException(string message) : base(message)
        {
        }
    }
}
