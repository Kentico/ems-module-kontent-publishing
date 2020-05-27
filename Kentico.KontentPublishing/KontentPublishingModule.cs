using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CMS;
using CMS.DataEngine;
using CMS.DocumentEngine;
using CMS.MediaLibrary;
using CMS.Relationships;
using CMS.Taxonomy;

[assembly: RegisterModule(typeof(Kentico.EMS.Kontent.Publishing.KontentPublishingModule))]

namespace Kentico.EMS.Kontent.Publishing
{
    public class KontentPublishingModule : Module
    {
        #region "Initialization"

        public const string MODULE_NAME = "Kentico.KontentPublishing";

        private readonly ContentTypeSync _contentTypeSync;
        private readonly PageSync _pageSync;
        private readonly AssetSync _assetSync;
        private readonly TaxonomySync _taxonomySync;
        private readonly LanguageSync _languageSync;

        private readonly SyncSettings _settings;

        public KontentPublishingModule() : base(MODULE_NAME)
        {
            _settings = new SyncSettings();
            _settings.LoadFromConfig();

            if (_settings.IsValid())
            {
                _assetSync = new AssetSync(_settings);
                _languageSync = new LanguageSync(_settings);
                _pageSync = new PageSync(_settings, _assetSync);
                _contentTypeSync = new ContentTypeSync(_settings);
                _taxonomySync = new TaxonomySync(_settings);
            }
        }

        protected override void OnInit()
        {
            base.OnInit();

            if (_contentTypeSync != null)
            {
                DocumentTypeInfo.TYPEINFODOCUMENTTYPE.Events.Insert.After += PageTypeCreated;
                DocumentTypeInfo.TYPEINFODOCUMENTTYPE.Events.Update.Before += PageTypeUpdating;
                DocumentTypeInfo.TYPEINFODOCUMENTTYPE.Events.Delete.After += PageTypeDeleted;

                ClassSiteInfo.TYPEINFO.Events.Insert.After += ClassSiteChanged;
                ClassSiteInfo.TYPEINFO.Events.Delete.After += ClassSiteChanged;

                RelationshipNameInfo.TYPEINFO.Events.Insert.After += RelationshipNameCreated;
                RelationshipNameInfo.TYPEINFO.Events.Update.Before += RelationshipNameUpdating;
                RelationshipNameInfo.TYPEINFO.Events.Delete.Before += RelationshipsNameDeleting;

                RelationshipNameSiteInfo.TYPEINFO.Events.Insert.After += RelationshipNameSiteChanged;
                RelationshipNameSiteInfo.TYPEINFO.Events.Delete.After += RelationshipNameSiteChanged;
            }
            if (_taxonomySync != null)
            {
                CategoryInfo.TYPEINFO.Events.Insert.After += CategoryCreated;
                CategoryInfo.TYPEINFO.Events.Update.Before += CategoryUpdating;
                CategoryInfo.TYPEINFO.Events.Delete.After += CategoryDeleted;
            }
            if (_pageSync != null)
            {
                DocumentEvents.Insert.After += PageUpdated;
                DocumentEvents.InsertNewCulture.After += PageUpdated;
                DocumentEvents.Update.Before += PageUpdating;
                DocumentEvents.Update.After += PageUpdated;
                DocumentEvents.Delete.Before += PageDeleting;
                DocumentEvents.Move.Before += PageMoving;

                WorkflowEvents.Publish.Before += PagePublishing;

                RelationshipInfo.TYPEINFO.Events.Insert.After += RelationshipUpdated;
                RelationshipInfo.TYPEINFO.Events.Update.After += RelationshipUpdated;
                RelationshipInfo.TYPEINFO.Events.Delete.After += RelationshipUpdated;

                RelationshipInfo.TYPEINFO_ADHOC.Events.Insert.After += RelationshipUpdated;
                RelationshipInfo.TYPEINFO_ADHOC.Events.Update.After += RelationshipUpdated;
                RelationshipInfo.TYPEINFO_ADHOC.Events.Delete.After += RelationshipUpdated;

                DocumentCategoryInfo.TYPEINFO.Events.Insert.After += CategoryUpdated;
                DocumentCategoryInfo.TYPEINFO.Events.Delete.After += CategoryUpdated;
            }
            if (_assetSync != null)
            {
                AttachmentInfo.TYPEINFO.Events.ChangeOrder.After += AttachmentChangedOrder;

                AttachmentInfo.TYPEINFO.Events.Insert.After += AttachmentInserted;
                AttachmentInfo.TYPEINFO_TEMPORARY.Events.Insert.After += TemporaryAttachmentUpdated;

                AttachmentInfo.TYPEINFO.Events.Update.After += AttachmentUpdated;
                AttachmentInfo.TYPEINFO_TEMPORARY.Events.Update.After += TemporaryAttachmentUpdated;

                AttachmentInfo.TYPEINFO.Events.Delete.After += AttachmentDeleted;

                MediaFileInfo.TYPEINFO.Events.Insert.After += MediaFileUpdated;
                MediaFileInfo.TYPEINFO.Events.Update.After += MediaFileUpdated;
                MediaFileInfo.TYPEINFO.Events.Delete.After += MediaFileDeleted;

                MediaLibraryInfo.TYPEINFO.Events.Delete.Before += MediaLibraryDeleting;
            }
        }

