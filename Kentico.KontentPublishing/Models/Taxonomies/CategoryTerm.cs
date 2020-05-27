using Newtonsoft.Json;
using System.Collections.Generic;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class CategoryTerm
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("codename")]
        public string Codename { get; set; }

        [JsonProperty("external_id")]
        public string ExternalId { get; set; }

        [JsonProperty("terms")]
        public IEnumerable<CategoryTerm> Terms { get; set; }
    }
}