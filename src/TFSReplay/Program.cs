using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Client.CommandLine;
using Microsoft.TeamFoundation.Framework.Common;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace TFSReplay
{
    class Program
    {
        static void Main(string[] args)
        {
            var options = new TFSReplayOptions();
            var parseResult = CommandLine.Parser.Default.ParseArguments(args, options);
            var startupFolder = new DirectoryInfo(Assembly.GetEntryAssembly().Location).Parent;

            if (startupFolder==null) throw new Exception("Could not determine startup folder");

            if (!parseResult)
            {
                return;
            }

            var changesetId = options.ChangeSetId;
            var destinationFolder = new DirectoryInfo(options.Destination);

            if (!destinationFolder.Exists)
            {
                // Try pre-pending the startup location
                destinationFolder = new DirectoryInfo(startupFolder.FullName + "\\" + options.Destination);
                if (!destinationFolder.Exists)
                {
                    Console.Error.WriteLine("Folder {0} does not exist", options.Destination);
                    return;
                }
            }
            
            var serverUri = new Uri(options.ServerUrl);
            var tpc = new TfsTeamProjectCollection(serverUri);
            tpc.EnsureAuthenticated();

            var vcs = tpc.GetService<VersionControlServer>();
            var destWorkspace = vcs.GetWorkspace(destinationFolder.FullName);
            
            var changeset = vcs.GetChangeset(changesetId);

            Console.WriteLine("Changeset {0}, committed by {1}:", changesetId,changeset.CommitterDisplayName);
            Console.WriteLine(changeset.Comment);
            Console.WriteLine();

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
                var strippedPath = serverFilePath.Replace(options.BaseServerPath, "");
                strippedPath = strippedPath.Replace('/', '\\');
                strippedPath = strippedPath.TrimStart('\\');
                

                Console.WriteLine("File: {0}", strippedPath);


                var isEdit = (changedFile.ChangeType & ChangeType.Edit) == ChangeType.Edit;
                var isAdd = (changedFile.ChangeType & ChangeType.Add) == ChangeType.Add;
                var isDelete = (changedFile.ChangeType & ChangeType.Delete) == ChangeType.Delete;

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
                    destWorkspace.PendAdd(newDestination);
                    Console.WriteLine("+{0}", MakeRelativePath(startupFolder.FullName, newDestination));
                    changedItems.Add(newDestination);
                }
                else if (isEdit)
                {
                    if (changedFile.Item.ItemType != ItemType.Folder)
                    {
                        destWorkspace.PendEdit(newDestination);

                        // Replace file
                        changedFile.Item.DownloadFile(newDestination);
                        Console.WriteLine("~{0}", MakeRelativePath(startupFolder.FullName, newDestination));
                        changedItems.Add(newDestination);
                        
                    }
                }
                else if (isDelete)
                {
                    // Delete file
                    if (fileExists)
                    {
                        destWorkspace.PendDelete(newDestination);
                        Console.WriteLine("-{0}", MakeRelativePath(startupFolder.FullName, newDestination));
                        changedItems.Add(newDestination);
                    }
                }
                

            }

            Console.WriteLine();
            Console.WriteLine();

            if (options.Checkin)
            {
                var newCommentFormat =
                    "Original changeset {0}, committed by {1}, was moved to this location.\r\n\r\nOriginal comment:\r\n{2}";

                var newComment = string.Format(newCommentFormat, changeset.ChangesetId, changeset.CommitterDisplayName,
                    changeset.Comment);

                var changeList = new StringBuilder();
                var pendingChanges = destWorkspace.GetPendingChanges(changedItems.ToArray());
                foreach (var pendingChange in pendingChanges)
                {
                    changeList.AppendLine(string.Format("{0}: {1}", pendingChange.ChangeTypeName, MakeRelativePath(startupFolder.FullName, pendingChange.LocalItem)));
                }

                if (pendingChanges.Length == 0)
                {
                    Console.Error.WriteLine("No pending changes");
                    return;
                }

                bool doCheckin = false;
                if (!options.NoPrompt)
                {
                    Console.WriteLine("About to commit:");
                    Console.WriteLine(changeList);
                    Console.WriteLine();
                    Console.WriteLine("As:");
                    Console.WriteLine(newComment);
                    Console.WriteLine();
                    Console.Write("Continue with check-in? (Y/N) ");
                    var answer = Console.ReadLine();
                    if (answer != null && (answer.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
                                           answer.Equals("yes", StringComparison.OrdinalIgnoreCase)))
                    {
                        doCheckin = true;
                    }
                }
                else
                {
                    doCheckin = true;
                }

                if (doCheckin)
                {
                    var newChangeset = destWorkspace.CheckIn(pendingChanges, newComment);
                    Console.WriteLine();
                    Console.WriteLine("Checked in, changeset {0}", newChangeset);
                }
            }
            
        }

        /// <summary>
        /// Creates a relative path from one file or folder to another.
        /// </summary>
        /// <param name="fromPath">Contains the directory that defines the start of the relative path.</param>
        /// <param name="toPath">Contains the path that defines the endpoint of the relative path.</param>
        /// <param name="dontEscape">Boolean indicating whether to add uri safe escapes to the relative path</param>
        /// <returns>The relative path from the start directory to the end path.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static String MakeRelativePath(String fromPath, String toPath)
        {
            if (String.IsNullOrEmpty(fromPath)) throw new ArgumentNullException("fromPath");
            if (String.IsNullOrEmpty(toPath)) throw new ArgumentNullException("toPath");

            Uri fromUri = new Uri(fromPath);
            Uri toUri = new Uri(toPath);

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            String relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (relativePath.StartsWith("../../../"))
            {
                // to many dots, so just return the toPath
                return toPath;
            }

            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