        private void RunSynchronization(Func<Task> action)
        {
            KontentSyncWorker.Current.Enqueue(
                () => Task.Run(async () => await action()).Wait()
            );
        }

        public bool IsConfigurationValid()
        {
            return _settings.IsValid();
        }

        #endregion

        #region "Categories"

        private void CategoryCreated(object sender, ObjectEventArgs e)
        {
            if ((e.Object is CategoryInfo category) && _taxonomySync.IsAtSynchronizedSite(category))
            {
                RunSynchronization(async () => await _taxonomySync.SyncCategories());
            }
        }

        private void CategoryDeleted(object sender, ObjectEventArgs e)
        {
            if ((e.Object is CategoryInfo category) && _taxonomySync.IsAtSynchronizedSite(category))
            {
                RunSynchronization(async () => await _taxonomySync.SyncCategories());
            }
        }

        private void CategoryUpdating(object sender, ObjectEventArgs e)
        {
            if ((e.Object is CategoryInfo category) && _taxonomySync.IsAtSynchronizedSite(category))
            {
                if (category.AnyItemChanged(TaxonomySync.UsedCategoryColumns))
                {
                    e.CallWhenFinished(
                        () => RunSynchronization(async () => await _taxonomySync.SyncCategories())
                    );
                }
            }
        }

        private void CategoryUpdated(object sender, ObjectEventArgs e)
        {
            if (e.Object is DocumentCategoryInfo category)
            {
                var node = _pageSync.GetSourceDocument(category.DocumentID);
                if ((node != null) && _pageSync.IsAtSynchronizedSite(node) && _pageSync.CanBePublished(node))
                {
                    RunSynchronization(async () => await _pageSync.SyncPage(node));
                }
            }
        }

        #endregion

        #region "Relationships"

        private void RelationshipNameCreated(object sender, ObjectEventArgs e)
        {
            if ((e.Object is RelationshipNameInfo relationshipName) && _contentTypeSync.IsAtSynchronizedSite(relationshipName))
            {
                RunSynchronization(async () => await _contentTypeSync.SyncRelationships());
            }
        }

        private void RelationshipNameUpdating(object sender, ObjectEventArgs e)
        {
            if ((e.Object is RelationshipNameInfo relationshipName) && _contentTypeSync.IsAtSynchronizedSite(relationshipName))
            {
                if (relationshipName.AnyItemChanged(ContentTypeSync.UsedRelationshipNameColumns))
                {
                    e.CallWhenFinished(
                        () => RunSynchronization(async () => await _contentTypeSync.SyncRelationships())
                    );
                }
            }
        }

        private void RelationshipsNameDeleting(object sender, ObjectEventArgs e)
        {
            if ((e.Object is RelationshipNameInfo relationshipName) && _contentTypeSync.IsAtSynchronizedSite(relationshipName))
            {
                e.CallWhenFinished(
                    () => RunSynchronization(async () => await _contentTypeSync.SyncRelationships())
                );
            }
        }

        private void RelationshipNameSiteChanged(object sender, ObjectEventArgs e)
        {
            if ((e.Object is RelationshipNameSiteInfo relationshipNameSite) && _contentTypeSync.IsSynchronizedSite(relationshipNameSite.SiteID))
            {
                RunSynchronization(async () => await _contentTypeSync.SyncRelationships());
            }
        }

