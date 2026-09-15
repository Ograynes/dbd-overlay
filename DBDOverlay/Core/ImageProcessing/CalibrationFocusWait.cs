using System;
using System.Threading.Tasks;

namespace DBDOverlay.Core.ImageProcessing
{
    public static class CalibrationFocusWait
    {
        public static async Task<bool> WaitAsync(Func<bool> ready, Func<bool> cancelled,
            Action<int> progress, Func<Task> delay)
        {
            for (int attempt = 0; attempt < 150; attempt++)
            {
                if (cancelled()) return false;
                if (ready()) return true;
                progress(30 - attempt / 5);
                await delay();
            }
            return !cancelled() && ready();
        }
    }
}
