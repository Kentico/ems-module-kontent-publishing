using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

using CMS.EventLog;
using CMS.MediaLibrary;

namespace Kentico.EMS.Kontent.Publishing
{
    internal partial class AssetFolderSync : SyncBase
    {
        public const string ATTACHMENTS = "Attachments";
        public const string MEDIA = "Media";

        public AssetFolderSync(SyncSettings settings) : base(settings)
        {
        }

        #region "External IDs"

        public static string GetAttachmentsFolderExternalId()
        {
            return $"assetfolder|{ATTACHMENTS}";
        }

        public static string GetMediaFolderExternalId()
        {
            return $"assetfolder|{MEDIA}";
        }

        public static string GetMediaFolderExternalId(Guid mediaLibraryGuid, string path)
        {
            return $"assetfolder|{mediaLibraryGuid}|{path}";
        }

        #endregion

        #region "Synchronization"

        private List<FolderData> GetFoldersToSynchronize()
        {
            return new List<FolderData>
            {
                new FolderData
                {
                    Name = "Document attachments",
                    ExternalId = GetAttachmentsFolderExternalId(),
                    Folders = new List<FolderData>(),
                },
                new FolderData
                {
                    Name = "Media libraries",
                    ExternalId = GetMediaFolderExternalId(),
                    Folders = GetMediaLibraryFolders(),
                },
            };
        }

        private List<FolderData> GetMediaLibraryFolders()
        {
            var mediaLibraries = MediaLibraryInfoProvider.GetMediaLibraries().OnSite(Settings.Sitename);

            return mediaLibraries.TypedResult.Select(mediaLibrary => new FolderData {
                Name = mediaLibrary.LibraryDisplayName,
                ExternalId = GetMediaFolderExternalId(mediaLibrary.LibraryGUID, "/"),
                Folders = GetMediaLibraryFolders(mediaLibrary),
            }).ToList();
        }

        private List<FolderData> GetMediaLibraryFolders(MediaLibraryInfo mediaLibrary)
        {
            var files = MediaFileInfoProvider.GetMediaFiles()
                .WhereEquals("FileLibraryID", mediaLibrary.LibraryID)
                .Column("FilePath");

            var folderPaths = files.GetListResult<string>()
                .Select(path => Path.GetDirectoryName(path))
                .Distinct()
                .Where(path => !string.IsNullOrEmpty(path));

            var parsedPaths = folderPaths.Select(path => new
            {
                Parent = Path.GetDirectoryName(path),
                Name = Path.GetFileName(path),
            });

            var groupedByParent = parsedPaths.GroupBy(parsed => parsed.Parent).ToDictionary(
                group => group.Key,
                group => group.Select(parsed => parsed.Name).ToList()
            );

            return GetFolders(mediaLibrary.LibraryGUID, "", groupedByParent);
        }

        private List<FolderData> GetFolders(Guid mediaLibraryGuid, string parent, Dictionary<string, List<string>> groupedByParent)
        {
            if (groupedByParent.TryGetValue(parent, out var folders))
            {
                return folders.Select(folder => {
                    var path = parent + "/" + folder;
                    return new FolderData
                    {
                        Name = folder,
                        ExternalId = GetMediaFolderExternalId(mediaLibraryGuid, path),
                        Folders = GetFolders(mediaLibraryGuid, path, groupedByParent),
                    };
                }).ToList();
            }
            return new List<FolderData>();
        }

        public async Task SyncAllFolders()
        {
            try
            {
                SyncLog.Log("Synchronizing asset folders");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "SYNCMEDIAFOLDERS");

                await DeleteAllFolders();
                
                var endpoint = $"/folders";

                var folders = GetFoldersToSynchronize();
                if (folders.Count == 0)
                {
                    return;
                }

                var payload = folders.Select(folder => new
                {
                    op = "addInto",
                    value = folder,
                });

                await ExecuteWithoutResponse(endpoint, PATCH, payload);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoKontentPublishing", "SYNCMEDIAFOLDERS", ex);
                throw;
            }
        }

        public async Task DeleteAllFolders()
        {
            try
            {
                SyncLog.Log("Deleting folders");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoKontentPublishing", "DELETEMEDIAFOLDERS");

                var folders = await GetAllFolders();
                var endpoint = $"/folders";

                if (folders.Count == 0)
                {
                    return;
                }

                var payload = folders.Select(folder => new {
                    op = "remove",
                    reference = new { id = folder.Id },
                });

                await ExecuteWithoutResponse(endpoint, PATCH, payload);
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
                SyncLog.LogException("KenticoKontentPublishing", "DELETEMEDIAFOLDERS", ex);
                throw;
            }
        }

        #endregion

        #region "Purge"
        
        private async Task<List<FolderData>> GetAllFolders()
        {
            var endpoint = $"/folders";

            var response = await ExecuteWithResponse<FoldersResponse>(endpoint, HttpMethod.Get);
            if (response == null)
            {
                return new List<FolderData>();
            }

            var ids = response.Folders;

            return ids.ToList();
        }

        #endregion
    }
}