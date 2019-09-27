
using Newtonsoft.Json;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class PaginationData
    {
        [JsonProperty("continuation_token")]
        public string ContinuationToken { get; set; }
    }
}
