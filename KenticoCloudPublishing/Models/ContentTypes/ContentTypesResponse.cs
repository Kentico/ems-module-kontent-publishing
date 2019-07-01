using System.Collections.Generic;

using Newtonsoft.Json;

namespace Kentico.KenticoCloudPublishing
{
    internal class ContentTypesResponse
    {
        [JsonProperty("types")]
        public IEnumerable<ContentTypeData> Types { get; set; }

        [JsonProperty("pagination")]
        public PaginationData Pagination { get; set; }
    }
}
