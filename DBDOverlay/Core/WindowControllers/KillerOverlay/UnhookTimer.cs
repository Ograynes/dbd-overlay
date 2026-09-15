using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DBDOverlay.Core.WindowControllers.KillerOverlay
{
    public sealed class UnhookTimer : IDisposable
    {
        private CancellationTokenSource cancellation;
        public int Generation { get; private set; }
        public void Cancel() { Generation++; cancellation?.Cancel(); cancellation = null; }
        public async Task RunAsync(int maximumMs, Action<int, long> update, int delayMs = 1500)
        {
            Cancel();
            var source = new CancellationTokenSource(); cancellation = source;
            int generation = Generation;
            try
            {
                await Task.Delay(delayMs, source.Token);
                var watch = Stopwatch.StartNew();
                while (watch.ElapsedMilliseconds < maximumMs)
                {
                    source.Token.ThrowIfCancellationRequested();
                    update(generation, watch.ElapsedMilliseconds);
                    await Task.Delay(100, source.Token);
                }
                source.Token.ThrowIfCancellationRequested();
                update(generation, -1);
            }
            catch (OperationCanceledException) { }
            finally { if (cancellation == source) cancellation = null; source.Dispose(); }
        }
        public void Dispose() => Cancel();
    }
}
