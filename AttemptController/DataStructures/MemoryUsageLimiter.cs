using System;
using System.Threading;
using System.Threading.Tasks;

namespace AttemptController.DataStructures
{
    public class MemoryUsageLimiter : IDisposable
    {
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public class ReduceMemoryUsageEventParameters : EventArgs
        {
            public readonly double FractionOfMemoryToTryToRemove;

            public ReduceMemoryUsageEventParameters(
                    double fractionOfMemoryToTryToRemove)
            {
                FractionOfMemoryToTryToRemove = fractionOfMemoryToTryToRemove;
            }
        }
        
    
        public event EventHandler<ReduceMemoryUsageEventParameters> OnReduceMemoryUsageEventHandler;

        private readonly double _fractionToRemoveOnCleanup;
        public MemoryUsageLimiter(
            double fractionToRemoveOnCleanup = 0.2   ,
            long hardMemoryLimit = 0)
        {
            _fractionToRemoveOnCleanup = fractionToRemoveOnCleanup;
            if (hardMemoryLimit == 0)
            {
                hardMemoryLimit = 1024L*1024L*1024L*2L;  
            }
                Task.Run(() => ThresholdReductionLoop(hardMemoryLimit, cancellationTokenSource.Token), cancellationTokenSource.Token);
        }
        

        public void ReduceMemoryUsage()
        {
            EventHandler<MemoryUsageLimiter.ReduceMemoryUsageEventParameters> localOnReduceMemoryUsageHandler = OnReduceMemoryUsageEventHandler;
            if (localOnReduceMemoryUsageHandler != null)
            {
                Parallel.ForEach(localOnReduceMemoryUsageHandler.GetInvocationList(),
                    d => {
                        try
                        {
                            d.DynamicInvoke(this, new MemoryUsageLimiter.ReduceMemoryUsageEventParameters(_fractionToRemoveOnCleanup));
                        }
                        catch (Exception)
                        {
                        }
                    }
                    );
            }
        }



        public void ThresholdReductionLoop(long hardMemoryLimit, CancellationToken cancellationToken)
        {
            while (true)
            {
                try
                {
                    System.Threading.Thread.Sleep(250);
                    cancellationToken.ThrowIfCancellationRequested();
                    long currentMemoryConsumptionInBytes = GC.GetTotalMemory(true);
                    if (currentMemoryConsumptionInBytes > hardMemoryLimit)
                    {
                        Console.Error.WriteLine("Starting memory reduction.");
                        ReduceMemoryUsage();
                        Console.Error.WriteLine("Completing memory reduction.");
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
        }
    }
}
