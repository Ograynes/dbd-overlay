using System;
using System.Threading.Tasks;
using DBDOverlay.Core.Utils;

namespace DBDOverlay.Core.BackgroundProcesses
{
    public abstract class BaseBackgroundProcess
    {
        private readonly object gate = new object();
        private volatile bool requested;
        private Task worker;
        public bool IsActive => requested;

        public void Run()
        {
            lock (gate)
            {
                requested = true;
                if (worker == null) worker = Task.Run(Loop);
            }
        }

        public virtual void Stop()
        {
            lock (gate) requested = false;
        }

        private void Loop()
        {
            try { while (requested) Action(); }
            catch (Exception error) { requested = false; Logger.Error(error.ToString()); }
            finally
            {
                lock (gate)
                {
                    worker = null;
                    if (requested) worker = Task.Run(Loop);
                }
            }
        }

        protected abstract void Action();
    }
}
