using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeDucky
{
    public static class Perf
    {
        public static void Test(Action action, TimeSpan? maxTime = null)
        {
            action();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var timeout = maxTime ?? TimeSpan.FromSeconds(3);
            var iters = 0;
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                action();
                ++iters;
            }

            var tot = sw.Elapsed;
            Console.WriteLine("{0}ms per iteration ({1} iterations)", sw.Elapsed.TotalMilliseconds / iters, iters);
        }
    }
}
