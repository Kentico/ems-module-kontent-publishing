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
using CMS.Helpers;
using CMS.Relationships;
using CMS.SiteProvider;

namespace Kentico.EMS.Kontent.Publishing
{
    internal partial class ContentTypeSync : SyncBase
    {
        private PageSync _pageSync;

        public static readonly string[] UsedRelationshipNameColumns = new[]
        {
            "RelationshipDisplayName",
            "RelationshipName",
        };

        public static readonly string[] UsedPageTypeColumns = new[]
        {
            "ClassDisplayName",
            "ClassName",
            "ClassFormDefinition",
        };
        private readonly int ELEMENT_MAXLENGTH;

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

        public static string GetGroupExternalId(Guid classGuid, Guid guid)
        {
            return $"group|{classGuid}|{guid}";
        }

        #endregion

        #region "Relationships"

        private async Task<SnippetData> GetSnippet(Guid guid)
        {
            try
            {
                var externalId = GetSnippetExternalId(guid);
                var endpoint = $"/snippets/external-id/{HttpUtility.UrlEncode(externalId)}";

                return await ExecuteWithResponse<SnippetData>(endpoint, HttpMethod.Get);
            }
            catch (HttpException ex)
            {
                // 404 is OK, snippet doesn't exist
                if (ex.GetHttpCode() == 404)
                {
                    return null;
                }

                throw;
            }
        }

        public async Task SyncRelationships(CancellationToken? cancellation)
        {
            try
            {
                SyncLog.Log($"Synchronizing relationships");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "UPSERTRELATIONSHIPSSNIPPET");

                var kontentSnippet = await GetSnippet(RELATED_PAGES_GUID);
                if (kontentSnippet != null)
                {
                    await PatchRelationshipsSnippet(kontentSnippet);
                }
                else
                {
                    await CreateRelationshipsSnippet();
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "UPSERTRELATIONSHIPSSNIPPET", ex);
                throw;
            }
        }

        private async Task DeleteRelationshipsSnippet()
        {
            try
            {
                SyncLog.Log($"Deleting relationships");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DELETERELATIONSHIPSSNIPPET");

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
                SyncLog.LogException("KenticoKontentPublishing", "DELETERELATIONSHIPSSNIPPET", ex);
                throw;
            }
        }

        private IEnumerable<object> GetRelationshipElements()
        {
            var relationshipNames = RelationshipNameInfoProvider.GetRelationshipNames()
                .WhereEqualsOrNull("RelationshipNameIsAdHoc", false)
                .OnSite(Settings.Sitename);

            var elements = relationshipNames.Select(relationshipName => new
            {
                external_id = GetFieldExternalId(RELATED_PAGES_GUID, relationshipName.RelationshipGUID),
                name = relationshipName.RelationshipDisplayName.LimitedTo(ELEMENT_MAXLENGTH),
                codename = relationshipName.RelationshipName.ToLower().LimitedTo(ELEMENT_MAXLENGTH),
                type = "modular_content",
            }).ToList();

            return elements;
        }

