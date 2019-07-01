
using System;

using Newtonsoft.Json;

namespace Kentico.KenticoCloudPublishing
{
    internal class ItemData
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("type")]
        public IdReference Type { get; set; }
    }
}
