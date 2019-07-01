using System.Collections.Generic;

using Newtonsoft.Json;

namespace Kentico.KenticoCloudPublishing
{
    internal class ItemsResponse
    {
        [JsonProperty("items")]
        public IEnumerable<ItemData> Items { get; set; }

        [JsonProperty("pagination")]
        public PaginationData Pagination { get; set; }
    }
}
