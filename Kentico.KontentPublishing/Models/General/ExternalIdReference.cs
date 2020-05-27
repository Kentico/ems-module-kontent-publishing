using Newtonsoft.Json;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class ExternalIdReference
    {
        [JsonProperty("external_id")]
        public string ExternalId { get; set; }
    }
}