        private async Task CreateRelationshipsSnippet()
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "CREATERELATIONSHIPSSNIPPET");

                var externalId = GetSnippetExternalId(RELATED_PAGES_GUID);
                var endpoint = $"/snippets";

                var payload = new
                {
                    name = "Relationships",
                    external_id = externalId,
                    elements = GetRelationshipElements(),
                };

                await ExecuteWithoutResponse(endpoint, HttpMethod.Post, payload);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "CREATERELATIONSHIPSSNIPPET", ex);
                throw;
            }
        }

        private async Task PatchRelationshipsSnippet(SnippetData kontentSnippet)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "PATCHRELATIONSHIPSSNIPPET");

                var externalId = GetSnippetExternalId(RELATED_PAGES_GUID);
                var endpoint = $"/snippets/external-id/{HttpUtility.UrlEncode(externalId)}";

                var removeAllExisting = kontentSnippet.Elements.Select(element => new
                {
                    op = "remove",
                    path = $"/elements/id:{element.Id}"
                });
                var addAllCurrent = GetRelationshipElements().Select(element => new
                {
                    op = "addInto",
                    path = "/elements",
                    value = element
                });
                var payload = removeAllExisting.AsEnumerable<object>().Concat(addAllCurrent).ToList();
                if (payload.Count > 0)
                {
                    await ExecuteWithoutResponse(endpoint, PATCH, payload);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "PATCHRELATIONSHIPSSNIPPET", ex);
                throw;
            }
        }

        #endregion

        #region "Fields"

        public const string UNSORTED_ATTACHMENTS = "UnsortedAttachments";
        public const string UNSORTED_ATTACHMENTS_NAME = "Attachments";
        public static Guid UNSORTED_ATTACHMENTS_GUID = new Guid("b21d2b8e-b793-413b-bce6-37461aa2963e");

        public const string RELATED_PAGES = "RelatedPages";
        public const string RELATED_PAGES_NAME = "Related pages";
        public static Guid RELATED_PAGES_GUID = new Guid("61688369-a239-4c8c-93ff-af314a3489a2");

        public const string CONTENT = "Content";
        public static Guid CONTENT_GUID = new Guid("68faa0d2-f17f-4e88-b019-64d526a626ec");


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

        public async Task SyncAllContentTypes(CancellationToken? cancellation)
        {
            SyncLog.Log("Synchronizing content types");

            var contentTypes = DataClassInfoProvider.GetClasses()
                .WhereEquals("ClassIsDocumentType", true)
                .WhereIn(
                    "ClassID",
                    ClassSiteInfoProvider.GetClassSites().OnSite(Settings.Sitename).Column("ClassID")
                )
                .WhereNotEquals("ClassName", "CMS.Root");

            var index = 0;

            foreach (var contentType in contentTypes)
            {
                if (cancellation?.IsCancellationRequested == true)
                {
                    return;
                }

                index++;

                SyncLog.Log($"Synchronizing content type {contentType.ClassDisplayName} ({index}/{contentTypes.Count})");

                await SyncContentType(cancellation, contentType);
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

        public async Task SyncContentType(CancellationToken? cancellation, DataClassInfo contentType)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "SYNCCONTENTTYPE", contentType.ClassDisplayName);

                var kontentContentType = await GetContentType(contentType);
                if (kontentContentType != null)
                {
                    await PatchContentType(kontentContentType, contentType);
                }
                else
                {
                    await CreateContentType(contentType);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "SYNCCONTENTTYPE", ex);
                throw;
            }
        }

        public async Task DeleteContentType(DataClassInfo contentType)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DELETECONTENTTYPE", contentType.ClassDisplayName);

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
                SyncLog.LogException("KenticoKontentPublishing", "DELETECONTENTTYPE", ex);
                throw;
            }
        }

        private IEnumerable<object> GetContentTypeGroups(DataClassInfo contentType)
        {
            return new[]
            {
                new
                {
                    name = CONTENT,
                    external_id = GetGroupExternalId(contentType.ClassGUID, CONTENT_GUID)
                },
                new
                {
                    name = UNSORTED_ATTACHMENTS_NAME,
                    external_id = GetGroupExternalId(contentType.ClassGUID, UNSORTED_ATTACHMENTS_GUID)
                },
                new
                {
                    name = TaxonomySync.CATEGORIES,
                    external_id = GetGroupExternalId(contentType.ClassGUID, TaxonomySync.CATEGORIES_GUID)
                },
                new
                {
                    name = RELATED_PAGES_NAME,
                    external_id = GetGroupExternalId(contentType.ClassGUID, RELATED_PAGES_GUID)
                },
            };
        }

        private IEnumerable<object> GetContentTypeElements(DataClassInfo contentType)
        {
            var items = GetItemsToSync(contentType.ClassName);
            object pendingCategory = null;

            var fieldItems = items.SelectMany(item =>
            {
                var category = item as FormCategoryInfo;
                if (category != null)
                {
                    pendingCategory = new
                    {
                        external_id = $"group|{contentType.ClassGUID}|{ValidationHelper.GetCodeName(category.CategoryName)}",
                        type = "guidelines",
                        guidelines = $"<h2>{category.CategoryName}</h2>",
                        content_group = new ExternalIdReference()
                        {
                            external_id = GetGroupExternalId(contentType.ClassGUID, CONTENT_GUID)
                        }
                    };
                    return new object[0];
                }

                var field = item as FormFieldInfo;
                if (field != null)
                {
                    var element = new
                    {
                        external_id = GetFieldExternalId(contentType.ClassGUID, field.Guid),
                        name = GetElementName(field).LimitedTo(ELEMENT_MAXLENGTH),
                        codename = field.Name.ToLower().LimitedTo(ELEMENT_MAXLENGTH),
                        guidelines = GetElementGuidelines(field),
                        is_required = !field.AllowEmpty,
                        type = GetElementType(field.DataType),
                        options = (field.DataType == FieldDataType.Binary)
                            ? new[] {
                                new MultipleChoiceElementOption { name = "True" },
                                new MultipleChoiceElementOption { name = "False" }
                            }
                            : null,
                        content_group = new ExternalIdReference()
                        {
                            external_id = GetGroupExternalId(contentType.ClassGUID, CONTENT_GUID)
                        }
                    };

                    if (pendingCategory != null)
                    {
                        // Only add category if there are some elements inside it
                        var elementWithCategory = new[] { pendingCategory, element };
                        pendingCategory = null;
                        return elementWithCategory;
                    }
                    return new[] { element };
                }

                return null;
            }).Where(x => x != null).ToList();

            var unsortedAttachmentsElement = new
            {
                external_id = GetFieldExternalId(contentType.ClassGUID, UNSORTED_ATTACHMENTS_GUID),
                name = UNSORTED_ATTACHMENTS_NAME,
                codename = UNSORTED_ATTACHMENTS.ToLower(),
                type = "asset",
                content_group = new ExternalIdReference()
                {
                    external_id = GetGroupExternalId(contentType.ClassGUID, UNSORTED_ATTACHMENTS_GUID)
                },
            };
            var relatedPagesElement = new
            {
                external_id = GetFieldExternalId(contentType.ClassGUID, RELATED_PAGES_GUID),
                name = RELATED_PAGES_NAME,
                codename = RELATED_PAGES.ToLower(),
                type = "snippet",
                snippet = new ExternalIdReference()
                {
                    external_id = GetSnippetExternalId(RELATED_PAGES_GUID)
                },
                content_group = new ExternalIdReference()
                {
                    external_id = GetGroupExternalId(contentType.ClassGUID, RELATED_PAGES_GUID)
                },
            };
            var categoriesElement = new
            {
                external_id = GetFieldExternalId(contentType.ClassGUID, TaxonomySync.CATEGORIES_GUID),
                name = TaxonomySync.CATEGORIES,
                codename = TaxonomySync.CATEGORIES.ToLower(),
                type = "taxonomy",
                taxonomy_group = new ExternalIdReference()
                {
                    external_id = TaxonomySync.GetTaxonomyExternalId(TaxonomySync.CATEGORIES_GUID)
                },
                content_group = new ExternalIdReference()
                {
                    external_id = GetGroupExternalId(contentType.ClassGUID, TaxonomySync.CATEGORIES_GUID)
                },
            };

            var allElements = fieldItems
                .AsEnumerable<object>()
                .Concat(new object[] {
                    unsortedAttachmentsElement,
                    relatedPagesElement,
                    categoriesElement,
                })
                .ToList();

            return allElements;
        }

        private object GetElementGuidelines(FormFieldInfo field)
        {
            return field.GetPropertyValue(FormFieldPropertyEnum.ExplanationText);
        }

        private string GetElementName(FormFieldInfo field)
        {
            switch (field.Name.ToLower())
            {
                case "documentname":
                    return "Document name";

                case "documentpublishfrom":
                    return "Publish from";

                case "documentpublishto":
                    return "Publish to";

                default:
                    {
                        if (!string.IsNullOrEmpty(field.Caption))
                        {
                            return field.Caption;
                        }
                        return field.Name;
                    }
            }
        }

        public async Task CreateContentType(DataClassInfo contentType)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "CREATECONTENTTYPE", contentType.ClassDisplayName);

                var endpoint = $"/types";


                var payload = new
                {
                    name = contentType.ClassDisplayName.LimitedTo(ELEMENT_MAXLENGTH),
                    code_name = contentType.ClassName.LimitedTo(ELEMENT_MAXLENGTH),
                    external_id = GetPageTypeExternalId(contentType.ClassGUID),
                    content_groups = GetContentTypeGroups(contentType),
                    elements = GetContentTypeElements(contentType),
                };

                await ExecuteWithoutResponse(endpoint, HttpMethod.Post, payload);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "CREATECONTENTTYPE", ex);
                throw;
            }
        }

        public async Task PatchContentType(ContentTypeData kontentContentType, DataClassInfo contentType)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "PATCHCONTENTTYPE", contentType.ClassDisplayName);

                var externalId = GetPageTypeExternalId(contentType.ClassGUID);
                var endpoint = $"/types/external-id/{HttpUtility.UrlEncode(externalId)}";

                var removeAllExistingElements = kontentContentType.Elements.Select(element => new
                {
                    op = "remove",
                    path = $"/elements/id:{element.Id}"
                });
                var removeAllExistingGroups = kontentContentType.ContentGroups.Select(group => new
                {
                    op = "remove",
                    path = $"/content_groups/id:{group.Id}"
                });

                var addAllCurrentElements = GetContentTypeElements(contentType).Select(element => new
                {
                    op = "addInto",
                    path = "/elements",
                    value = element
                });
                var addAllCurrentGroups = GetContentTypeGroups(contentType).Select(group => new
                {
                    op = "addInto",
                    path = "/content_groups",
                    value = group
                });

                var payload = removeAllExistingElements
                    .AsEnumerable<object>()
                    .Concat(removeAllExistingGroups)
                    .Concat(addAllCurrentGroups)
                    .Concat(addAllCurrentElements)
                    .ToList();

                if (payload.Count > 0)
                {
                    await ExecuteWithoutResponse(endpoint, PATCH, payload);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "PATCHCONTENTTYPE", ex);
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
                .Select(item => item.Id)
                .ToList();

            if (
                (ids.Count > 0) &&
                (response.Pagination != null) &&
                !string.IsNullOrEmpty(response.Pagination.ContinuationToken) &&
                (response.Pagination.ContinuationToken != continuationToken)
            )
            {
                var nextIds = await GetAllContentTypeIds(response.Pagination.ContinuationToken);
                ids = ids.Concat(nextIds).ToList();
            }

            return ids;
        }

        public async Task DeleteAllContentTypes(CancellationToken? cancellation)
        {
            try
            {
                SyncLog.Log("Deleting content types");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DELETEALLTYPES");

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
                SyncLog.LogException("KenticoKontentPublishing", "DELETEALLTYPES", ex);
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
                .Select(item => item.Id).ToList();

            if (
                (ids.Count > 0) &&
                (response.Pagination != null) &&
                !string.IsNullOrEmpty(response.Pagination.ContinuationToken) &&
                (response.Pagination.ContinuationToken != continuationToken)
            )
            {
                var nextIds = await GetAllContentTypeSnippetIds(response.Pagination.ContinuationToken);
                ids = ids.Concat(nextIds).ToList();
            }

            return ids;
        }

        public async Task DeleteAllContentTypeSnippets(CancellationToken? cancellation)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DELETEALLSNIPPETS");

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
                SyncLog.LogException("KenticoKontentPublishing", "DELETEALLSNIPPETS", ex);
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
