
using Newtonsoft.Json;

namespace Kentico.KenticoCloudPublishing
{
    internal class PaginationData
    {
        [JsonProperty("continuation_token")]
        public string ContinuationToken { get; set; }
    }
}
