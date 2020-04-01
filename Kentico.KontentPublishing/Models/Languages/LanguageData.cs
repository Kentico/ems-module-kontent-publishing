using System;

using Newtonsoft.Json;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class LanguageData
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("external_id")]
        public string ExternalId { get; set; }

        [JsonProperty("codename")]
        public string Codename { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("is_active")]
        public bool IsActive { get; set; }
    }
}
