using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class FolderData
    {
        [JsonProperty("id")]
        public Guid? Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("external_id")]
        public string ExternalId { get; set; }

        [JsonProperty("folders")]
        public IEnumerable<FolderData> Folders { get; set; }
    }
}
