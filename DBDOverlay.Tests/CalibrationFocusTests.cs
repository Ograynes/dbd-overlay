using System.Threading.Tasks;
using DBDOverlay.Core.ImageProcessing;
using Xunit;

namespace DBDOverlay.Tests
{
    public class CalibrationFocusTests
    {
        [Fact]
        public async Task AllowsReturningToGameAfterSeveralSeconds()
        {
            int ticks = 0;
            Assert.True(await CalibrationFocusWait.WaitAsync(() => ticks >= 25, () => false,
                _ => { }, () => { ticks++; return Task.CompletedTask; }));
            Assert.Equal(25, ticks);
        }
        [Fact]
        public async Task TimesOutWithoutCapturingAnotherApplication()
        {
            int ticks = 0;
            Assert.False(await CalibrationFocusWait.WaitAsync(() => false, () => false,
                _ => { }, () => { ticks++; return Task.CompletedTask; }));
            Assert.Equal(150, ticks);
        }
        [Fact]
        public async Task ClosingPromptCancelsEvenWhenGameIsReady()
        {
            Assert.False(await CalibrationFocusWait.WaitAsync(() => true, () => true,
                _ => { }, () => Task.CompletedTask));
        }
    }
}
