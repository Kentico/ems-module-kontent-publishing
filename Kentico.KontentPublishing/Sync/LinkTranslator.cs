using CMS.Base.Web.UI;
using CMS.SiteProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class LinkTranslator
    {
        private readonly SyncSettings _settings;
        private readonly AssetSync _assetSync;

        public LinkTranslator(SyncSettings settings, AssetSync assetSync)
        {
            _settings = settings;
            _assetSync = assetSync;
        }

        private string TranslateMediaQuery(string query)
        {
            if (!string.IsNullOrEmpty(query))
            {
                var newQueryParams = new List<KeyValuePair<string, string>>();

                var queryParams = HttpUtility.ParseQueryString(HttpUtility.HtmlDecode(query));
                foreach (var key in queryParams.AllKeys)
                {
                    var newQueryParam = TranslateQueryParam(key, queryParams[key]);
                    if (newQueryParam != null)
                    {
                        newQueryParams.AddRange(newQueryParam);
                    }
                }

                if (newQueryParams.Count > 0)
                {
                    var newQuery = $"?{ string.Join("&", newQueryParams.Select(param => $"{HttpUtility.UrlEncode(param.Key)}={HttpUtility.UrlEncode(param.Value)}"))}";

                    return newQuery;
                }
            }

            return null;
        }

        private static readonly Regex UrlAttributeRegEx = new Regex("(?<start>\\b(href|src)=\")(?<url>[^\"?]+)(?<query>\\?[^\"]+)?(?<end>\")");

        private async Task<string> ReplaceMediaLink(Match match)
        {
            var start = Convert.ToString(match.Groups["start"]);
            var url = HttpUtility.HtmlDecode(Convert.ToString(match.Groups["url"]));
            var query = HttpUtility.HtmlDecode(Convert.ToString(match.Groups["query"]));
            var end = Convert.ToString(match.Groups["end"]);

            try
            {
                // We need to set current site before every call to GetMediaData to avoid null reference
                SiteContext.CurrentSiteName = _settings.Sitename;
                var data = CMSDialogHelper.GetMediaData(url, _settings.Sitename);
                if (data != null)
                {
                    switch (data.SourceType)
                    {
                        case MediaSourceEnum.Attachment:
                        case MediaSourceEnum.DocumentAttachments:
                            {
                                var assetUrl = await _assetSync.GetAssetUrl("attachment", data.AttachmentGuid);
                                var newQuery = TranslateMediaQuery(query);

                                return $"{start}{HttpUtility.HtmlEncode(assetUrl)}{HttpUtility.HtmlEncode(newQuery)}{end}";
                            }

                        case MediaSourceEnum.MediaLibraries:
                            {
                                var assetUrl = await _assetSync.GetAssetUrl("media", data.MediaFileGuid);
                                var newQuery = TranslateMediaQuery(query);

                                return $"{start}{HttpUtility.HtmlEncode(assetUrl)}{HttpUtility.HtmlEncode(newQuery)}{end}";
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "TRANSLATEURL", ex, 0, $"Failed to replace media URL '{url + query}', keeping the original URL.");
            }

            // Keep as it is if translation is not successful, only resolve to absolute URL if needed
            if (url.StartsWith("~"))
            {
                return $"{start}{HttpUtility.HtmlEncode(_settings.WebRoot)}{HttpUtility.HtmlEncode(url.Substring(1))}{HttpUtility.HtmlEncode(query)}{end}";
            }
            return match.ToString();
        }

        private KeyValuePair<string, string>[] TranslateQueryParam(string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "width":
                    return new[] { new KeyValuePair<string, string>("w", value) };

                case "height":
                    return new[] { new KeyValuePair<string, string>("h", value) };

                case "maxsidesize":
                    return new[] {
                        new KeyValuePair<string, string>("w", value),
                        new KeyValuePair<string, string>("h", value),
                        new KeyValuePair<string, string>("fit", "clip")
                    };

                default:
                    return null;
            }
        }

        public async Task<string> TranslateLinks(string content)
        {
            return await UrlAttributeRegEx.ReplaceAsync(content, ReplaceMediaLink);
        }
    }
}