        private void RelationshipUpdated(object sender, ObjectEventArgs e)
        {
            if (e.Object is RelationshipInfo relationship)
            {
                RunSynchronization(async () => await _pageSync.SyncAllCultures(relationship.LeftNodeId));
            }
        }

        #endregion

        #region "Attachments"

        private void AttachmentDeleted(object sender, ObjectEventArgs e)
        {
            if ((e.Object is AttachmentInfo attachment) && _assetSync.IsAtSynchronizedSite(attachment))
            {
                var node = _pageSync.GetSourceDocument(attachment.AttachmentDocumentID);
                if (node != null)
                {
                    RunSynchronization(async () =>
                        {
                            // TODO - Remove when deleting referenced attachments with external ID is allowed
                            if ((node.DocumentWorkflowStepID == 0) && _pageSync.CanBePublished(node))
                            {
                                // We need to delete and recreate page when deleting attachment for a page without workflow, because asset can be deleted only when not referenced
                                // Result is the same, but KC just wants it this way
                                await _pageSync.DeletePage(node);
                                await _assetSync.DeleteAttachment(attachment);
                                await _pageSync.SyncPage(node);
                                return;
                            }

                            await _assetSync.DeleteAttachment(attachment);
                        }
                    );
                }
            }
        }

        private void TemporaryAttachmentUpdated(object sender, ObjectEventArgs e)
        {
            if ((e.Object is AttachmentInfo attachment) && _assetSync.IsAtSynchronizedSite(attachment))
            {
                RunSynchronization(async () =>
                {
                    await _assetSync.SyncAttachment(attachment);
                });
            }
        }

        private void AttachmentInserted(object sender, ObjectEventArgs e)
        {
            if ((e.Object is AttachmentInfo attachment) && _assetSync.IsAtSynchronizedSite(attachment))
            {
                var node = _pageSync.GetSourceDocument(attachment.AttachmentDocumentID);
                if (node != null)
                {
                    RunSynchronization(async () =>
                    {
                        await _assetSync.SyncAttachment(attachment);

                        if ((node.DocumentWorkflowStepID == 0) && _pageSync.CanBePublished(node))
                        {
                            await _pageSync.SyncPage(node);
                        }
                    });
                }
            }
        }

        private void AttachmentUpdated(object sender, ObjectEventArgs e)
        {
            if ((e.Object is AttachmentInfo attachment) && _assetSync.IsAtSynchronizedSite(attachment))
            {
                var node = _pageSync.GetSourceDocument(attachment.AttachmentDocumentID);
                if (node != null)
                {
                    RunSynchronization(async () =>
                        {
                            // The attachment needs to be deleted because upsert doesn't allow updating file reference
                            // TODO - Remove when updating referenced attachments with external ID is allowed
                            if ((node.DocumentWorkflowStepID == 0) && _pageSync.CanBePublished(node))
                            {
                                // We need to delete and recreate page when deleting attachment for a page without workflow, because asset can be deleted only when not referenced
                                // Result is the same, but KC just wants it this way
                                await _pageSync.DeletePage(node);
                                await _assetSync.DeleteAttachment(attachment);
                                await _assetSync.SyncAttachment(attachment);
                                await _pageSync.SyncPage(node);
                                return;
                            }

                            await _assetSync.SyncAttachment(attachment);
                        }
                    );
                }
            }
        }

        private void AttachmentChangedOrder(object sender, ObjectChangeOrderEventArgs e)
        {
            if ((e.Object is AttachmentInfo attachment) && _assetSync.IsAtSynchronizedSite(attachment))
            {
                var node = _pageSync.GetSourceDocument(attachment.AttachmentDocumentID);
                if ((node != null) && (node.DocumentWorkflowStepID == 0) && _pageSync.CanBePublished(node))
                {
                    RunSynchronization(async () => await _pageSync.SyncPage(node));
                }
            }
        }
        
        #endregion

        #region "Media files"

        private void MediaLibraryDeleting(object sender, ObjectEventArgs e)
        {
            if ((e.Object is MediaLibraryInfo mediaLibrary) && _assetSync.IsAtSynchronizedSite(mediaLibrary))
            {
                var mediaFiles = MediaFileInfoProvider.GetMediaFiles()
                    .WhereEquals("FileLibraryID", mediaLibrary.LibraryID)
                    .BinaryData(false)
                    .TypedResult
                    .ToList();

                RunSynchronization(async () => await _assetSync.DeleteMediaFiles(null, mediaFiles, mediaLibrary.LibraryDisplayName));
            }
        }

