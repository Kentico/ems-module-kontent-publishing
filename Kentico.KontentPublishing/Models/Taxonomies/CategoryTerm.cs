using System.Collections.Generic;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class CategoryTerm
    {
        public string name { get; set; }

        public string codename { get; set; }

        public string external_id { get; set; }

        public IEnumerable<CategoryTerm> terms { get; set; }
    }
}