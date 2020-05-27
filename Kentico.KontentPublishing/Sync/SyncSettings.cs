using System;
using System.Configuration;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class SyncSettings
    {
        public Guid ProjectId { get; private set; }

        public string CMApiKey { get; private set; }

        public string AssetsDomain { get; private set; }

        public string Sitename { get; private set; }

        public string WebRoot { get; private set; }

        public void LoadFromConfig()
        {
            Guid.TryParse(ConfigurationManager.AppSettings.Get("KCSyncProjectID"), out Guid projectId);
            ProjectId = projectId;

            CMApiKey = ConfigurationManager.AppSettings.Get("KCSyncCMAPIKey");

            AssetsDomain = ConfigurationManager.AppSettings.Get("KCSyncAssetsDomain");

            WebRoot = ConfigurationManager.AppSettings.Get("KCSyncWebRoot");

            Sitename = ConfigurationManager.AppSettings.Get("KCSyncSitename");
        }

        public bool IsValid()
        {
            return
                (ProjectId != Guid.Empty) &&
                !string.IsNullOrEmpty(CMApiKey) &&
                !string.IsNullOrEmpty(AssetsDomain) &&
                !string.IsNullOrEmpty(WebRoot) &&
                !string.IsNullOrEmpty(Sitename);
        }
    }
}
