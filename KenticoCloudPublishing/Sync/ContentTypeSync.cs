using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using CMS.DataEngine;
using CMS.DocumentEngine;
using CMS.EventLog;
using CMS.FormEngine;
using CMS.Relationships;
using CMS.SiteProvider;

namespace Kentico.KenticoCloudPublishing
{
    internal partial class ContentTypeSync : SyncBase
    {
        private PageSync _pageSync;

        public static readonly string[] UsedRelationshipNameColumns = new[]
        {
            "RelationshipName",
        };

        public static readonly string[] UsedPageTypeColumns = new[]
        {
            "ClassFormDefinition",
            // TODO - Include when update content type endpoints are implemented, until then it is not worth deleting and synchronizing all items
            // "ClassDisplayName"
        };

        public ContentTypeSync(SyncSettings settings, PageSync pageSync) : base(settings)
        {
            _pageSync = pageSync;
        }

        #region "External IDs"

        public static string GetFieldExternalId(Guid classGuid, Guid fieldGuid)
        {
            return $"field|{classGuid}|{fieldGuid}";
        }

        public static string GetSnippetExternalId(Guid guid)
        {
            return $"snippet|{guid}";
        }

        public static string GetPageTypeExternalId(Guid classGuid)
        {
            return $"node|{classGuid}";
        }

        #endregion

        #region "Relationships"

