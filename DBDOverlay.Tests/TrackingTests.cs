using System;
using System.Threading;
using System.Threading.Tasks;
using DBDOverlay.Core.BackgroundProcesses;
using DBDOverlay.Core.WindowControllers.KillerOverlay;
using Xunit;

namespace DBDOverlay.Tests
{
    public class TrackingTests
    {
        [Theory]
        [InlineData(false, false, false, false)]
        [InlineData(true, false, false, true)]
        [InlineData(false, true, false, true)]
        [InlineData(false, false, true, true)]
        [InlineData(true, true, false, true)]
        [InlineData(true, false, true, true)]
        [InlineData(false, true, true, true)]
        [InlineData(true, true, true, true)]
        public void WorkerStaysEnabledUntilEveryFeatureIsOff(bool hooks, bool timer, bool panel, bool expected)
            => Assert.Equal(expected, KillerMode.ShouldRun(hooks, timer, panel));

        [Fact]
        public async Task CancelRunningTimerPreventsFurtherCallbacks()
        {
            using (var timer = new UnhookTimer())
            {
                int calls = 0;
                var task = timer.RunAsync(1000, (g, t) => calls++, 0);
                Assert.True(calls > 0);
                timer.Cancel();
                int atCancel = calls;
                await task;
                Assert.Equal(atCancel, calls);
            }
        }
        private static HookTransition Confirm(HookTracker tracker, HudObservation observation)
        {
            tracker.Observe(observation); tracker.Observe(observation); return tracker.Observe(observation);
        }
        [Fact]
        public void HookIsCountedOnceAndUnhookRequiresPositiveEvidence()
        {
            var t = new HookTracker();
            Assert.Equal(HookTransition.Hooked, Confirm(t, HudObservation.Hooked));
            Assert.Equal(HookTransition.None, Confirm(t, HudObservation.Hooked));
            Assert.Equal(1, t.Hooks);
            for (int i = 0; i < 10; i++) Assert.Equal(HookTransition.None, t.Observe(HudObservation.Unknown));
            Assert.Equal(HudObservation.Hooked, t.State);
            Assert.Equal(HookTransition.Unhooked, Confirm(t, HudObservation.Unhooked));
            Assert.Equal(HookTransition.Hooked, Confirm(t, HudObservation.Hooked));
            Assert.Equal(2, t.Hooks);
        }
        [Fact]
        public void UnknownBreaksConfirmationAndTerminalNeverStartsUnhookTimer()
        {
            var t = new HookTracker();
            t.Observe(HudObservation.Hooked); t.Observe(HudObservation.Hooked); t.Observe(HudObservation.Unknown);
            Assert.Equal(HookTransition.None, t.Observe(HudObservation.Hooked));
            Confirm(t, HudObservation.Hooked);
            Assert.Equal(HookTransition.Terminal, Confirm(t, HudObservation.Terminal));
            Assert.Equal(HookTransition.None, Confirm(t, HudObservation.Unhooked));
            Assert.Equal(HookTransition.None, Confirm(t, HudObservation.Hooked));
            Assert.Equal(1, t.Hooks);
        }
        [Fact]
        public void InitiallyUnhookedDoesNotStartTimer() => Assert.Equal(HookTransition.None, Confirm(new HookTracker(), HudObservation.Unhooked));

        [Fact]
        public async Task CancelDuringAnimationPreventsLateTimerUpdates()
        {
            using (var timer = new UnhookTimer())
            {
                int calls = 0;
                var task = timer.RunAsync(1000, (g, t) => Interlocked.Increment(ref calls), 100);
                timer.Cancel();
                await task;
                Assert.Equal(0, calls);
            }
        }
        [Fact]
        public async Task RestartInvalidatesPreviousTimerGeneration()
        {
            using (var timer = new UnhookTimer())
            {
                int oldCalls = 0, newCalls = 0;
                var old = timer.RunAsync(500, (g, t) => oldCalls++, 100);
                int oldGeneration = timer.Generation;
                var current = timer.RunAsync(1, (g, t) => newCalls++, 0);
                await Task.WhenAll(old, current);
                Assert.Equal(0, oldCalls); Assert.True(newCalls > 0); Assert.True(timer.Generation > oldGeneration);
            }
        }
        private sealed class Worker : BaseBackgroundProcess
        {
            public readonly AutoResetEvent Entered = new AutoResetEvent(false);
            public readonly AutoResetEvent Release = new AutoResetEvent(false);
            public int Inside, Maximum;
            protected override void Action()
            {
                int inside = Interlocked.Increment(ref Inside);
                Maximum = Math.Max(Maximum, inside); Entered.Set();
                Release.WaitOne(2000);
                Interlocked.Decrement(ref Inside);
            }
        }
        [Fact]
        public void RapidStopStartNeverCreatesConcurrentLoops()
        {
            var w = new Worker();
            try
            {
                w.Run(); Assert.True(w.Entered.WaitOne(2000));
                for (int i = 0; i < 10; i++) { w.Stop(); w.Run(); }
                w.Release.Set(); Assert.True(w.Entered.WaitOne(2000));
                Assert.Equal(1, w.Maximum);
                w.Stop(); w.Release.Set();
                Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref w.Inside) == 0, 2000));
                w.Run(); Assert.True(w.Entered.WaitOne(2000));
            }
            finally { w.Stop(); w.Release.Set(); }
        }
    }
}
