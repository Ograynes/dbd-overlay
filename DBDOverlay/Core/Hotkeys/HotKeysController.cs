using DBDOverlay.Core.ImageProcessing;
using DBDOverlay.Core.WindowControllers.MapOverlay;
using DBDOverlay.Properties;
using System.Windows.Forms;
using System.Windows.Input;

namespace DBDOverlay.Core.Hotkeys
{
    public static class HotKeysController
    {
        public static void RegisterAllHotKeys()
        {
            var readModifier = (ModifierKeys)Settings.Default.ReadModifier;
            var readKey = (Keys)Settings.Default.ReadKey;
            var nextModifier = (ModifierKeys)Settings.Default.NextMapModifier;
            var nextKey = (Keys)Settings.Default.NextMapKey;
            var previousModifier = (ModifierKeys)Settings.Default.PreviousMapModifier;
            var previousKey = (Keys)Settings.Default.PreviousMapKey;
            var screenshotsModifier = (ModifierKeys)Settings.Default.CreateScreenshotsModifier;
            var screenshotsKey = (Keys)Settings.Default.CreateScreenshotsKey;

            KeyboardHook.Instance.RegisterHotKey((int)HotKeyType.Read, readModifier, readKey, PressedRead);
            KeyboardHook.Instance.RegisterHotKey((int)HotKeyType.NextMap, nextModifier, nextKey, PressedNext);
            KeyboardHook.Instance.RegisterHotKey((int)HotKeyType.PreviousMap, previousModifier, previousKey, PressedPrevious);
            KeyboardHook.Instance.RegisterHotKey((int)HotKeyType.CreateScreenshots, screenshotsModifier, screenshotsKey, PressedSaveImages);
        }

        public static void UnregisterAllHotKeys()
        {
            KeyboardHook.Instance.UnregisterAllHotKeys();
        }

        public static void Dispose()
        {
            KeyboardHook.Instance.Dispose();
        }

        private static async void PressedRead(object sender, KeyPressedEventArgs e)
        {
            e.Log("Read map");
            try
            {
                var map = await System.Threading.Tasks.Task.Run(() => ImageReader.Instance.GetMapInfo());
                MapOverlayController.Instance.ChangeMap(map);
            }
            catch (System.Exception error) { System.Windows.MessageBox.Show(error.Message, "Map recognition"); }
        }

        private static void PressedNext(object sender, KeyPressedEventArgs e)
        {
            e.Log("Next map");
            MapOverlayController.Instance.SwitchMapVariationToNext();
        }

        private static void PressedPrevious(object sender, KeyPressedEventArgs e)
        {
            e.Log("Previous map");
            MapOverlayController.Instance.SwitchMapVariationToPrevious();
        }

        private static void PressedSaveImages(object sender, KeyPressedEventArgs e)
        {
            e.Log("Save images");
            ImageReader.Instance.HandleSurvivors(Settings.Default.Is2v8Mode, true);
        }
    }
}
