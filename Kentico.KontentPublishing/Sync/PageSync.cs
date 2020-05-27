using System;
using System.Web;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

using CMS.DataEngine;
using CMS.DocumentEngine;
using CMS.FormEngine;
using CMS.EventLog;
using CMS.Relationships;
using CMS.SiteProvider;
using CMS.Localization;
using CMS.Taxonomy;

namespace Kentico.EMS.Kontent.Publishing
{
    internal class PageSync : SyncBase
    {
        private const int ITEM_NAME_MAXLENGTH = 50;

        private readonly AssetSync _assetSync;
        private readonly LinkTranslator _linkTranslator;
        private readonly TreeProvider _tree = new TreeProvider();

        public PageSync(SyncSettings settings, AssetSync assetSync) : base(settings)
        {
            _assetSync = assetSync;
            _linkTranslator = new LinkTranslator(settings, assetSync);
        }

        public bool IsAtSynchronizedSite(TreeNode node)
        {
            var siteId = SiteInfoProvider.GetSiteID(Settings.Sitename);

            return node.NodeSiteID == siteId;
        }

        public bool CanBePublished(TreeNode node)
        {
            return IsAtSynchronizedSite(node) && DocumentHelper.GetPublished(new NodeWithoutPublishFrom(node));
        }

        public TreeNode GetSourceDocument(int documentId)
        {
            return _tree.SelectSingleDocument(documentId);
        }

        public MultiDocumentQuery GetSourceDocuments()
        {
            return new MultiDocumentQuery().PublishedVersion();
        }

        public DocumentQuery GetSourceDocuments(string className)
        {
            return new DocumentQuery(className).PublishedVersion();
        }

        public static string GetPageExternalId(Guid nodeGuid)
        {
            return $"node|{nodeGuid}";
        }

        private string GetVariantEndpoint(TreeNode node)
        {
            var itemExternalId = GetPageExternalId(node.NodeGUID);
            var cultureInfo = CultureInfoProvider.GetCultureInfo(node.DocumentCulture);
            if (cultureInfo == null)
            {
                throw new InvalidOperationException($"Document culture '{node.DocumentCulture}' not found.");
            }

            var endpoint = $"/items/external-id/{HttpUtility.UrlEncode(itemExternalId)}/variants/codename/{HttpUtility.UrlEncode(cultureInfo.CultureCode)}";

            // Use this when endpoints by external ID are supported
            // var languageExternalId = LanguageSync.GetLanguageExternalId(cultureInfo.CultureGUID);
            // var endpoint = $"/items/external-id/{HttpUtility.UrlEncode(itemExternalId)}/variants/external-id/{HttpUtility.UrlEncode(languageExternalId)}";

            return endpoint;
        }

        private async Task UnpublishVariant(TreeNode node)
        {
            var variantEndpoint = GetVariantEndpoint(node);
            var endpoint = $"{variantEndpoint}/unpublish";

            await ExecuteWithoutResponse(endpoint, HttpMethod.Put);
        }

        public async Task UnpublishPage(TreeNode node)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "UNPUBLISHPAGE", $"{node.NodeAliasPath} - {node.DocumentCulture} ({node.NodeGUID})");

                if (node == null)
                {
                    throw new ArgumentNullException(nameof(node));
                }