        private void MediaFileDeleted(object sender, ObjectEventArgs e)
        {
            if ((e.Object is MediaFileInfo mediaFile) && _assetSync.IsAtSynchronizedSite(mediaFile))
            {
                RunSynchronization(async () => await _assetSync.DeleteMediaFile(mediaFile));
            }
        }

        private void MediaFileUpdated(object sender, ObjectEventArgs e)
        {
            if ((e.Object is MediaFileInfo mediaFile) && _assetSync.IsAtSynchronizedSite(mediaFile))
            {
                RunSynchronization(async () => await _assetSync.SyncMediaFile(mediaFile));
            }
        }

        #endregion

        #region "Page types"

        private void PageTypeCreated(object sender, ObjectEventArgs e)
        {
            if ((e.Object is DocumentTypeInfo contentType) && _contentTypeSync.IsAtSynchronizedSite(contentType))
            {
                RunSynchronization(async () => await _contentTypeSync.CreateContentType(contentType));
            }
        }

        private void PageTypeDeleted(object sender, ObjectEventArgs e)
        {
            if ((e.Object is DocumentTypeInfo contentType) && _contentTypeSync.IsAtSynchronizedSite(contentType))
            {
                RunSynchronization(async () => await _contentTypeSync.DeleteContentType(contentType));
            }
        }

        private void PageTypeUpdating(object sender, ObjectEventArgs e)
        {
            if ((e.Object is DocumentTypeInfo contentType) && _contentTypeSync.IsAtSynchronizedSite(contentType))
            {
                if (contentType.AnyItemChanged(ContentTypeSync.UsedPageTypeColumns))
                {
                    e.CallWhenFinished(
                        () => RunSynchronization(async () => await _contentTypeSync.SyncContentType(contentType))
                    );
                }
            }
        }

        private void ClassSiteChanged(object sender, ObjectEventArgs e)
        {
            if ((e.Object is ClassSiteInfo classSite) && _contentTypeSync.IsSynchronizedSite(classSite.SiteID))
            {
                var contentType = DataClassInfoProvider.GetDataClassInfo(classSite.ClassID);
                if (contentType?.ClassIsDocumentType == true)
                {
                    RunSynchronization(async () => await _contentTypeSync.SyncContentType(contentType));
                }
            }
        }

        #endregion

        #region "Pages"

        private void PagePublishing(object sender, WorkflowEventArgs e)
        {
            // Publishing a page includes deleting all its attachments, but assets can't be physically deleted when they are referenced from an item
            // That is why we need to delete the page here with its attachments
            // The publishing will recreate the page anyway
            var node = e.Document;

            if (_pageSync.IsAtSynchronizedSite(node))
            {
                RunSynchronization(async () => await _pageSync.DeletePage(node));
            }
        }

        private void PageUpdating(object sender, DocumentEventArgs e)
        {
            var oldCanBePublished = false;
            var node = e.Node;

            node.ExecuteWithOriginalData(() =>
            {
                oldCanBePublished = _pageSync.CanBePublished(node);
            });

            var canBePublished = _pageSync.CanBePublished(node);

            if (oldCanBePublished && !canBePublished)
            {
                // Unpublished
                e.CallWhenFinished(() =>
                {
                    RunSynchronization(async () => await _pageSync.DeletePage(node));
                });
            }
        }

        private void PageUpdated(object sender, DocumentEventArgs e)
        {
            var node = e.Node;

            if (_pageSync.IsAtSynchronizedSite(node))
            {
                RunSynchronization(async () => await _pageSync.SyncPage(node));
            }
        }

        private void PageDeleting(object sender, DocumentEventArgs e)
        {
            var node = e.Node;

            if (_pageSync.IsAtSynchronizedSite(node))
            {
                var attachments = AttachmentInfoProvider.GetAttachments(node.DocumentID, false)
                    .ExceptVariants()
                    .TypedResult
                    .ToList();

                RunSynchronization(async () => {
                    await _pageSync.DeletePage(node);
                    await _assetSync.DeleteAttachments(null, attachments, node.NodeAliasPath);
                });
            }
        }

