using System;

using Newtonsoft.Json;

namespace Kentico.KenticoCloudPublishing
{
    internal class FileReferenceData
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }
    }
}
