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
        private const int TAXONOMY_NAME_MAXLENGTH = 50;
        private const int TAXONOMY_CODENAME_MAXLENGTH = 60;

        public const string CATEGORIES = "Categories";
        public static Guid CATEGORIES_GUID = new Guid("c13a89d6-c5a9-4c6c-bceb-e27bf04e26d3");
        
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
            "CategoryDisplayName",
            "CategoryName",
            "CategoryParentID",
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
        
        public async Task SyncCategories()
        {
            try
            {
                SyncLog.Log("Synchronizing categories");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "SYNCCATEGORIES");

                // TODO - consider patch
                await DeleteCategoriesTaxonomy();
                await CreateCategoriesTaxonomy();
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "SYNCCATEGORIES", ex);
                throw;
            }
        }

        private async Task DeleteCategoriesTaxonomy()
        {
            try
            {
                SyncLog.Log("Deleting categories");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DELETECATEGORIESTAXONOMY");

                var externalId = GetTaxonomyExternalId(CATEGORIES_GUID);
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
                Name = category.CategoryDisplayName.LimitedTo(TAXONOMY_NAME_MAXLENGTH),
                Codename = category.CategoryName.ToLower().LimitedTo(TAXONOMY_CODENAME_MAXLENGTH),
                ExternalId = GetCategoryTermExternalId(category.CategoryGUID),
                Terms = GetCategoryTerms(categories, category.CategoryID)
            }).ToList();
        }
            
        private async Task CreateCategoriesTaxonomy()
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "CREATECATEGORIESTAXONOMY");

                var categories = CategoryInfoProvider.GetCategories()
                    .OnSite(Settings.Sitename, true)
                    .WhereNull("CategoryUserID")
                    // Global first
                    .OrderBy("CategorySiteID", "CategoryOrder")
                    .TypedResult;

                var externalId = GetTaxonomyExternalId(CATEGORIES_GUID);
                var endpoint = $"/taxonomies";

                var payload = new
                {
                    name = "Categories",
                    codename = CATEGORIES.ToLower(),
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
            var endpoint = $"/taxonomies";

            var response = await ExecuteWithResponse<TaxonomyResponse>(endpoint, HttpMethod.Get, continuationToken);
            if (response == null)
            {
                return new List<Guid>();
            }

            var ids = response.Taxonomies
                .Select(item => item.Id).ToList();

            if (
                (ids.Count > 0) &&
                (response.Pagination != null) &&
                !string.IsNullOrEmpty(response.Pagination.ContinuationToken) &&
                (response.Pagination.ContinuationToken != continuationToken)
            )
            {
                var nextIds = await GetAllTaxonomyIds(response.Pagination.ContinuationToken);
                ids = ids.Concat(nextIds).ToList();
            }

            return ids;
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