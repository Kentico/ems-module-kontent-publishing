using System;

using Newtonsoft.Json;

namespace Kentico.KenticoCloudPublishing
{
    public class SnippetData
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }
    }
}