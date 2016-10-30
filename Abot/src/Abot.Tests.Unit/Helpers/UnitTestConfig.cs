using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Abot.Tests.Unit.Helpers
{
    public class UnitTestConfig
    {
        public UnitTestConfig()
        {
            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json");
            var cr = builder.Build();

            LoadSiteSimulatorConfig(cr.GetSection("sitesimulator"));
        }

        private void LoadSiteSimulatorConfig(IConfigurationSection siteSimulator)
        {
            SiteSimulatorBaseAddress = siteSimulator["baseaddress"];
        }

        public string SiteSimulatorBaseAddress { get; private set; }
    }
}
