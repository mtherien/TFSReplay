using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace TFSReplay
{
    internal class ChangesetProcessor
    {
        private readonly Workspace _destinationWorkspace;
        private readonly ILogger _logger;

        public ChangesetProcessor(Workspace destinationWorkspace, ILogger logger)
        {
            _destinationWorkspace = destinationWorkspace;
            _logger = logger;
        }

        public List<string> ProcessChangeset(Changeset changeset, DirectoryInfo destinationFolder, DirectoryInfo startupFolder, string baseServerPath, Dictionary<string,string> mapPaths)
        {
            int changesetId;
            _logger.LogInfo("Changeset {0}, committed by {1}:", changeset.ChangesetId, changeset.CommitterDisplayName);
            _logger.LogInfo(changeset.Comment);
            _logger.LogInfo("");

            var changedFiles = from change in changeset.Changes
                           where
                               ((change.ChangeType & ChangeType.Edit) == ChangeType.Edit
                                || (change.ChangeType & ChangeType.Add) == ChangeType.Add
                                || (change.ChangeType & ChangeType.Delete) == ChangeType.Delete)
                           select change;

            var changedItems = new List<string>();
            

            foreach (var changedFile in changedFiles)
            {
                var serverFilePath = changedFile.Item.ServerItem;
                var strippedPath = serverFilePath.Replace(baseServerPath, "");
                strippedPath = strippedPath.Replace('/', '\\');
                strippedPath = strippedPath.TrimStart('\\');


                _logger.LogInfo("File: {0}", strippedPath);


                var isEdit = (changedFile.ChangeType & ChangeType.Edit) == ChangeType.Edit;
                var isAdd = (changedFile.ChangeType & ChangeType.Add) == ChangeType.Add;
                var isDelete = (changedFile.ChangeType & ChangeType.Delete) == ChangeType.Delete;
                var isRename = (changedFile.ChangeType & ChangeType.Rename) == ChangeType.Rename;

                foreach (var key in mapPaths.Keys)
                {
                    if (strippedPath.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    {
                        strippedPath = strippedPath.Replace(key, mapPaths[key]);
                        break;
                    }
                }

                var newDestination = destinationFolder.FullName + "\\" + strippedPath;

                var fileExists = File.Exists(newDestination);

                if (isAdd || (isEdit && !fileExists))
                {
                    // Add file
                    if (changedFile.Item.ItemType == ItemType.Folder)
                    {
                        Directory.CreateDirectory(newDestination);
                    }
                    else
                    {
                        changedFile.Item.DownloadFile(newDestination);
                    }
                    if (_destinationWorkspace != null) _destinationWorkspace.PendAdd(newDestination);
                    _logger.LogInfo("+{0}", PathHelper.MakeRelativePath(startupFolder.FullName, newDestination));
                    changedItems.Add(newDestination);
                }
                else if (isEdit)
                {
                    if (changedFile.Item.ItemType != ItemType.Folder)
                    {
                        if (_destinationWorkspace != null) _destinationWorkspace.PendEdit(newDestination);

                        // Replace file
                        changedFile.Item.DownloadFile(newDestination);
                        _logger.LogInfo("~{0}", PathHelper.MakeRelativePath(startupFolder.FullName, newDestination));
                        changedItems.Add(newDestination);
                    }
                }
                else if (isDelete)
                {
                    // Delete file
                    if (fileExists)
                    {
                        if (_destinationWorkspace != null) _destinationWorkspace.PendDelete(newDestination);
                        _logger.LogInfo("-{0}", PathHelper.MakeRelativePath(startupFolder.FullName, newDestination));
                        changedItems.Add(newDestination);
                    }
                }
                else
                {
                    var changeTypes = new StringBuilder();
                    changeTypes.Append((changedFile.ChangeType & ChangeType.Branch) == ChangeType.Branch ? " BRANCH" : "");
                    changeTypes.Append((changedFile.ChangeType & ChangeType.Encoding) == ChangeType.Encoding ? " ENCODING" : "");
                    changeTypes.Append((changedFile.ChangeType & ChangeType.Lock) == ChangeType.Lock ? " LOCK" : "");
                    changeTypes.Append((changedFile.ChangeType & ChangeType.Merge) == ChangeType.Merge ? " MERGE" : "");
                    changeTypes.Append((changedFile.ChangeType & ChangeType.None) == ChangeType.None ? " NONE" : "");
                    changeTypes.Append((changedFile.ChangeType & ChangeType.Property) == ChangeType.Property ? " PROPERTY" : "");
                    changeTypes.Append((changedFile.ChangeType & ChangeType.Rename) == ChangeType.Rename ? " RENAME" : "");
                    changeTypes.Append((changedFile.ChangeType & ChangeType.Rollback) == ChangeType.Rollback ? " ROLLBACK" : "");
                    changeTypes.Append((changedFile.ChangeType & ChangeType.SourceRename) == ChangeType.SourceRename
                        ? " SOURCERENAME"
                        : "");
                    changeTypes.Append((changedFile.ChangeType & ChangeType.Undelete) == ChangeType.Undelete ? " UNDELETE" : "");

                    _logger.LogError("*** WARNING *** Unhandled change types on file {0}:\r\n\t{1}",
                        PathHelper.MakeRelativePath(startupFolder.FullName, newDestination), changeTypes);
                }
            }
            return changedItems;
        }

    }
}
