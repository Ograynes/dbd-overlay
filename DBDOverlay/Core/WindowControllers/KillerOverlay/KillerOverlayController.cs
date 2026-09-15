using DBDOverlay.Core.Extensions;
using DBDOverlay.Core.Utils;
using DBDOverlay.Properties;
using DBDOverlay.UI.Styles;
using DBDOverlay.UI.Windows.Overlays;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Application = System.Windows.Application;

namespace DBDOverlay.Core.WindowControllers.KillerOverlay
{
    public class KillerOverlayController
    {
        public bool CanBeMoved { get; set; } = false;
        public List<Survivor> Survivors = new List<Survivor>();
        private static KillerOverlayController instance;
        private static KillerOverlayWindow killerOverlay;
        private static KillerOverlayWindow killerOverlayWindow;

        private int survivorsCount;
        private int maxTimer;
        private int unhookEndurance;

        private readonly int survivorsCountDefault = 4;
        private readonly int survivorsCount2v8 = 8;
        private readonly int unhookAnimationDelay = 1500;
        private readonly int maxTimerDefault = 60000;
        private readonly int unhookEnduranceDefault = 15000;
        private readonly int unhookEndurance2v8 = 10000;

        private readonly string defaultTimerValue;
        private readonly char delimiter;

        public static KillerOverlayController Instance
        {
            get
            {
                if (instance == null) instance = new KillerOverlayController();
                return instance;
            }
        }

        public static KillerOverlayWindow Overlay
        {
            get
            {
                if (killerOverlay == null) killerOverlay = new KillerOverlayWindow();
                return killerOverlay;
            }
        }

        public static KillerOverlayWindow Window
        {
            get
            {
                if (killerOverlayWindow == null) killerOverlayWindow = new KillerOverlayWindow(false);
                return killerOverlayWindow;
            }
        }

        public KillerOverlayController()
        {
            delimiter = 0.1.ToString().ReplaceRegex(@"\d", string.Empty).FirstOrDefault();
            defaultTimerValue = $"0{delimiter}0";
            survivorsCount = survivorsCountDefault;
            maxTimer = maxTimerDefault;
            unhookEndurance = unhookEnduranceDefault;
            SetSurvivors();
        }

        private readonly List<HookTracker> trackers = new List<HookTracker>();
        private readonly List<UnhookTimer> timers = new List<UnhookTimer>();
        private int detectionGeneration;
        public int DetectionGeneration => Volatile.Read(ref detectionGeneration);

        public void Observe(int index, HudObservation observation)
        {
            if (index < 0 || index >= trackers.Count) return;
            var transition = trackers[index].Observe(observation);
            if (transition == HookTransition.None) return;
            Logger.Info($"Survivor {index + 1}: {transition}");
            timers[index].Cancel();
            HandleAction(KillerOverlayAction.SetTimerValue, index, defaultTimerValue);
            HandleAction(KillerOverlayAction.SetDefaultTimer, index);
            if (transition == HookTransition.Hooked)
            {
                Survivors[index].State = SurvivorState.Hooked;
                Survivors[index].Hooks = trackers[index].Hooks;
                HandleAction(KillerOverlayAction.IncrementHooks, index);
            }
            else if (transition == HookTransition.Unhooked)
            {
                Survivors[index].State = SurvivorState.Unhooked;
                RunTimer(index);
            }
        }

        public void SuspendDetection()
        {
            Interlocked.Increment(ref detectionGeneration);
            foreach (var tracker in trackers) tracker.Observe(HudObservation.Unknown);
        }

