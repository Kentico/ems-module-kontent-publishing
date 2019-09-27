using System.Collections.Generic;

using Newtonsoft.Json;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class LanguagesResponse
    {
        [JsonProperty("languages")]
        public IEnumerable<LanguageData> Languages { get; set; }

        [JsonProperty("pagination")]
        public PaginationData Pagination { get; set; }
    }
}
