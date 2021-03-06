﻿using System;
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
using CMS.DataEngine;

namespace Kentico.EMS.Kontent.Publishing
{
    internal partial class AssetSync : SyncBase
    {
        private const int ASSET_TITLE_MAXLENGTH = 50;

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

        private string GetShortenedFileName(string fullFileName)
        {
            var name = Path.GetFileNameWithoutExtension(fullFileName);
            var extension = Path.GetExtension(fullFileName);

            return name.LimitedTo(ASSET_TITLE_MAXLENGTH - extension.Length) + extension;
        }

        public async Task<string> GetAssetUrl(string type, Guid guid)
        {
            var externalId = GetAssetExternalId(type, guid);
            var asset = await GetAsset(externalId);
            if (asset == null)
            {
                return null;
            }

            return $"https://{HttpUtility.UrlEncode(Settings.AssetsDomain)}/{Settings.ProjectId}/{asset.FileReference.Id}/{HttpUtility.UrlEncode(asset.FileName)}";
        }

        private async Task<List<Guid>> GetAllAssetIds(string continuationToken = null)
        {
            var endpoint = $"/assets";

            var response = await ExecuteWithResponse<AssetsResponse>(endpoint, HttpMethod.Get, continuationToken);
            if (response == null)
            {
                return new List<Guid>();
            }

            var ids = response.Assets
                .Select(asset => asset.Id)
                .ToList();

            if (
                (ids.Count > 0) &&
                (response.Pagination != null) &&
                !string.IsNullOrEmpty(response.Pagination.ContinuationToken) &&
                (response.Pagination.ContinuationToken != continuationToken)
            )
            {
                var nextIds = await GetAllAssetIds(response.Pagination.ContinuationToken);
                ids = ids.Concat(nextIds).ToList();
            }

            return ids;
        }

        public async Task DeleteAllAssets(CancellationToken? cancellation)
        {
            try
            {
                SyncLog.Log("Deleting all assets");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DELETEALLASSETS");

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
                SyncLog.LogException("KenticoKontentPublishing", "DELETEALLASSETS", ex);
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
                var endpoint = $"/assets/external-id/{HttpUtility.UrlEncode(externalId)}";

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
            var endpoint = $"/assets/external-id/{HttpUtility.UrlEncode(externalId)}";

            return await ExecuteWithResponse<AssetData>(endpoint, HttpMethod.Get);
        }

        private async Task<FileReferenceData> UploadBinaryFile(byte[] data, string mimeType, string fileName)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var endpoint = $"/files/{HttpUtility.UrlEncode(fileName)}";

            return await ExecuteUploadWithResponse<FileReferenceData>(endpoint, data, mimeType);
        }

        private async Task UpsertAsset(string externalId, string title, string description, Guid fileReferenceId)
        {
            var endpoint = $"/assets/external-id/{HttpUtility.UrlEncode(externalId)}";

            var payload = new
            {
                file_reference = new
                {
                    id = fileReferenceId,
                    type = "internal"
                },
                title = title.LimitedTo(ASSET_TITLE_MAXLENGTH),
                folder = new { id = Guid.Empty },
                descriptions = new []
                {
                    new
                    {
                        language = new {
                            id = Guid.Empty.ToString(),
                        },
                        description,
                    }
                }
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
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DELETEMEDIAFILE", mediaFile.FileName);
                var externalId = GetMediaFileExternalId(mediaFile.FileGUID);

                await DeleteAsset(externalId);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "DELETEMEDIAFILE", ex, 0, mediaFile.FileName);
                throw;
            }
        }

        public async Task SyncAllMediaLibraries(CancellationToken? cancellation)
        {
            SyncLog.Log("Synchronizing media libraries");

            var mediaLibraries = MediaLibraryInfo.Provider.Get().OnSite(Settings.Sitename);

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

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "UPSERTMEDIAFILES", mediaLibrary.LibraryDisplayName);

