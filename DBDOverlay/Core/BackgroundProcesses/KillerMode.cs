using DBDOverlay.Core.ImageProcessing;
using DBDOverlay.Properties;
using DBDOverlay.Core.WindowControllers.KillerOverlay;

namespace DBDOverlay.Core.BackgroundProcesses
{
    public class KillerMode : BaseBackgroundProcess
    {
        private static KillerMode instance;

        public static KillerMode Instance
        {
            get
            {
                if (instance == null) instance = new KillerMode();
                return instance;
            }
        }

        protected override void Action()
        {
            ImageReader.Instance.HandleSurvivors(Settings.Default.Is2v8Mode);
        }

        public void RunConditional()
        {
            if (!IsActive && ShouldRun(Settings.Default.IsHookMode, Settings.Default.IsPostUnhookTimerMode, Settings.Default.IsSidePanelMode)) Run();
        }

        public static bool ShouldRun(bool hooks, bool timer, bool sidePanel) => hooks || timer || sidePanel;

        public void StopConditional()
        {
            if (!ShouldRun(Settings.Default.IsHookMode, Settings.Default.IsPostUnhookTimerMode, Settings.Default.IsSidePanelMode)) Stop();
        }

        public override void Stop()
        {
            base.Stop();
            KillerOverlayController.Instance.CancelTimers();
        }
    }
}
