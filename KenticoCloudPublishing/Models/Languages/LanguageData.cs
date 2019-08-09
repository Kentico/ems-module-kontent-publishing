
using System;

using Newtonsoft.Json;

namespace Kentico.KenticoCloudPublishing
{
    internal class LanguageData
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("codename")]
        public string Codename { get; set; }
    }
}
