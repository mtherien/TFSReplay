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
        [STAThread]
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
            var sourceTeamProjectCollection = new TfsTeamProjectCollection(serverUri);
            sourceTeamProjectCollection.EnsureAuthenticated();
            var sourceVersionControlService = sourceTeamProjectCollection.GetService<VersionControlServer>();

            TfsTeamProjectCollection destinationTeamProjectCollection = sourceTeamProjectCollection;
            Workspace destinationWorkspace = null;
            //if (!string.IsNullOrWhiteSpace(options.DestinationUrl))
            //{
            //    destinationTeamProjectCollection = new TfsTeamProjectCollection(new Uri(options.DestinationUrl));
            //    destinationTeamProjectCollection.EnsureAuthenticated();
            //    var destinationVersionControlService =
            //        destinationTeamProjectCollection.GetService<VersionControlServer>();
            //    destinationWorkspace = destinationVersionControlService.GetWorkspace(destinationFolder.FullName);
            //}

            if (destinationWorkspace == null)
            {
                try
                {
                    destinationWorkspace = sourceVersionControlService.GetWorkspace(destinationFolder.FullName);
                }
                catch (Exception ex)
                {
                    if (ex.GetType().Name != "ItemNotMappedException")
                        throw;
                }
                
            }

            var mapPaths = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(options.MapFile))
            {
                mapPaths = LoadMapFile(options.MapFile);
            }

            var changeset = sourceVersionControlService.GetChangeset(changesetId);
            var logger = new ConsoleLogger();

            var changesetProcessor = new ChangesetProcessor(destinationWorkspace, logger);

            var changedItems = changesetProcessor.ProcessChangeset(changeset, destinationFolder, startupFolder, options.BaseServerPath, mapPaths);

            Console.WriteLine();
            Console.WriteLine();

            if (options.Checkin)
            {
                if (changedItems.Count <= 0)
                {
                    Console.WriteLine("*** No changes detected, a checkin is not necessary.");
                    return;
                }

                var newCommentFormat =
                    "Original changeset {0}, committed by {1}, was moved to this location.\r\n\r\nOriginal comment:\r\n{2}";

                var newComment = string.Format(newCommentFormat, changeset.ChangesetId, changeset.CommitterDisplayName,
                    changeset.Comment);

                var changeList = new StringBuilder();
                PendingChange[] pendingChanges = new PendingChange[0];
                if (destinationWorkspace != null) 
                {
                    pendingChanges = destinationWorkspace.GetPendingChanges(changedItems.ToArray());
                }

                foreach (var pendingChange in pendingChanges)
                {
                    changeList.AppendLine(string.Format("{0}: {1}", pendingChange.ChangeTypeName, PathHelper.MakeRelativePath(startupFolder.FullName, pendingChange.LocalItem)));
                }

                {
                    var currentClipboard = System.Windows.Forms.Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(currentClipboard))
                    {
                        currentClipboard +=
                            "\r\n--------------------------------------------------------------------------\r\n";
                    }
                    System.Windows.Forms.Clipboard.SetText(currentClipboard + newComment);
                }

                if (pendingChanges.Length == 0)
                {
                    Console.Error.WriteLine("No pending changes");
                    return;
                }

                bool doCheckin = false;
                if (logger.HasErrors || !options.NoPrompt)
                {
                    if (logger.HasErrors)
                    {
                        Console.WriteLine("*** WARNING *** There were warnings in processing the changeset.");
                        Console.WriteLine("Please look at the output and confirm you want to checkin the changes.");
                        Console.WriteLine();
                    }
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
                    if (destinationWorkspace == null)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Destination Workspace undetermined.  Unable to checkin");
                    }
                    else
                    {
                        var newChangeset = destinationWorkspace.CheckIn(pendingChanges, newComment);
                        Console.WriteLine();
                        Console.WriteLine("Checked in, changeset {0}", newChangeset);
                    }
                }

            }
            
        }

        private static Dictionary<string,string> LoadMapFile(string mapFile)
        {
            var map = new Dictionary<string, string>();

            using (var textReader = new StreamReader(mapFile))
            {
                while (!textReader.EndOfStream)
                {
                    var fileLine = textReader.ReadLine();
                    if (fileLine.Length > 0 && fileLine[0] == '#')
                        continue;

                    var splitLine = fileLine.Split(',');
                    var sourceFolder = splitLine[0];
                    var destinationFolder = splitLine[1];

                    //if (sourceFolder.EndsWith("\\"))
                    //    sourceFolder = sourceFolder.Substring(0, sourceFolder.Length - 1);

                    map.Add(sourceFolder,destinationFolder);
                }
            }

            return map;
        }
    }
}
