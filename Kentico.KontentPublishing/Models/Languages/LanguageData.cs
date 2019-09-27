
using System;

using Newtonsoft.Json;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class LanguageData
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("codename")]
        public string Codename { get; set; }
    }
}
