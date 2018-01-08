using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lens
{
    public class InvalidStateException : Exception
    {
        public InvalidStateException(IState invalidState)
            : base($"Instance of {invalidState.GetType().Name} was set to an invalid state.")
        {
        }
    }
}
