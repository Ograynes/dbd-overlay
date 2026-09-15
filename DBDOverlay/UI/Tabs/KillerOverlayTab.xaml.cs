using DBDOverlay.Core.BackgroundProcesses;
using DBDOverlay.Core.Extensions;
using DBDOverlay.Core.ImageProcessing;
using DBDOverlay.Core.WindowControllers.KillerOverlay;
using DBDOverlay.Core.Windows;
using DBDOverlay.Properties;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;

namespace DBDOverlay.UI.Tabs
{
    public partial class KillerOverlayTabView : UserControl
    {
        private readonly int RGBSum = 765;
        private readonly int defaultHooksThreshold = 600;

        public KillerOverlayTabView()
        {
            InitializeComponent();
            KillerOverlayController.Overlay.HideHooks();
            KillerOverlayController.Overlay.HideTimer();

            HooksToggleButton.IsChecked = Settings.Default.IsHookMode;
            PostUnhookTimerToggleButton.IsChecked = Settings.Default.IsPostUnhookTimerMode;
            Mode2v8ToggleButton.IsChecked = Settings.Default.Is2v8Mode;
            SidePanelToggleButton.IsChecked = Settings.Default.IsSidePanelMode;

            //if (KillerOverlayController.Instance.CanBeMoved) SelectAreaToggleButton.IsChecked = true;
            WindowsServices.Instance.KillerOverlayMoveModeOff += HandleMoveModeOff;

            SetSliderValue(Settings.Default.HooksThreshold);
        }

        private async void Calibrate_Click(object sender, RoutedEventArgs e)
        {
            await OpenCalibration(false);
        }

        private async void LearnUnhooked_Click(object sender, RoutedEventArgs e)
        {
            await OpenCalibration(true);
        }

        private async System.Threading.Tasks.Task OpenCalibration(bool learn)
        {
            if (WindowsServices.Instance.IsCalibrating) return;
            if (!GameCapture.TryGetBounds(out var bounds, false))
            {
                MessageBox.Show("No visible Dead by Daylight window was found. Restore the game if it is minimized, then try again.");
                return;
            }
            bool wasRunning = KillerMode.Instance.IsActive;
            KillerMode.Instance.Stop();
            WindowsServices.Instance.IsCalibrating = true;
            WindowsServices.Instance.CheckActiveWindow();
            var main = Application.Current.MainWindow;
            var previous = main.WindowState;
            var sidePanel = KillerOverlayController.Window;
            bool panelWasVisible = sidePanel.IsVisible;
            var hint = new TextBlock { Margin = new Thickness(14), TextWrapping = TextWrapping.Wrap,
                Foreground = System.Windows.Media.Brushes.White };
            var waiting = new Window { Title = "Calibration", Width = 380, Height = 110,
                ShowActivated = false, ShowInTaskbar = false, Topmost = true,
                WindowStyle = WindowStyle.ToolWindow, ResizeMode = ResizeMode.NoResize,
                Background = (System.Windows.Media.Brush)FindResource("DarkestGrayBrush"), Content = hint,
                Left = SystemParameters.WorkArea.Right - 400, Top = SystemParameters.WorkArea.Top + 20 };
            bool cancelled = false;
            waiting.Closed += (s, e) => cancelled = true;
            KillerOverlayController.Overlay.Hide();
            main.WindowState = WindowState.Minimized;
            sidePanel.Hide();
            try
            {
                hint.Text = "Click inside Dead by Daylight. Calibration will open automatically. Close this message to cancel.";
                waiting.Show();
                bool ready = await CalibrationFocusWait.WaitAsync(
                    () => GameCapture.TryGetBounds(out bounds), () => cancelled,
                    seconds => hint.Text = $"Click inside Dead by Daylight ({seconds}s). Calibration opens automatically. Close to cancel.",
                    () => System.Threading.Tasks.Task.Delay(200));
                if (!ready) return;
                waiting.Hide();
                await System.Threading.Tasks.Task.Delay(200);
                if (!GameCapture.TryGetBounds(out bounds)) return;
                using (var snapshot = GameCapture.Capture(bounds))
                {
                    var dialog = new DBDOverlay.UI.Windows.HudCalibrationWindow(snapshot, Settings.Default.Is2v8Mode, learn);
                    dialog.ShowDialog();
                }
                KillerOverlayController.Instance.ResetSurvivors();
            }
            catch (System.Exception error) { MessageBox.Show(error.Message, "Calibration"); }
            finally
            {
                waiting.Close();
                main.WindowState = previous;
                if (panelWasVisible) sidePanel.Show();
                WindowsServices.Instance.IsCalibrating = false;
                WindowsServices.Instance.CheckActiveWindow();
                if (wasRunning) KillerMode.Instance.RunConditional();
            }
        }

