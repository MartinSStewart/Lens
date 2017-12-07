using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lens
{
    /// <summary>
    /// A custom Debug class that breaks on asserts rather than show an error message. 
    /// This is useful since it doesn't block unit tests.
    /// </summary>
    /// <remarks>
    /// Original code found on
    /// https://github.com/MartinSStewart/Aventyr-Project/blob/develop/Source/Common/Common/DebugEx.cs
    /// </remarks>
    public static class DebugEx
    {
        public delegate void FailDelegate(string message);
        public static event FailDelegate FailEvent;

        [DebuggerStepThrough]
        public static void Assert(bool condition, string message = "")
        {
            if (!condition)
            {
                Fail(message);
            }
        }

        [DebuggerStepThrough]
        public static void Fail(string message = "")
        {
            FailEvent?.Invoke(message);
            Debugger.Break();
        }
    }
}
