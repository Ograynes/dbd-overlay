using System.Configuration;

namespace DBDOverlay.Core.Reshade
{
    public sealed class IntegrationSettings : ApplicationSettingsBase
    {
        public static IntegrationSettings Default { get; } = (IntegrationSettings)Synchronized(new IntegrationSettings());
        [UserScopedSetting, DefaultSettingValue("False")]
        public bool AutoReload { get => (bool)this[nameof(AutoReload)]; set => this[nameof(AutoReload)] = value; }
        [UserScopedSetting, DefaultSettingValue("")]
        public string ConfigPath { get => (string)this[nameof(ConfigPath)]; set => this[nameof(ConfigPath)] = value; }
        [UserScopedSetting, DefaultSettingValue("")]
        public string NamedMappings { get => (string)this[nameof(NamedMappings)]; set => this[nameof(NamedMappings)] = value; }
        [UserScopedSetting, DefaultSettingValue("")]
        public string MappingFolder { get => (string)this[nameof(MappingFolder)]; set => this[nameof(MappingFolder)] = value; }
    }
}