        public void CancelTimers()
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(CancelTimers);
                return;
            }
            SuspendDetection();
            foreach (var timer in timers) timer.Cancel();
            SetTimers();
        }
        public void SetTimers()
        {
            for (int i = 0; i < survivorsCount; i++)
            {
                HandleAction(KillerOverlayAction.SetDefaultTimerConditional, i);
            }
        }

        public void ResetSurvivors()
        {
            SuspendDetection();
            var is2v8Mode = Settings.Default.Is2v8Mode;
            maxTimer = is2v8Mode ? unhookEndurance2v8 : maxTimerDefault;
            survivorsCount = is2v8Mode ? survivorsCount2v8 : survivorsCountDefault;
            unhookEndurance = is2v8Mode ? unhookEndurance2v8 : unhookEnduranceDefault;
            foreach (var timer in timers) timer.Dispose();
            timers.Clear();
            trackers.Clear();
            Survivors.Clear();
            SetSurvivors();
            for (int i = 0; i < survivorsCount; i++)
            {
                HandleAction(KillerOverlayAction.ResetHooks, i);
            }
            SetTimers();
            ResizeLabels();
        }

        private void SetSurvivors()
        {
            for (int i = 1; i <= survivorsCount; i++)
            {
                Survivors.Add(new Survivor());
                trackers.Add(new HookTracker());
                timers.Add(new UnhookTimer());
            }
        }

        private void ResizeLabels()
        {
            for (int i = 0; i < survivorsCount; i++)
            {
                HandleAction(KillerOverlayAction.ResizeLabels, i);
            }
        }

        private async void RunTimer(int index)
        {
            var timer = timers[index];
            await timer.RunAsync(maxTimer, (generation, elapsed) =>
            {
                if (index >= timers.Count || timers[index] != timer || timer.Generation != generation) return;
                if (elapsed < 0)
                {
                    HandleAction(KillerOverlayAction.SetTimerValue, index, defaultTimerValue);
                    HandleAction(KillerOverlayAction.SetDefaultTimer, index);
                    return;
                }
                HandleAction(KillerOverlayAction.SetTimerValue, index, (elapsed / 1000.0).ToString("F1"));
                HandleAction(elapsed < unhookEndurance ? KillerOverlayAction.SetEnduranceTimer : KillerOverlayAction.SetDefaultTimer, index);
            }, unhookAnimationDelay);
        }
        private void HandleAction(KillerOverlayAction actionType, int survivorIndex, string argument = null)
        {
            if (Settings.Default.IsHookMode || Settings.Default.IsPostUnhookTimerMode) ActionFactory(Overlay, actionType, survivorIndex, argument);
            if (Settings.Default.IsSidePanelMode) ActionFactory(Window, actionType, survivorIndex, argument);
        }

        private void ActionFactory(KillerOverlayWindow killerWindow, KillerOverlayAction actionType, int index, string argument = null)
        {
            switch (actionType)
            {
                case KillerOverlayAction.IncrementHooks:
                    killerWindow.GetHooksLabel((SurvivorNumber)(index + 1)).IncrementHooks();
                    break;
                case KillerOverlayAction.SetTimerValue:
                    killerWindow.GetTimerLabel((SurvivorNumber)(index + 1)).Content = argument;
                    break;
                case KillerOverlayAction.SetEnduranceTimer:
                    killerWindow.GetTimerLabel((SurvivorNumber)(index + 1)).UpdateColors(Palette.DarkYellowBrush, Palette.DarkestGrayBrush);
                    break;
                case KillerOverlayAction.SetDefaultTimer:
                    killerWindow.GetTimerLabel((SurvivorNumber)(index + 1)).UpdateColors(Palette.DarkestGrayBrush, Palette.WhiteBrush);
                    break;
                case KillerOverlayAction.SetDefaultTimerConditional:
                    {
                        var textBlock = killerWindow.GetTimerLabel((SurvivorNumber)(index + 1));
                        textBlock.Content = defaultTimerValue;
                        if (textBlock.GetBorder() != null)
                        {
                            textBlock.UpdateColors(Palette.DarkestGrayBrush, Palette.WhiteBrush);
                        }
                        break;
                    }
                case KillerOverlayAction.ResetHooks:
                    {
                        var textBlock = killerWindow.GetHooksLabel((SurvivorNumber)(index + 1));
                        textBlock.Content = "0";
                        var labelBorder = textBlock.GetBorder();
                        if (labelBorder != null)
                        {
                            labelBorder.Background = Palette.DarkestGrayBrush;
                        }
                        break;
                    }
                case KillerOverlayAction.ResizeLabels:
                    {
                        var is2v8Mode = Settings.Default.Is2v8Mode;
                        killerWindow.GetHooksLabel((SurvivorNumber)(index + 1)).FontSize = is2v8Mode ? 16 : 20;
                        killerWindow.GetTimerLabel((SurvivorNumber)(index + 1)).FontSize = is2v8Mode ? 12 : 16;
                        break;
                    }
                default:
                    break;
            }
        }
    }
}
