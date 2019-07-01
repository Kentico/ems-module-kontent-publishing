
using System;

using Newtonsoft.Json;

namespace Kentico.KenticoCloudPublishing
{
    internal class IdReference
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }
    }
}
