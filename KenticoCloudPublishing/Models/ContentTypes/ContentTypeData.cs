using System;

using Newtonsoft.Json;

namespace Kentico.KenticoCloudPublishing
{
    internal class ContentTypeData
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }
    }
}
