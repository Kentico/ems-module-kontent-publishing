﻿using Newtonsoft.Json;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class MultipleChoiceElementOption
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("codename")]
        public string Codename { get; set; }
    }
}
