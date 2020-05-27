using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using CMS.EventLog;
using CMS.Localization;
using CMS.SiteProvider;

namespace Kentico.EMS.Kontent.Publishing
{
    internal partial class LanguageSync : SyncBase
    {
        private const int CULTURE_MAXLENGTH = 25;

        public LanguageSync(SyncSettings settings) : base(settings)
        {
        }

        #region "External IDs"

        public static string GetLanguageExternalId(Guid cultureGuid)
        {
            return $"language|{cultureGuid}";
        }

        #endregion

        #region "Synchronization"

        public bool IsAtSynchronizedSite(CultureInfo culture)
        {
            var siteId = SiteInfoProvider.GetSiteID(Settings.Sitename);

            return CultureSiteInfoProvider.GetCultureSiteInfo(culture.CultureID, siteId) != null;
        }

        private async Task<List<LanguageData>> GetAllLanguages(string continuationToken = null)
        {
            var endpoint = $"/languages";

            var response = await ExecuteWithResponse<LanguagesResponse>(endpoint, HttpMethod.Get, continuationToken);
            if (response == null)
            {
                return new List<LanguageData>();
            }

            var languages = response.Languages.ToList();

            if (
                (languages.Count > 0) &&
                (response.Pagination != null) &&
                !string.IsNullOrEmpty(response.Pagination.ContinuationToken) &&
                (response.Pagination.ContinuationToken != continuationToken)
            )
            {
                var nextIds = await GetAllLanguages(response.Pagination.ContinuationToken);
                languages = languages.Concat(nextIds).ToList();
            }

            return languages;
        }

        public async Task SyncCultures()
        {
            try
            {
                SyncLog.Log("Synchronizing cultures");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "SYNCCULTURES");

                var existingLanguages = await GetAllLanguages();
                var cultures = CultureSiteInfoProvider.GetSiteCultures(Settings.Sitename).ToList();

                await PatchDefaultLanguage();

                // Deactivate all unknown languages to make sure they don't conflict with the active ones
                foreach (var language in existingLanguages)
                {
                    if (language.IsActive && (language.Id != Guid.Empty) && !cultures.Exists(culture => GetLanguageExternalId(culture.CultureGUID).Equals(language.ExternalId)))
                    {
                        await DeactivateLanguage(language);
                    }
                }

                // Create or update all known languages
                foreach (var culture in cultures)
                {
                    if (existingLanguages.Exists(language => language.ExternalId?.Equals(GetLanguageExternalId(culture.CultureGUID)) == true))
                    {
                        await PatchLanguage(culture);
                    }
                    else
                    {
                        await CreateLanguage(culture);
                    }
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "SYNCCULTURES", ex);
                throw;
            }
        }

        private async Task DeactivateLanguage(LanguageData language)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DEACTIVATELANGUAGE", $"{language.Codename})");

                var endpoint = $"/languages/{language.Id}";

                var payload = new object[] {
                    new
                    {
                        op = "replace",
                        property_name = "name",
                        value = ("(deactivated) " + language.Name).LimitedTo(CULTURE_MAXLENGTH),
                    },
                    new
                    {
                        op = "replace",
                        property_name = "codename",
                        value = ("deactivated_" + language.Codename).LimitedTo(CULTURE_MAXLENGTH),
                    },
                    new
                    {
                        op = "replace",
                        property_name = "is_active",
                        value = false,
                    },
                };

                await ExecuteWithoutResponse(endpoint, PATCH, payload);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "DEACTIVATELANGUAGE", ex);
                throw;
            }
        }

        private async Task PatchDefaultLanguage()
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "PATCHDEFAULTLANGUAGE");

                var endpoint = $"/languages/{Guid.Empty}";

                var payload = new object[] {
                    new
                    {
                        op = "replace",
                        property_name = "name",
                        value = "Default (do not use)",
                    },
                    new
                    {
                        op = "replace",
                        property_name = "codename",
                        value = "default_do_not_use",
                    },
                };

                await ExecuteWithoutResponse(endpoint, PATCH, payload);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "PATCHDEFAULTLANGUAGE", ex);
                throw;
            }
        }

        private async Task PatchLanguage(CultureInfo culture)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "PATCHLANGUAGE", $"{culture.CultureName} ({culture.CultureCode})");

                var externalId = GetLanguageExternalId(culture.CultureGUID);
                var endpoint = $"/languages/external-id/{externalId}";

                var payload = new object[] {
                    new
                    {
                        op = "replace",
                        property_name = "name",
                        value = culture.CultureName.LimitedTo(CULTURE_MAXLENGTH),
                    },
                    new
                    {
                        op = "replace",
                        property_name = "codename",
                        value = culture.CultureCode.LimitedTo(CULTURE_MAXLENGTH),
                    },
                    new
                    {
                        op = "replace",
                        property_name = "is_active",
                        value = true,
                    },
                };

                await ExecuteWithoutResponse(endpoint, PATCH, payload);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "PATCHLANGUAGE", ex);
                throw;
            }
        }

        private async Task CreateLanguage(CultureInfo culture)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "CREATELANGUAGE", $"{culture.CultureName} ({culture.CultureCode})");

                var externalId = GetLanguageExternalId(culture.CultureGUID);
                var endpoint = $"/languages";

                var payload = new
                {
                    name = culture.CultureName.LimitedTo(CULTURE_MAXLENGTH),
                    codename = culture.CultureCode.LimitedTo(CULTURE_MAXLENGTH),
                    external_id = externalId,
                    is_active = true,
                    // Default language is always empty, and no fallback is used as a result
                    // If Kontent supported language without fallback, this needs to be updated
                    // fallback_language = new { id = Guid.Empty }
                };

                await ExecuteWithoutResponse(endpoint, HttpMethod.Post, payload);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "CREATELANGUAGE", ex);
                throw;
            }
        }

        #endregion
    }
}