using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LensTests
{
    public static class Benchmark
    {
        /// <summary> 
        /// </summary> 
        /// <remarks>Original code found here: https://stackoverflow.com/a/1622491 </remarks> 
        public static TimeSpan Run(Action act, int iterations)
        {
            GC.Collect();
            act.Invoke(); // run once outside of loop to avoid initialization costs
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                act.Invoke();
            }
            sw.Stop();

            return TimeSpan.FromTicks(sw.Elapsed.Ticks / iterations);
        }
    }
}
