using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading;

using CMS.DocumentEngine;
using CMS.EventLog;
using CMS.MediaLibrary;
using CMS.IO;
using CMS.SiteProvider;

namespace Kentico.KenticoCloudPublishing
{
    internal partial class AssetSync : SyncBase
    {        
        public AssetSync(SyncSettings settings) : base(settings)
        {
        }

        #region "External IDs"

        private static string GetAssetExternalId(string type, Guid guid)
        {
            return $"{type}|{guid}";
        }

        public static string GetAttachmentExternalId(Guid attachmentGuid)
        {
            return GetAssetExternalId("attachment", attachmentGuid);
        }

        public static string GetMediaFileExternalId(Guid mediaFileGuid)
        {
            return GetAssetExternalId("media", mediaFileGuid);
        }

        #endregion

        #region "Assets"

        public async Task<string> GetAssetUrl(string type, Guid guid, string fileName)
        {
            var externalId = GetAssetExternalId(type, guid);
            var asset = await GetAsset(externalId);
            if (asset == null)
            {
                return null;
            }

            return $"https://{Settings.AssetsDomain}/{Settings.ProjectId}/{asset.FileReference.Id}/{asset.FileName}";
        }

        private async Task<List<Guid>> GetAllAssetIds(string continuationToken = null)
        {
            var query = (continuationToken != null) ? "?continuationToken=" + continuationToken : "";
            var itemsEndpoint = $"/assets{query}";

            var response = await ExecuteWithResponse<AssetsResponse>(itemsEndpoint, HttpMethod.Get);
            if (response == null)
            {
                return new List<Guid>();
            }

            var ids = response.Assets
                .Select(asset => asset.Id);

            if ((response.Pagination != null) && !string.IsNullOrEmpty(response.Pagination.ContinuationToken))
            {
                var nextIds = await GetAllAssetIds(response.Pagination.ContinuationToken);
                ids = ids.Concat(nextIds);
            }

            return ids.ToList();
        }

        public async Task DeleteAllAssets(CancellationToken? cancellation)
        {
            try
            {
                SyncLog.Log("Deleting all assets");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "DELETEALLASSETS");

                var assetIds = await GetAllAssetIds();

                var index = 0;

                foreach (var assetId in assetIds)
                {
                    if (cancellation?.IsCancellationRequested == true)
                    {
                        return;
                    }

                    await DeleteAsset(assetId);

                    index++;

                    if (index % 10 == 0)
                    {
                        SyncLog.Log($"Deleted assets ({index}/{assetIds.Count})");
                    }
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "DELETEALLPAGES", ex);
                throw;
            }
        }

        private async Task DeleteAsset(Guid id)
        {
            var endpoint = $"/assets/{id}";

            await ExecuteWithoutResponse(endpoint, HttpMethod.Delete);
        }

        private async Task DeleteAsset(string externalId)
        {
            try
            {
                var endpoint = $"/assets/external-id/{externalId}";

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
        }

        private async Task<AssetData> GetAsset(string externalId)
        {
            var endpoint = $"/assets/external-id/{externalId}";

            return await ExecuteWithResponse<AssetData>(endpoint, HttpMethod.Get);
        }

        private async Task<FileReferenceData> UploadBinaryFile(byte[] data, string mimeType, string fileName)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var endpoint = $"/files/{fileName}";

            return await ExecuteUploadWithResponse<FileReferenceData>(endpoint, data, mimeType);
        }

        private async Task UpsertAsset(string externalId, string title, Guid fileReferenceId)
        {
            var endpoint = $"/assets/external-id/{externalId}";

            var payload = new
            {
                file_reference = new
                {
                    id = fileReferenceId,
                    type = "internal"
                },
                title,
                descriptions = new object[0]
            };

            await ExecuteWithoutResponse(endpoint, HttpMethod.Put, payload);
        }

        #endregion

        #region "Media files"

