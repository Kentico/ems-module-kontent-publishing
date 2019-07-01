using System.Collections.Generic;

namespace Kentico.KenticoCloudPublishing
{
    internal class CategoryTerm
    {
        public string name { get; set; }

        public string external_id { get; set; }

        public IEnumerable<CategoryTerm> terms { get; set; }
    }
}