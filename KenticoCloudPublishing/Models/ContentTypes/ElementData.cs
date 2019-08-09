using System;
using Newtonsoft.Json;

namespace Kentico.KenticoCloudPublishing
{
    internal class ElementData
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }
    }
}
