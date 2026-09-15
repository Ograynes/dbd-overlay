using System;
using System.IO;
using DBDOverlay.Core.WindowControllers.MapOverlay.Languages;
using Xunit;

namespace DBDOverlay.Tests
{
    public class StartupTests
    {
        [Fact]
        public void MapResourcesLoadUsingExecutableConfiguration()
        {
            var directory = AppDomain.CurrentDomain.BaseDirectory;
            var configuration = Path.Combine(directory, "DBDOverlay.exe.config");
            Assert.True(File.Exists(configuration));
            // The test runner's own redirects can hide failures in the shipped app config.
            var domain = AppDomain.CreateDomain("DBDOverlay startup probe", null, new AppDomainSetup
            {
                ApplicationBase = directory,
                ConfigurationFile = configuration
            });
            try
            {
                var probe = (ResourceProbe)domain.CreateInstanceAndUnwrap(
                    typeof(ResourceProbe).Assembly.FullName, typeof(ResourceProbe).FullName);
                Assert.True(probe.LoadMapNames() > 0);
            }
            finally { AppDomain.Unload(domain); }
        }
    }

    public sealed class ResourceProbe : MarshalByRefObject
    {
        public int LoadMapNames() => MapNamesContainer.GetReshadeMapsList().Count;
    }
}
