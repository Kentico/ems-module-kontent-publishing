
using System;

using Newtonsoft.Json;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class IdReference
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }
    }
}
