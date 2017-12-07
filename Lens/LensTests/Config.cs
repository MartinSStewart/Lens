using Lens;
using LensTests;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LensTests
{
    [SetUpFixture]
    public class Config
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            // If a DebugEx.Assert statement fails then we want the unit test to also fail.
            DebugEx.FailEvent += message => throw new DebugExException(message);
        }
    }
}