        private void ResetCalibration_Click(object sender, RoutedEventArgs e)
        {
            DetectionSettings.Default.SetRegion(null, Settings.Default.Is2v8Mode);
            KillerOverlayController.Instance.ResetSurvivors();
        }
        private void Hooks_Checked(object sender, RoutedEventArgs e)
        {
            Settings.Default.IsHookMode = true;
            Settings.Default.Save();
            KillerOverlayController.Overlay.ShowHooks();
            KillerMode.Instance.RunConditional();
            WindowsServices.Instance.CheckActiveWindow();
        }

        private void Hooks_Unchecked(object sender, RoutedEventArgs e)
        {
            Settings.Default.IsHookMode = false;
            Settings.Default.Save();
            KillerOverlayController.Overlay.HideHooks();
            KillerMode.Instance.StopConditional();
            WindowsServices.Instance.CheckActiveWindow();
        }

        private void PostUnhookTimer_Checked(object sender, RoutedEventArgs e)
        {
            Settings.Default.IsPostUnhookTimerMode = true;
            Settings.Default.Save();
            KillerOverlayController.Overlay.ShowTimer();
            KillerMode.Instance.RunConditional();
            WindowsServices.Instance.CheckActiveWindow();
        }

        private void PostUnhookTimer_Unchecked(object sender, RoutedEventArgs e)
        {
            Settings.Default.IsPostUnhookTimerMode = false;
            Settings.Default.Save();
            KillerOverlayController.Overlay.HideTimer();
            KillerMode.Instance.StopConditional();
            WindowsServices.Instance.CheckActiveWindow();
        }

        private void Mode2v8_Checked(object sender, RoutedEventArgs e)
        {
            Settings.Default.Is2v8Mode = true;
            Settings.Default.Save();
            KillerOverlayController.Overlay.ShowMoreSurvivors();
            KillerOverlayController.Window.ShowMoreSurvivors(false);
        }

        private void Mode2v8_Unchecked(object sender, RoutedEventArgs e)
        {
            Settings.Default.Is2v8Mode = false;
            Settings.Default.Save();
            KillerOverlayController.Overlay.HideMoreSurvivors();
            KillerOverlayController.Window.HideMoreSurvivors(false);
        }

        private void SidePanel_Checked(object sender, RoutedEventArgs e)
        {
            Settings.Default.IsSidePanelMode = true;
            Settings.Default.Save();
            KillerMode.Instance.RunConditional();
            if (SidePanelToggleButton.IsVisible)
            {
                KillerOverlayController.Window.ShowSidePanel();
            }
        }

        private void SidePanel_Unchecked(object sender, RoutedEventArgs e)
        {
            if (SidePanelToggleButton.IsVisible)
            {
                KillerOverlayController.Window.HideSidePanel();
            }
            Settings.Default.IsSidePanelMode = false;
            Settings.Default.Save();
            KillerMode.Instance.StopConditional();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            KillerOverlayController.Instance.ResetSurvivors();
        }

        private void SelectArea_Checked(object sender, RoutedEventArgs e)
        {
            KillerOverlayController.Overlay.ShowGrid();
            KillerOverlayController.Instance.CanBeMoved = true;
            WindowsServices.Instance.RevertWindowExTransparent(KillerOverlayController.Overlay, KillerOverlayController.Overlay.DefaultStyle);
        }

        private void SelectArea_Unchecked(object sender, RoutedEventArgs e)
        {
            KillerOverlayController.Overlay.HideGrid();
            KillerOverlayController.Overlay.DefaultStyle = WindowsServices.Instance.SetWindowExTransparent(KillerOverlayController.Overlay);
            KillerOverlayController.Instance.CanBeMoved = false;
            KillerOverlayController.Overlay.SaveBounds();
        }

        private void ResetPosSize_Click(object sender, RoutedEventArgs e)
        {
            KillerOverlayController.Overlay.ResetBounds();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var threshold = (ThresholdSlider.Value * RGBSum / 100).Round();
            SetThreshold(threshold);
        }

        private void ResetThreshold_Click(object sender, RoutedEventArgs e)
        {
            SetThreshold(defaultHooksThreshold);
            SetSliderValue(defaultHooksThreshold);
        }

        private void HandleMoveModeOff(object sender, EventArgs e)
        {
            //SelectAreaToggleButton.Uncheck();
        }

        private void Calibration_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void Calibration_Unchecked(object sender, RoutedEventArgs e)
        {

        }

        private void SetThreshold(int threshold)
        {
            ImageReader.Instance.SetHooksThreshold(threshold);
            Settings.Default.HooksThreshold = threshold;
            Settings.Default.Save();
        }

        private void SetSliderValue(int threshold)
        {
            ThresholdSlider.Value = (threshold * 100.0 / RGBSum).Round();
        }

    }
}
