using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class ContentTypeData
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("elements")]
        public IEnumerable<ElementData> Elements { get; set; }

        [JsonProperty("content_groups")]
        public IEnumerable<ContentGroupData> ContentGroups { get; set; }
    }
}
