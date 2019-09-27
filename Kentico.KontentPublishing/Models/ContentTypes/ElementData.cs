using System;
using Newtonsoft.Json;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class ElementData
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }
    }
}