                var mediaFiles = MediaFileInfo.Provider.Get()
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
                SyncLog.LogException("KenticoKontentPublishing", "UPSERTMEDIAFILES", ex);
                throw;
            }
        }

        public async Task DeleteMediaFiles(CancellationToken? cancellation, ICollection<MediaFileInfo> mediaFiles, string info)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DELETEMEDIAFILES", info);

                var index = 0;

                foreach (var mediaFile in mediaFiles)
                {
                    if (cancellation?.IsCancellationRequested == true)
                    {
                        return;
                    }

                    index++;

                    SyncLog.Log($"Deleting media file {mediaFile.FileName} ({index}/{mediaFiles.Count})");

                    await DeleteMediaFile(mediaFile);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "DELETEMEDIAFILES", ex);
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
                var fullFileName = mediaFile.FileName + mediaFile.FileExtension;

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "SYNCMEDIAFILE", fullFileName);

                var externalId = GetMediaFileExternalId(mediaFile.FileGUID);

                var existing = await GetAsset(externalId);

                var fileName = GetShortenedFileName(fullFileName);
                var title = string.IsNullOrEmpty(mediaFile.FileTitle) ? fileName : mediaFile.FileTitle;

                // TODO - Consider detection by something more sophisticated than file size + name, but be careful, last modified may be off due to metadata changes
                if ((existing == null) || (mediaFile.FileSize != existing.Size) || (fileName != existing.FileName))
                {
                    // Upload new 
                    var filePath = MediaFileInfoProvider.GetMediaFilePath(mediaFile.FileLibraryID, mediaFile.FilePath);
                    var data = File.ReadAllBytes(filePath);
                    var fileReference = await UploadBinaryFile(data, mediaFile.FileMimeType, fileName);

                    await UpsertAsset(externalId, title, mediaFile.FileDescription, fileReference.Id);
                }
                else
                {
                    // Update metadata of existing
                    await UpsertAsset(externalId, title, mediaFile.FileDescription, existing.FileReference.Id);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "SYNCMEDIAFILE", ex);
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
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DELETEATTACHMENT", attachment.AttachmentName);

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
                SyncLog.LogException("KenticoKontentPublishing", "DELETEATTACHMENT", ex, 0, attachment.AttachmentName);
                throw;
            }
        }

        public async Task DeleteAttachments(CancellationToken? cancellation, ICollection<AttachmentInfo> attachments, string info)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DELETEATTACHMENTS", info);

                var index = 0;

                foreach (var attachment in attachments)
                {
                    if (cancellation?.IsCancellationRequested == true)
                    {
                        return;
                    }

                    index++;

                    SyncLog.Log($"Deleting attachment {attachment.AttachmentName} ({index}/{attachments.Count})");

                    await DeleteAttachment(attachment);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "DELETEATTACHMENTS", ex);
                throw;
            }
        }

        public async Task SyncAllAttachments(CancellationToken? cancellation)
        {
            try
            {
                SyncLog.Log("Synchronizing page attachments");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "SYNCALLATTACHMENTS");

                var attachments = AttachmentInfo.Provider.Get()
                    .OnSite(Settings.Sitename);

                await SyncAttachments(cancellation, attachments);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "SYNCALLATTACHMENTS", ex);
                throw;
            }
        }

        private async Task SyncAttachments(CancellationToken? cancellation, ObjectQuery<AttachmentInfo> attachments)
        {
            var processAttachments = attachments
                .Clone()
                .ExceptVariants()
                .BinaryData(false);

            var index = 0;

            foreach (var attachment in processAttachments)
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

        public async Task SyncAllAttachments(CancellationToken? cancellation, TreeNode node)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "SYNCALLATTACHMENTS", node.NodeAliasPath);

                var attachments = AttachmentInfoProvider.GetAttachments(node.DocumentID, false);

                await SyncAttachments(cancellation, attachments);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "SYNCALLATTACHMENTS", ex);
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
                var fullFileName = attachment.AttachmentName;

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "SYNCATTACHMENT", fullFileName);

                var externalId = GetAttachmentExternalId(attachment.AttachmentGUID);

                var fileName = GetShortenedFileName(fullFileName);
                var title = string.IsNullOrEmpty(attachment.AttachmentTitle) ? fileName : attachment.AttachmentTitle;

                var existing = await GetAsset(externalId);

                // TODO - Consider detection by something more sophisticated than file size + name, but be careful, last modified may be off due to metadata changes
                if ((existing == null) || (attachment.AttachmentSize != existing.Size) || (fileName != existing.FileName))
                {
                    // Upload new 
                    var data = AttachmentBinaryHelper.GetAttachmentBinary((DocumentAttachment)attachment);
                    var fileReference = await UploadBinaryFile(data, attachment.AttachmentMimeType, fileName);

                    await UpsertAsset(externalId, title, attachment.AttachmentDescription, fileReference.Id);
                }
                else
                {
                    // Update metadata of existing
                    await UpsertAsset(externalId, title, attachment.AttachmentDescription, existing.FileReference.Id);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "SYNCATTACHMENT", ex);
                throw;
            }
        }

        #endregion
    }
}