        public bool IsAtSynchronizedSite(MediaFileInfo mediaFile)
        {
            var siteId = SiteInfoProvider.GetSiteID(Settings.Sitename);

            return mediaFile.FileSiteID == siteId;
        }

        public bool IsAtSynchronizedSite(MediaLibraryInfo mediaLibrary)
        {
            var siteId = SiteInfoProvider.GetSiteID(Settings.Sitename);

            return mediaLibrary.LibrarySiteID == siteId;
        }

        public async Task DeleteMediaFile(MediaFileInfo mediaFile)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "DELETEMEDIAFILE", mediaFile.FileName);
                var externalId = GetMediaFileExternalId(mediaFile.FileGUID);

                await DeleteAsset(externalId);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "DELETEMEDIAFILE", ex, 0, mediaFile.FileName);
                throw;
            }
        }

        public async Task SyncAllMediaLibraries(CancellationToken? cancellation)
        {
            SyncLog.Log("Synchronizing media libraries");

            var mediaLibraries = MediaLibraryInfoProvider.GetMediaLibraries().OnSite(Settings.Sitename);

            var index = 0;

            foreach (var mediaLibrary in mediaLibraries)
            {
                if (cancellation?.IsCancellationRequested == true)
                {
                    return;
                }

                index++;

                SyncLog.Log($"Media library {mediaLibrary.LibraryDisplayName} ({index}/{mediaLibraries.Count})");

                await UpsertAllMediaFiles(mediaLibrary);
            }
        }

        public async Task UpsertAllMediaFiles(MediaLibraryInfo mediaLibrary)
        {
            try
            {
                SyncLog.Log($"Synchronizing files for media library {mediaLibrary.LibraryDisplayName}");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "UPSERTMEDIAFILES", mediaLibrary.LibraryDisplayName);

                var mediaFiles = MediaFileInfoProvider.GetMediaFiles()
                    .WhereEquals("FileLibraryID", mediaLibrary.LibraryID)
                    .BinaryData(false);

                var index = 0;

                foreach (var mediaFile in mediaFiles)
                {
                    index++;

                    SyncLog.Log($"Media file {mediaFile.FilePath} ({index}/{mediaFiles.Count})");

                    await SyncMediaFile(mediaFile);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "UPSERTMEDIAFILES", ex);
                throw;
            }
        }

        public async Task DeleteAllMediaFiles(MediaLibraryInfo mediaLibrary)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "DELETEMEDIAFILES", mediaLibrary.LibraryDisplayName);

                var mediaFiles = MediaFileInfoProvider.GetMediaFiles()
                    .WhereEquals("FileLibraryID", mediaLibrary.LibraryID)
                    .BinaryData(false)
                    .TypedResult;

                foreach (var mediaFile in mediaFiles)
                {
                    await DeleteMediaFile(mediaFile);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "DELETEMEDIAFILES", ex);
                throw;
            }
        }

        public async Task SyncMediaFile(MediaFileInfo mediaFile)
        {
            if (mediaFile == null)
            {
                throw new ArgumentNullException(nameof(mediaFile));
            }

            try
            {
                var fileName = mediaFile.FileName + mediaFile.FileExtension;

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "SYNCMEDIAFILE", fileName);

                var externalId = GetMediaFileExternalId(mediaFile.FileGUID);

                var existing = await GetAsset(externalId);

                // TODO - Consider detection by something more sophisticated than file size, but be careful, last modified may be off due to metadata changes
                if ((existing == null) || (mediaFile.FileSize != existing.Size))
                {
                    // We need to delete the asset first, otherwise upsert endpoint doesn't allow us to change file reference
                    await DeleteAsset(externalId);

                    // Upload new 
                    var filePath = MediaFileInfoProvider.GetMediaFilePath(mediaFile.FileLibraryID, mediaFile.FilePath);
                    var data = File.ReadAllBytes(filePath);
                    var fileReference = await UploadBinaryFile(data, mediaFile.FileMimeType, fileName);

                    await UpsertAsset(externalId, fileName, fileReference.Id);
                }
                else
                {
                    // Update metadata of existing
                    await UpsertAsset(externalId, fileName, existing.FileReference.Id);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "SYNCMEDIAFILE", ex);
                throw;
            }
        }

        #endregion

        #region "Attachments"

        public bool IsAtSynchronizedSite(AttachmentInfo attachment)
        {
            var siteId = SiteInfoProvider.GetSiteID(Settings.Sitename);

            return attachment.AttachmentSiteID == siteId;
        }

        public async Task DeleteAttachment(AttachmentInfo attachment)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "DELETEATTACHMENT", attachment.AttachmentName);

                var externalId = GetAttachmentExternalId(attachment.AttachmentGUID);

                await DeleteAsset(externalId);
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
                SyncLog.LogException("KenticoCloudPublishing", "DELETEATTACHMENT", ex, 0, attachment.AttachmentName);
                throw;
            }
        }

        public async Task DeleteAllAttachments(CancellationToken? cancellation, TreeNode node)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "DELETEATTACHMENTS", node.NodeAliasPath);

                var attachments = AttachmentInfoProvider.GetAttachments(node.DocumentID, false)
                    .ExceptVariants()
                    .TypedResult;

                foreach (var attachment in attachments)
                {
                    if (cancellation?.IsCancellationRequested == true)
                    {
                        return;
                    }

                    await DeleteAttachment(attachment);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "DELETEATTACHMENTS", ex);
                throw;
            }
        }

        public async Task SyncAllAttachments(CancellationToken? cancellation)
        {
            try
            {
                SyncLog.Log("Synchronizing page attachments");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "UPSERTALLATTACHMENTS");

                var attachments = AttachmentInfoProvider.GetAttachments()
                    .OnSite(Settings.Sitename)
                    .ExceptVariants()
                    .BinaryData(false);

                var index = 0;

                foreach (var attachment in attachments)
                {
                    if (cancellation?.IsCancellationRequested == true)
                    {
                        return;
                    }

                    index++;

                    SyncLog.Log($"Synchronizing attachment {attachment.AttachmentName} ({index}/{attachments.Count})");

                    await SyncAttachment(attachment);

                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "UPSERTALLATTACHMENTS", ex);
                throw;
            }
        }
        
        public async Task SyncAllAttachments(TreeNode node)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "SYNCALLATTACHMENTS", node.NodeAliasPath);

                var attachments = AttachmentInfoProvider.GetAttachments(node.DocumentID, false)
                    .ExceptVariants()
                    .TypedResult;

                foreach (var attachment in attachments)
                {
                    await SyncAttachment(attachment);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "SYNCALLATTACHMENTS", ex);
                throw;
            }
        }

        public async Task SyncAttachment(AttachmentInfo attachment)
        {
            if (attachment == null)
            {
                throw new ArgumentNullException(nameof(attachment));
            }

            // Do not synchronize variants
            if (attachment.IsVariant())
            {
                return;
            }

            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "SYNCATTACHMENT", attachment.AttachmentName);

                var externalId = GetAttachmentExternalId(attachment.AttachmentGUID);

                var existing = await GetAsset(externalId);

                // TODO - Consider detection by something more sophisticated than file size + name
                // but be careful, last modified may be off due to metadata changes
                if ((existing == null) || (attachment.AttachmentSize != existing.Size) || (attachment.AttachmentName != existing.FileName))
                {
                    // Upload new 
                    var data = AttachmentBinaryHelper.GetAttachmentBinary((DocumentAttachment)attachment);
                    var fileReference = await UploadBinaryFile(data, attachment.AttachmentMimeType, attachment.AttachmentName);

                    await UpsertAsset(externalId, attachment.AttachmentName, fileReference.Id);
                }
                else
                {
                    // Update metadata of existing
                    await UpsertAsset(externalId, attachment.AttachmentName, existing.FileReference.Id);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "SYNCATTACHMENT", ex);
                throw;
            }
        }

        #endregion      
    }
}