        public async Task SyncRelationships(CancellationToken? cancellation)
        {
            try
            {
                SyncLog.Log($"Synchronizing relationships");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "UPSERTRELATIONSHIPSSNIPPET");

                // TODO - handle with update when available in API
                await DeleteRelationshipsSnippet();
                await CreateRelationshipsSnippet();
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "UPSERTRELATIONSHIPSSNIPPET", ex);
                throw;
            }
        }

        private async Task DeleteRelationshipsSnippet()
        {
            try
            {
                SyncLog.Log($"Deleting relationships");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "DELETERELATIONSHIPSSNIPPET");

                var externalId = GetSnippetExternalId(RELATED_PAGES_GUID);
                var endpoint = $"/snippets/external-id/{HttpUtility.UrlEncode(externalId)}";

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
                SyncLog.LogException("KenticoCloudPublishing", "DELETERELATIONSHIPSSNIPPET", ex);
                throw;
            }
        }

        private async Task CreateRelationshipsSnippet()
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "CREATERELATIONSHIPSSNIPPET");

                var relationshipNames = RelationshipNameInfoProvider.GetRelationshipNames()
                    .WhereEqualsOrNull("RelationshipNameIsAdHoc", false)
                    .OnSite(Settings.Sitename);

                var externalId = GetSnippetExternalId(RELATED_PAGES_GUID);
                var endpoint = $"/snippets";

                var payload = new {
                    name = "Relationships",
                    external_id = externalId,
                    elements = relationshipNames.Select(relationshipName => new {
                        external_id = GetFieldExternalId(RELATED_PAGES_GUID, relationshipName.RelationshipGUID),
                        name = relationshipName.RelationshipName,
                        type = "modular_content",
                    }).ToList(),
                };

                await ExecuteWithoutResponse(endpoint, HttpMethod.Post, payload);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "CREATERELATIONSHIPSSNIPPET", ex);
                throw;
            }
        }

        #endregion

        #region "Fields"

        public const string UNSORTED_ATTACHMENTS = "UnsortedAttachments";
        public static Guid UNSORTED_ATTACHMENTS_GUID = new Guid("b21d2b8e-b793-413b-bce6-37461aa2963e");

        public const string RELATED_PAGES = "RelatedPages";
        public static Guid RELATED_PAGES_GUID = new Guid("61688369-a239-4c8c-93ff-af314a3489a2");

        public const string CATEGORIES = "Categories";
        public static Guid CATEGORIES_GUID = new Guid("c13a89d6-c5a9-4c6c-bceb-e27bf04e26d3");

        private static HashSet<string> SystemFields = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) {
            "DocumentName",
            "DocumentPublishFrom",
            "DocumentPublishTo"
        };

        public static List<IDataDefinitionItem> GetItemsToSync(string className)
        {
            var treeFormInfo = FormHelper.GetFormInfo("CMS.Tree", false);
            var documentFormInfo = FormHelper.GetFormInfo("CMS.Document", false);
            var formInfo = FormHelper.GetFormInfo(className, false);

            var systemCategory = new FormCategoryInfo
            {
                CategoryName = "System",
            };
            var treeSystemItems = treeFormInfo.GetFields<FormFieldInfo>().Where(item => SystemFields.Contains(item.Name));
            var documentSystemItems = documentFormInfo.GetFields<FormFieldInfo>().Where(item => SystemFields.Contains(item.Name));
            var typeItems = formInfo.ItemsList.Where(item =>
            {
                var field = item as FormFieldInfo;
                if (field != null)
                {
                    // Do not include the ID field
                    return !field.PrimaryKey;
                }
                return true;
            });

            return typeItems
                .Concat(new[] { systemCategory })
                .Concat(treeSystemItems)
                .Concat(documentSystemItems)
                .ToList();
        }

        private string GetElementType(string dataType)
        {
            switch (dataType)
            {
                case FieldDataType.Binary:
                    return "multiple_choice";

                case FieldDataType.Date:
                case FieldDataType.DateTime:
                    return "date_time";

                case FieldDataType.Decimal:
                case FieldDataType.Double:
                case FieldDataType.Integer:
                case FieldDataType.LongInteger:
                    return "number";

                case FieldDataType.File:
                case FieldDataType.DocAttachments:
                    return "asset";

                case FieldDataType.DocRelationships:
                    return "modular_content";

                case FieldDataType.Guid:
                case FieldDataType.Text:
                case FieldDataType.Xml:
                case FieldDataType.LongText:
                case FieldDataType.TimeSpan:
                default:
                    return "text";
                    //return "rich_text";
            }
        }

        #endregion

        #region "Synchronization"
       
        public bool IsAtSynchronizedSite(RelationshipNameInfo relationshipName)
        {
            var siteId = SiteInfoProvider.GetSiteID(Settings.Sitename);

            return RelationshipNameSiteInfoProvider.GetRelationshipNameSiteInfo(relationshipName.RelationshipNameId, siteId) != null;
        }


        public bool IsAtSynchronizedSite(DataClassInfo contentType)
        {
            var siteId = SiteInfoProvider.GetSiteID(Settings.Sitename);

            return ClassSiteInfoProvider.GetClassSiteInfo(contentType.ClassID, siteId) != null;
        }

        public async Task SyncAllContentTypes(CancellationToken? cancellation, bool syncPages)
        {
            SyncLog.Log("Synchronizing content types");

            var contentTypes = DataClassInfoProvider.GetClasses()
                .WhereEquals("ClassIsDocumentType", true)
                .WhereIn(
                    "ClassID",
                    ClassSiteInfoProvider.GetClassSites().OnSite(Settings.Sitename).Column("ClassID")
                );

            var index = 0;

            foreach (var contentType in contentTypes)
            {
                if (cancellation?.IsCancellationRequested == true)
                {
                    return;
                }

                index++;

                SyncLog.Log($"Synchronizing content type {contentType.ClassDisplayName} ({index}/{contentTypes.Count})");

                await SyncContentType(cancellation, contentType, syncPages);
            }
        }

        private async Task<ContentTypeData> GetContentType(DataClassInfo contentType)
        {
            try
            {
                var externalId = GetPageTypeExternalId(contentType.ClassGUID);
                var endpoint = $"/types/external-id/{HttpUtility.UrlEncode(externalId)}";

                return await ExecuteWithResponse<ContentTypeData>(endpoint, HttpMethod.Get);
            }
            catch (HttpException ex)
            {
                // 404 is OK, content type doesn't exist
                if (ex.GetHttpCode() == 404)
                {
                    return null;
                }

                throw;
            }
        }

        public async Task SyncContentType(CancellationToken? cancellation, DataClassInfo contentType, bool syncPages = true)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "SYNCCONTENTTYPE", contentType.ClassDisplayName);

                // TODO - handle with update when available in API and get rid of delete all items
                var cloudContentType = await GetContentType(contentType);
                if (cloudContentType != null)
                {
                    await _pageSync.DeleteAllItems(cancellation, cloudContentType.Id);

                    await DeleteContentType(contentType);
                }

                await CreateContentType(contentType);

                // Consider removing this with the above, it may take a while, maybe explicit synchronization from the module UI may be sufficient
                if (syncPages)
                {
                    await _pageSync.SyncAllPages(cancellation, contentType);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "SYNCCONTENTTYPE", ex);
                throw;
            }
        }

        public async Task DeleteContentType(DataClassInfo contentType)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "DELETECONTENTTYPE", contentType.ClassDisplayName);

                var externalId = GetPageTypeExternalId(contentType.ClassGUID);
                var endpoint = $"/types/external-id/{HttpUtility.UrlEncode(externalId)}";

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
                SyncLog.LogException("KenticoCloudPublishing", "DELETECONTENTTYPE", ex);
                throw;
            }
        }

        public async Task CreateContentType(DataClassInfo contentType)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "CREATECONTENTTYPE", contentType.ClassDisplayName);

                var endpoint = $"/types";

                var items = GetItemsToSync(contentType.ClassName);

                var fieldItems = items.Select(item =>
                {
                    var category = item as FormCategoryInfo;
                    if (category != null)
                    {
                        // TODO - when content groups are supported
                        //return new { name = category.CategoryName };
                        return null;
                    }

                    var field = item as FormFieldInfo;
                    if (field != null)
                    {
                        return new
                        {
                            external_id = GetFieldExternalId(contentType.ClassGUID, field.Guid),
                            name = field.Name, // We use field name, not caption to keep the code name same as the column name in the EMS
                            type = GetElementType(field.DataType),
                            taxonomy_group = (ExternalIdReference)null, // dummy
                            snippet = (ExternalIdReference)null, // dummy
                            options = (field.DataType == FieldDataType.Binary)
                                ? new[] {
                                        new MultipleChoiceElementOption { name = "True" },
                                        new MultipleChoiceElementOption { name = "False" }
                                }
                                : null,
                        };
                    }

                    return null;
                }).Where(x => x != null).ToList();

                var unsortedAttachmentsElement = new
                {
                    external_id = GetFieldExternalId(contentType.ClassGUID, UNSORTED_ATTACHMENTS_GUID),
                    name = UNSORTED_ATTACHMENTS,
                    type = "asset",
                    taxonomy_group = (ExternalIdReference)null, // dummy
                    snippet = (ExternalIdReference)null, // dummy
                    options = (MultipleChoiceElementOption[])null // dummy
                };
                var relatedPagesElement = new
                {
                    external_id = GetFieldExternalId(contentType.ClassGUID, RELATED_PAGES_GUID),
                    name = (string)null, // dummy
                    type = "snippet",
                    taxonomy_group = (ExternalIdReference)null, // dummy
                    snippet = new ExternalIdReference()
                    {
                        external_id = GetSnippetExternalId(RELATED_PAGES_GUID)
                    },
                    options = (MultipleChoiceElementOption[])null // dummy
                };
                var categoriesElement = new
                {
                    external_id = GetFieldExternalId(contentType.ClassGUID, CATEGORIES_GUID),
                    name = (string)null, // dummy
                    type = "taxonomy",
                    taxonomy_group = new ExternalIdReference()
                    {
                        external_id = TaxonomySync.GetTaxonomyExternalId(CATEGORIES_GUID)
                    },
                    snippet = (ExternalIdReference)null, // dummy
                    options = (MultipleChoiceElementOption[])null // dummy
                };

                var payload = new
                {
                    name = contentType.ClassDisplayName,
                    external_id = GetPageTypeExternalId(contentType.ClassGUID),
                    elements = fieldItems.Concat(new[] {
                        unsortedAttachmentsElement,
                        relatedPagesElement,
                        categoriesElement,
                    }).ToList(),
                };

                await ExecuteWithoutResponse(endpoint, HttpMethod.Post, payload);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "CREATECONTENTTYPE", ex);
                throw;
            }
        }

        #endregion
                       
        #region "Purge"

        private async Task<List<Guid>> GetAllContentTypeIds(string continuationToken = null)
        {
            var query = (continuationToken != null) ? "?continuationToken=" + HttpUtility.UrlEncode(continuationToken) : "";
            var itemsEndpoint = $"/types{query}";

            var response = await ExecuteWithResponse<ContentTypesResponse>(itemsEndpoint, HttpMethod.Get);
            if (response == null)
            {
                return new List<Guid>();
            }

            var ids = response.Types
                .Select(item => item.Id);

            if ((response.Pagination != null) && !string.IsNullOrEmpty(response.Pagination.ContinuationToken))
            {
                var nextIds = await GetAllContentTypeIds(response.Pagination.ContinuationToken);
                ids = ids.Concat(nextIds);
            }

            return ids.ToList();
        }

        public async Task DeleteAllContentTypes(CancellationToken? cancellation)
        {
            try
            {
                SyncLog.Log("Deleting content types");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "DELETEALLTYPES");

                var contentTypeIds = await GetAllContentTypeIds();

                foreach (var contentTypeId in contentTypeIds)
                {
                    if (cancellation?.IsCancellationRequested == true)
                    {
                        return;
                    }

                    await DeleteContentType(contentTypeId);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "DELETEALLTYPES", ex);
                throw;
            }
        }

        private async Task DeleteContentType(Guid id)
        {
            var endpoint = $"/types/{id}";

            await ExecuteWithoutResponse(endpoint, HttpMethod.Delete);
        }

        private async Task<List<Guid>> GetAllContentTypeSnippetIds(string continuationToken = null)
        {
            var query = (continuationToken != null) ? "?continuationToken=" + HttpUtility.UrlEncode(continuationToken) : "";
            var endpoint = $"/snippets{query}";

            var response = await ExecuteWithResponse<SnippetsResponse>(endpoint, HttpMethod.Get);
            if (response == null)
            {
                return new List<Guid>();
            }

            var ids = response.Snippets
                .Select(item => item.Id);

            if ((response.Pagination != null) && !string.IsNullOrEmpty(response.Pagination.ContinuationToken))
            {
                var nextIds = await GetAllContentTypeSnippetIds(response.Pagination.ContinuationToken);
                ids = ids.Concat(nextIds);
            }

            return ids.ToList();
        }

        public async Task DeleteAllContentTypeSnippets(CancellationToken? cancellation)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "DELETEALLSNIPPETS");

                var contentTypeSnippetIds = await GetAllContentTypeSnippetIds();

                foreach (var contentTypeSnippetId in contentTypeSnippetIds)
                {
                    if (cancellation?.IsCancellationRequested == true)
                    {
                        return;
                    }

                    await DeleteContentTypeSnippet(contentTypeSnippetId);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "DELETEALLSNIPPETS", ex);
                throw;
            }
        }

        private async Task DeleteContentTypeSnippet(Guid id)
        {
            var endpoint = $"/snippets/{id}";

            await ExecuteWithoutResponse(endpoint, HttpMethod.Delete);
        }

        #endregion
    }
}