                await CancelScheduling(node);
                await UnpublishVariant(node);
            }
            catch (HttpException ex)
            {
                switch (ex.GetHttpCode())
                {
                    case 404:
                    case 400:
                        // May not be there yet, 404 and 400 is OK
                        break;

                    default:
                        throw;
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "UNPUBLISHPAGE", ex, 0, $"{node.NodeAliasPath} - {node.DocumentCulture} ({node.NodeGUID})");
                throw;
            }
        }

        private async Task<bool> DeleteVariant(TreeNode node)
        {
            var endpoint = GetVariantEndpoint(node);

            try
            {
                await ExecuteWithoutResponse(endpoint, HttpMethod.Delete);

                return true;
            }
            catch (HttpException ex)
            {
                if (ex.GetHttpCode() == 404)
                {
                    // May not be there yet, 404 is OK but we want to report it
                    return false;
                }

                throw;
            }
        }

        public async Task DeletePages(CancellationToken? cancellation, ICollection<TreeNode> nodes, string info)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DELETEPAGES", info);

                var index = 0;

                foreach (var node in nodes)
                {
                    if (cancellation?.IsCancellationRequested == true)
                    {
                        return;
                    }

                    index++;

                    SyncLog.Log($"Deleting page {node.NodeAliasPath} ({index}/{nodes.Count})");

                    await DeletePage(node);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "DELETEPAGES", ex);
                throw;
            }
        }

        public async Task DeletePage(TreeNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DELETEPAGE", $"{node.NodeAliasPath} - {node.DocumentCulture} ({node.NodeGUID})");

                var variantDeleted = await DeleteVariant(node);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "DELETEPAGE", ex, 0, $"{node.NodeAliasPath} - {node.DocumentCulture} ({node.NodeGUID})");
                throw;
            }
        }

        public async Task SyncAllPages(CancellationToken? cancellation, DataClassInfo contentType = null, string path = null)
        {
            if (contentType == null)
            {
                throw new ArgumentNullException(nameof(contentType));
            }

            try
            {
                SyncLog.Log($"Synchronizing pages for content type {contentType.ClassDisplayName}");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "SYNCALLPAGES", contentType.ClassDisplayName);

                var documents = GetSourceDocuments(contentType.ClassName)
                    .OnSite(Settings.Sitename)
                    .AllCultures()
                    .PublishedVersion();

                var documentsOnPath = string.IsNullOrEmpty(path) ?
                    documents :
                    documents.Path(path, PathTypeEnum.Section);

                var index = 0;

                foreach (var node in documents)
                {
                    if (cancellation?.IsCancellationRequested == true)
                    {
                        return;
                    }

                    index++;

                    SyncLog.Log($"Synchronizing page { node.NodeAliasPath} - { node.DocumentCulture} ({ node.NodeGUID}) - {index}/{documents.Count}");

                    await SyncPage(node);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "SYNCALLPAGES", ex);
                throw;
            }
        }

        private async Task UpsertItem(TreeNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            var externalId = GetPageExternalId(node.NodeGUID);
            var endpoint = $"/items/external-id/{HttpUtility.UrlEncode(externalId)}";

            var pageType = DataClassInfoProvider.GetDataClassInfo(node.NodeClassName);
            if (pageType == null)
            {
                throw new InvalidOperationException($"Page type {node.NodeClassName} not found.");
            }

            var formInfo = FormHelper.GetFormInfo(node.ClassName, false);
            if (formInfo == null)
            {
                throw new InvalidOperationException($"Form info for {node.NodeClassName} not found.");
            }

            var name = node.NodeName.LimitedTo(ITEM_NAME_MAXLENGTH);
            if (string.IsNullOrEmpty(name))
            {
                name = node.NodeAliasPath.LimitedTo(ITEM_NAME_MAXLENGTH);
            }

            var payload = new
            {
                name,
                type = new
                {
                    external_id = ContentTypeSync.GetPageTypeExternalId(pageType.ClassGUID)
                },
                sitemap_locations = Array.Empty<object>()
            };

            await ExecuteWithoutResponse(endpoint, HttpMethod.Put, payload);
        }

        private List<object> GetRelationshipElements(TreeNode node)
        {
            var query = new DataQuery();

            var nodeRelationshipsQuery = RelationshipInfoProvider.GetRelationships()
                .WhereEquals("LeftNodeID", node.NodeID);

            // Get all the relationships for the document and also empty records for every existing relationships that the document is not using to send a complete set of relationship elements
            var allRelationshipsQuery = query
                .From(
@"
(" + query.IncludeDataParameters(nodeRelationshipsQuery.Parameters, nodeRelationshipsQuery.QueryText) + @") R
FULL OUTER JOIN CMS_RelationshipNameSite RNS ON R.RelationshipNameID = RNS.RelationshipNameID
LEFT JOIN CMS_RelationshipName RN ON RNS.RelationshipNameID = RN.RelationshipNameID
LEFT JOIN CMS_Tree T ON R.RightNodeID = T.NodeID
"
                )
                .Columns("NodeGUID", "RelationshipGUID")
                .WhereEqualsOrNull("RelationshipNameIsAdHoc", false)
                .WhereEquals("RNS.SiteID", node.NodeSiteID)
                .OrderBy("RelationshipOrder");

            var relationshipElements = allRelationshipsQuery
                .Result
                .Tables[0]
                .AsEnumerable()
                .GroupBy(row => (Guid)row["RelationshipGUID"])
                .Select(group => new {
                    element = new
                    {
                        external_id = ContentTypeSync.GetFieldExternalId(ContentTypeSync.RELATED_PAGES_GUID, group.Key)
                    },
                    value = (object)group
                        .Where(row => row["NodeGUID"] != DBNull.Value)
                        .Select(row => new { external_id = GetPageExternalId((Guid)row["NodeGUID"]) })
                        .ToList()
                })
                .Cast<object>()
                .ToList();

            return relationshipElements;
        }

        private async Task UpsertVariant(TreeNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            var externalId = GetPageExternalId(node.NodeGUID);
            var endpoint = GetVariantEndpoint(node);

            var contentType = DataClassInfoProvider.GetDataClassInfo(node.NodeClassName);
            if (contentType == null)
            {
                throw new InvalidOperationException($"Content type {node.NodeClassName} not found.");
            }

            var formInfo = FormHelper.GetFormInfo(node.ClassName, false);
            if (formInfo == null)
            {
                throw new InvalidOperationException($"Form info for {node.NodeClassName} not found.");
            }

            var fieldsToSync = ContentTypeSync.GetItemsToSync(node.NodeClassName).OfType<FormFieldInfo>();
            var fieldElements = await Task.WhenAll(
                fieldsToSync.Select(async (field) => new
                {
                    element = new
                    {
                        external_id = ContentTypeSync.GetFieldExternalId(contentType.ClassGUID, field.Guid)
                    },
                    value = await GetElementValue(node, field)
                })
            );
            var unsortedAttachmentsElement = new
            {
                element = new
                {
                    external_id = ContentTypeSync.GetFieldExternalId(contentType.ClassGUID, ContentTypeSync.UNSORTED_ATTACHMENTS_GUID)
                },
                value = (object)GetAttachmentGuids(node, null).Select(guid => new {
                    external_id = AssetSync.GetAttachmentExternalId(guid)
                })
            };
            var categoriesElement = new
            {
                element = new
                {
                    external_id = ContentTypeSync.GetFieldExternalId(contentType.ClassGUID, TaxonomySync.CATEGORIES_GUID)
                },
                value = (object)GetCategoryGuids(node).Select(guid => new {
                    external_id = TaxonomySync.GetCategoryTermExternalId(guid)
                }).ToList()
            };

            var relationshipElements = GetRelationshipElements(node);

            var payload = new
            {
                elements = fieldElements
                    .Concat(new[] {
                        unsortedAttachmentsElement,
                        categoriesElement,
                    })
                    .Concat(relationshipElements)
                    .ToList()
            };

            await ExecuteWithoutResponse(endpoint, HttpMethod.Put, payload, true);
        }

        private async Task PublishVariant(TreeNode node, DateTime? publishWhen)
        {
            var variantEndpoint = GetVariantEndpoint(node);
            var endpoint = $"{variantEndpoint}/publish";

            var isScheduledToFuture = publishWhen.HasValue && (publishWhen.Value > DateTime.Now.AddMinutes(1));
            var payload = isScheduledToFuture ?
                new
                {
                    scheduled_to = publishWhen.Value.ToUniversalTime()
                } :
                null;

            await ExecuteWithoutResponse(endpoint, HttpMethod.Put, payload);
        }

        private async Task CancelScheduling(TreeNode node)
        {
            try
            {
                var variantEndpoint = GetVariantEndpoint(node);
                var endpoint = $"{variantEndpoint}/cancel-scheduled-publish";

                await ExecuteWithoutResponse(endpoint, HttpMethod.Put);
            }
            catch (HttpException ex)
            {
                switch (ex.GetHttpCode())
                {
                    // The request may end up with 404 Not found if the variant wasn't imported yet
                    // Or it may end up with 400 Bad request if not scheduled
                    // Anyway, it is easier to fail and forget this way than finding out if current workflow step through GET endpoints for workflow and variant
                    case 400:
                    case 404:
                        break;

                    default:
                        throw;
                }
            }
        }

        private async Task CreateNewVersion(TreeNode node)
        {
            try
            {
                var externalId = GetPageExternalId(node.NodeGUID);
                var variantEndpoint = GetVariantEndpoint(node);
                var endpoint = $"{variantEndpoint}/new-version";

                await ExecuteWithoutResponse(endpoint, HttpMethod.Put);
            }
            catch (HttpException ex)
            {
                switch (ex.GetHttpCode())
                {
                    case 404:
                    case 400:
                        // The request may end up with 400 Bad request if the item is already there, but not published.
                        // The request may end up with 404 Not found if the variant wasn't imported yet

                        // It is easier to fail and forget this way than finding out if current workflow step through GET endpoints for workflow and variant
                        break;

                    default:
                        throw;
                }
            }
        }

        public async Task SyncAllCultures(int nodeId)
        {
            // Sync all language versions of the document
            var nodes = GetSourceDocuments()
                .OnSite(Settings.Sitename)
                .WhereEquals("NodeID", nodeId)
                .PublishedVersion()
                .WithCoupledColumns()
                .AllCultures();

            foreach (var node in nodes)
            {
                if (IsAtSynchronizedSite(node) && CanBePublished(node))
                {
                    await SyncPage(node);
                }
            }
        }

        public async Task SyncPageWithAllData(CancellationToken? cancellation, TreeNode node)
        {
            if (cancellation?.IsCancellationRequested == true)
            {
                return;
            }

            await _assetSync.SyncAllAttachments(cancellation, node);
            await SyncPage(node);
        }

        public async Task SyncPage(TreeNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (!CanBePublished(node))
            {
                // Not published pages should be deleted in KC, but we never delete their attachments, attachments always reflect state in the CMS_Attachment table
                await DeletePage(node);
                return;
            }

            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "SYNCPAGE", $"{node.NodeAliasPath} ({node.DocumentCulture}) {node.NodeGUID}");

                await UpsertItem(node);

                await CancelScheduling(node);
                await CreateNewVersion(node);
                await UpsertVariant(node);

                await PublishVariant(node, node.DocumentPublishFrom);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "SYNCPAGE", ex, 0, $"{node.NodeAliasPath} - {node.DocumentCulture} ({node.NodeGUID})");
                throw;
            }
        }

        private IList<Guid> GetRelatedNodeGuids(TreeNode node, FormFieldInfo relationshipsField)
        {
            var relationshipName = RelationshipNameInfoProvider.GetAdHocRelationshipNameCodeName(node.ClassName, relationshipsField);
            var relationshipNameInfo = RelationshipNameInfoProvider.GetRelationshipNameInfo(relationshipName);
            if (relationshipNameInfo == null)
            {
                return new List<Guid>();
            }

            var guids = RelationshipInfoProvider.GetRelationships()
                .Columns("NodeGUID")
                .Source(
                    s => s.LeftJoin(new QuerySourceTable("CMS_Tree"), "RightNodeID", "NodeID")
                )
                .WhereEquals("LeftNodeID", node.NodeID)
                .WhereEquals("RelationshipID", relationshipNameInfo.RelationshipNameId)
                .OrderBy("RelationshipOrder")
                .GetListResult<Guid>();

            return guids;
        }

        private IList<Guid> GetAttachmentGuids(TreeNode node, FormFieldInfo attachmentsField)
        {
            var guidsQuery = AttachmentInfoProvider.GetAttachments(node.DocumentID, false)
                .ExceptVariants()
                .Columns("AttachmentGUID")
                .OrderBy("AttachmentOrder");

            var narrowedQuery = (attachmentsField != null)
                ? guidsQuery.WhereEquals("AttachmentGroupGUID", attachmentsField.Guid)
                : guidsQuery.WhereTrue("AttachmentIsUnsorted");

            var guids = narrowedQuery
                .GetListResult<Guid>();

            return guids;
        }

        private IList<Guid> GetCategoryGuids(TreeNode node)
        {
            var siteId = SiteInfoProvider.GetSiteID(Settings.Sitename);

            var guids = DocumentCategoryInfoProvider.GetDocumentCategories(node.DocumentID)
                .OnSite(Settings.Sitename, true)
                .WhereNull("CategoryUserID")
                .TypedResult
                .Select(category => category.CategoryGUID)
                .ToList();

            return guids;
        }

        private async Task<object> GetElementValue(TreeNode node, FormFieldInfo field)
        {
            var value = node.GetValue(field.Name);

            switch (field.DataType)
            {
                case FieldDataType.Boolean:
                    if (value == null)
                    {
                        return null;
                    }
                    return (bool)value
                        ? new[] { new { codename = "true" } }
                        : new[] { new { codename = "false" } };

                case FieldDataType.Date:
                case FieldDataType.DateTime:
                    if (value == null)
                    {
                        return null;
                    }
                    return ((DateTime)value).ToUniversalTime();

                case FieldDataType.Decimal:
                case FieldDataType.Double:
                case FieldDataType.Integer:
                case FieldDataType.LongInteger:
                    return Convert.ToString(Convert.ToDecimal(value));

                case FieldDataType.File:
                    if (value == null)
                    {
                        return Array.Empty<object>();
                    }
                    else
                    {
                        return new[] {
                            new { external_id = AssetSync.GetAttachmentExternalId((Guid)value) }
                        };
                    }

                case FieldDataType.DocAttachments:
                    return GetAttachmentGuids(node, field).Select(guid => new {
                        external_id = AssetSync.GetAttachmentExternalId(guid)
                    });

                case FieldDataType.DocRelationships:
                    return GetRelatedNodeGuids(node, field).Select(guid => new {
                        external_id = GetPageExternalId(guid)
                    });

                case FieldDataType.LongText:
                    return await _linkTranslator.TranslateLinks(Convert.ToString(value));

                case FieldDataType.Binary:
                    throw new NotSupportedException("Binary field type is not supported");

                case FieldDataType.Guid:
                case FieldDataType.Text:
                case FieldDataType.Xml:
                case FieldDataType.TimeSpan:
                default:
                    return Convert.ToString(value);
            }
        }

        public async Task SyncAllPages(CancellationToken? cancellation)
        {
            SyncLog.Log("Synchronizing pages");

            var contentTypes = DataClassInfoProvider.GetClasses()
                .WhereEquals("ClassIsDocumentType", true)
                .OnSite(Settings.Sitename);

            var index = 0;

            foreach (var contentType in contentTypes)
            {
                if (cancellation?.IsCancellationRequested == true) {
                    return;
                }

                index++;

                SyncLog.Log($"Synchronizing pages for content type {contentType.ClassDisplayName} ({index}/{contentTypes.Count})");

                await SyncAllPages(cancellation, contentType);
            }
        }

        public async Task DeleteAllItems(CancellationToken? cancellation, Guid? contentTypeId = null)
        {
            try
            {
                SyncLog.Log("Deleting all content items");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DELETEALLITEMS");

                var itemIds = await GetAllItemIds(contentTypeId);

                var index = 0;

                foreach (var itemId in itemIds)
                {
                    if (cancellation?.IsCancellationRequested == true)
                    {
                        return;
                    }

                    await DeleteItem(itemId);

                    index++;

                    if (index % 10 == 0)
                    {
                        SyncLog.Log($"Deleted content items ({index}/{itemIds.Count})");
                    }
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "DELETEALLITEMS", ex);
                throw;
            }
        }
        
        private async Task DeleteItem(Guid itemId)
        {
            var endpoint = $"/items/{itemId}";

            await ExecuteWithoutResponse(endpoint, HttpMethod.Delete);
        }

        private async Task<List<Guid>> GetAllItemIds(Guid? contentTypeId, string continuationToken = null)
        {
            var endpoint = $"/items";

            var response = await ExecuteWithResponse<ItemsResponse>(endpoint, HttpMethod.Get, continuationToken);
            if (response == null)
            {
                return new List<Guid>();
            }

            var items = contentTypeId.HasValue ?
                response.Items.Where(item => item.Type.Id == contentTypeId.Value) :
                response.Items;

            var ids = items.Select(item => item.Id).ToList();

            if (
                (ids.Count > 0) &&
                (response.Pagination != null) &&
                !string.IsNullOrEmpty(response.Pagination.ContinuationToken) &&
                (response.Pagination.ContinuationToken != continuationToken)
            )
            {
                var nextIds = await GetAllItemIds(contentTypeId, response.Pagination.ContinuationToken);
                ids = ids.Concat(nextIds).ToList();
            }

            return ids;
        }
    }
}
