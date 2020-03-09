
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class FoldersResponse
    {
        [JsonProperty("folders")]
        public IEnumerable<FolderData> Folders { get; set; }
    }
}
