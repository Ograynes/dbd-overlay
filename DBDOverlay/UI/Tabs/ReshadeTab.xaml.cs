using DBDOverlay.Core.Reshade;
using DBDOverlay.Core.Utils;
using DBDOverlay.Properties;
using DBDOverlay.UI.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace DBDOverlay.UI.Tabs
{
    public partial class ReshadeTabView : UserControl
    {
        private bool initializing = true;
        public ReshadeTabView()
        {
            InitializeComponent();
            if (string.IsNullOrWhiteSpace(IntegrationSettings.Default.ConfigPath))
            {
                IntegrationSettings.Default.ConfigPath = ReShadeReload.FindConfig();
                IntegrationSettings.Default.Save();
            }
            AutoApplyToggle.IsChecked = IntegrationSettings.Default.AutoReload;
            RefreshView(); initializing = false;
            Loaded += (s, e) => { ReshadeManager.Instance.StatusChanged += StatusChanged; RefreshView(); };
            Unloaded += (s, e) => ReshadeManager.Instance.StatusChanged -= StatusChanged;
        }
        private void StatusChanged(object sender, EventArgs e) => Dispatcher.Invoke(RefreshStatus);
        private void RefreshStatus()
        {
            ApplyStatus.Text = ReshadeManager.Instance.Status;
            ActiveFilter.Text = ReshadeManager.Instance.LastFilter;
            ActiveMap.Text = ReshadeManager.Instance.LastMap;
        }
        private void RefreshView()
        {
            ReShadePathTextBox.Text = Settings.Default.ReshadeFiltersPath;
            ConfigPathTextBox.Text = IntegrationSettings.Default.ConfigPath;
            MainFilterNameTextBox.Text = string.IsNullOrWhiteSpace(Settings.Default.MainFilterName) ? "DBDOverlay" : Settings.Default.MainFilterName;
            int count = ReshadeManager.Instance.Filters.Count;
            FiltersStatusLabel.Text = $"{count} filters found";
            AssignFiltersButton.IsEnabled = count > 0;
            GenerateFilterButton.IsEnabled = count > 0 && !ReshadeManager.Instance.FilterExists(Settings.Default.MainFilterName);
            MainFilterNameTextBox.IsEnabled = GenerateFilterButton.IsEnabled;
            RefreshStatus();
        }
        private void Run(Action action)
        {
            try { action(); RefreshView(); }
            catch (Exception error) { ApplyStatus.Text = error.Message; Logger.Warn(error.Message); }
        }
        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new CommonOpenFileDialog { IsFolderPicker = true, Title = "Select ReShade preset folder" })
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok) Run(() => ChangeFolder(dialog.FileName));
        }
        private void ChangeFolder(string folder)
        {
            if (string.Equals(folder.TrimEnd('\\'), Settings.Default.ReshadeFiltersPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
            ReshadeManager.Instance.CancelReload();
            Settings.Default.ReshadeFiltersPath = folder;
            Settings.Default.ReshadeMappings = "";
            Settings.Default.Save();
            ReshadeManager.Instance.Initialize();
            ReshadeManager.Instance.ClearMapFilterPairs();
        }
        private void ClearFolder_Click(object sender, RoutedEventArgs e) => Run(() => ChangeFolder(""));
        private void RefreshFilters_Click(object sender, RoutedEventArgs e) => Run(() => ReshadeManager.Instance.Initialize());
        private void GenerateFilter_Click(object sender, RoutedEventArgs e) => Run(() =>
        {
            var name = MainFilterNameTextBox.Text.Trim();
            var path = PresetStore.Resolve(Settings.Default.ReshadeFiltersPath, name);
            if (File.Exists(path)) throw new IOException("That preset already exists. Choose another name.");
            if (ReshadeManager.Instance.Filters.Count == 0) throw new IOException("Add a source filter first.");
            PresetStore.Copy(Settings.Default.ReshadeFiltersPath, ReshadeManager.Instance.Filters[0], name);
            Settings.Default.MainFilterName = name; Settings.Default.Save();
        });
        private void AssignFilters_Click(object sender, RoutedEventArgs e) => Run(() =>
        {
            new AssignFiltersWindow { Owner = Application.Current.MainWindow }.ShowDialog();
        });
        private void SelectConfig_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "ReShade configuration|ReShade.ini|INI files|*.ini", Title = "Select the game's ReShade.ini" };
            if (dialog.ShowDialog() == true) Run(() =>
            {
                IntegrationSettings.Default.ConfigPath = dialog.FileName;
                IntegrationSettings.Default.Save(); ReshadeManager.Instance.CancelReload();
            });
        }
        private void AutoApply_Changed(object sender, RoutedEventArgs e)
        {
            if (initializing) return;
            Run(() => ReshadeManager.Instance.SetAutomaticReload(AutoApplyToggle.IsChecked == true));
        }
        private void ApplyNow_Click(object sender, RoutedEventArgs e) => Run(() => ReshadeManager.Instance.RequestReload());
    }
}
