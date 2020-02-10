using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using CMS.EventLog;
using CMS.SiteProvider;
using CMS.Taxonomy;

namespace Kentico.EMS.Kontent.Publishing
{
    internal partial class TaxonomySync : SyncBase
    {
        public TaxonomySync(SyncSettings settings) : base(settings)
        {
        }

        #region "External IDs"

        public static string GetTaxonomyExternalId(Guid guid)
        {
            return $"taxonomy|{guid}";
        }

        public static string GetCategoryTermExternalId(Guid categoryGuid)
        {
            return $"term|category|{categoryGuid}";
        }

        #endregion

        #region "Synchronization"

        public static readonly string[] UsedCategoryColumns = new[]
        {
            "CategoryName",
        };

        public bool IsAtSynchronizedSite(CategoryInfo category)
        {
            // Synchronize global categories
            if (category.CategorySiteID == 0)
            {
                return true;
            }

            var siteId = SiteInfoProvider.GetSiteID(Settings.Sitename);

            return category.CategorySiteID == siteId;
        }
        
        public async Task SyncCategories(CancellationToken? cancellation = null)
        {
            try
            {
                SyncLog.Log("Synchronizing categories");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "UPSERTCATEGORIESTAXONOMY");

                // TODO - consider patch
                await DeleteCategoriesTaxonomy();
                await CreateCategoriesTaxonomy();
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "UPSERTCATEGORIESTAXONOMY", ex);
                throw;
            }
        }

        private async Task DeleteCategoriesTaxonomy()
        {
            try
            {
                SyncLog.Log("Deleting categories");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DELETECATEGORIESTAXONOMY");

                var externalId = GetTaxonomyExternalId(ContentTypeSync.CATEGORIES_GUID);
                var endpoint = $"/taxonomies/external-id/{HttpUtility.UrlEncode(externalId)}";

                await ExecuteWithoutResponse(endpoint, HttpMethod.Delete);
            }
            catch (HttpException ex)
            {
                if (ex.GetHttpCode() == 404)
                {
                    // May not be there yet, 404 is OK
                    return;
                }

                throw;
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "DELETECATEGORIESTAXONOMY", ex);
                throw;
            }
        }

        private List<CategoryTerm> GetCategoryTerms(IEnumerable<CategoryInfo> categories, int parentId = 0)
        {
            return categories.Where(c => c.CategoryParentID == parentId).Select(category => new CategoryTerm()
            {
                name = category.CategoryName,
                external_id = GetCategoryTermExternalId(category.CategoryGUID),
                terms = GetCategoryTerms(categories, category.CategoryID)
            }).ToList();
        }
            
        private async Task CreateCategoriesTaxonomy()
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "CREATECATEGORIESTAXONOMY");

                var categories = CategoryInfoProvider.GetCategories()
                    .OnSite(Settings.Sitename, true)
                    // Global first
                    .OrderBy("CategorySiteID", "CategoryOrder")
                    .TypedResult;

                var externalId = GetTaxonomyExternalId(ContentTypeSync.CATEGORIES_GUID);
                var endpoint = $"/taxonomies";

                var payload = new
                {
                    name = "Categories",
                    external_id = externalId,
                    terms = GetCategoryTerms(categories),
                };

                await ExecuteWithoutResponse(endpoint, HttpMethod.Post, payload);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "CREATECATEGORIESTAXONOMY", ex);
                throw;
            }
        }

        #endregion

        #region "Purge"
        
        private async Task<List<Guid>> GetAllTaxonomyIds(string continuationToken = null)
        {
            var query = (continuationToken != null) ? "?continuationToken=" + HttpUtility.UrlEncode(continuationToken) : "";
            var endpoint = $"/taxonomies{query}";

            var response = await ExecuteWithResponse<TaxonomyResponse>(endpoint, HttpMethod.Get);
            if (response == null)
            {
                return new List<Guid>();
            }

            var ids = response.Taxonomies.Select(item => item.Id);

            return ids.ToList();
        }

        public async Task DeleteAllTaxonomies(CancellationToken? cancellation)
        {
            try
            {
                SyncLog.Log("Deleting all taxonomies");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DELETEALLTAXONOMIES");

                var taxonomyIds = await GetAllTaxonomyIds();

                foreach (var taxonomyId in taxonomyIds)
                {
                    if (cancellation?.IsCancellationRequested == true)
                    {
                        return;
                    }

                    await DeleteTaxonomy(taxonomyId);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "DELETEALLTAXONOMIES", ex);
                throw;
            }
        }

        private async Task DeleteTaxonomy(Guid id)
        {
            var endpoint = $"/taxonomies/{id}";

            await ExecuteWithoutResponse(endpoint, HttpMethod.Delete);
        }

        #endregion
    }
}