        private void PageMoving(object sender, DocumentEventArgs e)
        {
            var node = e.Node;
            var target = e.TargetParentNode;

            bool isAtSynchronizedSite = _pageSync.IsAtSynchronizedSite(node);
            bool targetIsAtSynchronizedSite = _pageSync.IsAtSynchronizedSite(target);

            if (isAtSynchronizedSite != targetIsAtSynchronizedSite)
            {
                if (!targetIsAtSynchronizedSite)
                {
                    // Delete subtree when moving away from synchronized site 
                    var subtree = _pageSync.GetSourceDocuments()
                        .OnSite(node.NodeSiteName)
                        .Path(node.NodeAliasPath, PathTypeEnum.Section)
                        .AllCultures()
                        .TypedResult
                        .ToList();

                    var documentIds = subtree.Select(n => n.DocumentID).ToList();
                    var attachments = AttachmentInfoProvider.GetAttachments()
                        .WhereIn("AttachmentDocumentID", documentIds)
                        .BinaryData(false)
                        .ExceptVariants()
                        .TypedResult
                        .ToList();

                    RunSynchronization(async () =>
                    {
                        var info = node.NodeAliasPath + "%";

                        await _pageSync.DeletePages(null, subtree, info);
                        await _assetSync.DeleteAttachments(null, attachments, info);
                    });
                }
                else
                {
                    // Sync subtree when moving to synchronized site 
                    e.CallWhenFinished(() =>
                    {
                        var subtree = _pageSync.GetSourceDocuments()
                            .OnSite(node.NodeSiteName)
                            .Path(node.NodeAliasPath, PathTypeEnum.Section)
                            .AllCultures()
                            .WithCoupledColumns()
                            .TypedResult
                            .ToList();

                        RunSynchronization(async () =>
                        {
                            foreach (var page in subtree)
                            {
                                await _pageSync.SyncPageWithAllData(null, page);
                            }
                        });
                    });
                }
            }
        }

        #endregion

        #region "Sync methods"

        public async Task SyncMediaLibraries(CancellationToken? cancellation)
        {
            await _assetSync.SyncAllMediaLibraries(cancellation);
        }

        public async Task SyncRelationships()
        {
            await _contentTypeSync.SyncRelationships();
        }

        public async Task SyncCategories()
        {
            await _taxonomySync.SyncCategories();
        }

        public async Task SyncContentTypes(CancellationToken? cancellation)
        {
            await _contentTypeSync.SyncAllContentTypes(cancellation);
        }

        public async Task SyncAttachments(CancellationToken? cancellation)
        {
            await _assetSync.SyncAllAttachments(cancellation);
        }

        public async Task SyncLanguages()
        {
            await _languageSync.SyncCultures();
        }

        public async Task SyncPages(CancellationToken? cancellation)
        {
            await _pageSync.SyncAllPages(cancellation);
        }

        public async Task SyncAll(CancellationToken? cancellation)
        {
            await SyncMediaLibraries(cancellation);
            await SyncRelationships();
            await SyncCategories();
            await SyncContentTypes(cancellation);
            await SyncAttachments(cancellation);
            await SyncLanguages();
            await SyncPages(cancellation);
        }

        #endregion

        #region "Delete methods"

        public async Task DeleteAssets(CancellationToken? cancellation)
        {
            await _assetSync.DeleteAllAssets(cancellation);
        }

        public async Task DeleteContentTypeSnippets(CancellationToken? cancellation)
        {
            await _contentTypeSync.DeleteAllContentTypeSnippets(cancellation);
        }

        public async Task DeleteTaxonomies(CancellationToken? cancellation)
        {
            await _taxonomySync.DeleteAllTaxonomies(cancellation);
        }

        public async Task DeleteItems(CancellationToken? cancellation)
        {
            await _pageSync.DeleteAllItems(cancellation);
        }

        public async Task DeleteContentTypes(CancellationToken? cancellation)
        {
            await _contentTypeSync.DeleteAllContentTypes(cancellation);
        }

        public async Task DeleteAll(CancellationToken? cancellation)
        {
            await DeleteItems(cancellation);
            await DeleteContentTypes(cancellation);
            await DeleteContentTypeSnippets(cancellation);
            await DeleteTaxonomies(cancellation);
            await DeleteAssets(cancellation);
        }

        #endregion
    }
}
