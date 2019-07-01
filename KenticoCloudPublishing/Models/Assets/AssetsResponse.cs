
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Kentico.KenticoCloudPublishing
{
    internal class AssetsResponse
    {
        [JsonProperty("assets")]
        public IEnumerable<AssetData> Assets { get; set; }

        [JsonProperty("pagination")]
        public PaginationData Pagination { get; set; }
    }
}
