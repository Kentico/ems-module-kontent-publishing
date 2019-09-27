using System;

using Newtonsoft.Json;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class AssetData
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("file_reference")]
        public IdReference FileReference { get; set; }

        [JsonProperty("size")]
        public int Size { get; set; }

        [JsonProperty("file_name")]
        public string FileName { get; set; }
    }
}
