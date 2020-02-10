
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class TaxonomyResponse
    {
        [JsonProperty("taxonomies")]
        public IEnumerable<TaxonomyData> Taxonomies { get; set; }

        [JsonProperty("pagination")]
        public PaginationData Pagination { get; set; }
    }
}
