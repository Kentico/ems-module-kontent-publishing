
using System;

using Newtonsoft.Json;

namespace Kentico.KenticoCloudPublishing
{
    internal class TaxonomyData
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }
    }
}
