﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;

namespace TFSReplay
{
    public class TFSReplayOptions
    {
        [Option('c', "changeset", HelpText = "Changeset ID To Replay", Required = true)]
        public int ChangeSetId { get; set; }

        [Option('s', "ServerUrl", HelpText = "Source TFS Server Url", Required = true)]
        public string ServerUrl { get; set; }

        [Option('d', "Destination", HelpText = "Destination Folder that the files will be written to", Required = true)]
        public string Destination { get; set; }

        //[Option('e', "dEstinationUrl", HelpText = "Destination TFS Server Url (if different from source)", Required = false)]
        //public string DestinationUrl { get; set; }

        [Option('b', "BasePath", HelpText = "TFS Server path to replay from (Ex: $/TeamProjectName)", Required = false)]
        public string BaseServerPath { get; set; }

        [Option("checkin", HelpText = "Checkin items after moved", Required = false, DefaultValue = false)]
        public bool Checkin { get; set; }

        [Option("noprompt", HelpText = "Do not prompt before checking in", Required = false, DefaultValue = false)]
        public bool NoPrompt { get; set; }

        //[Option("merge", HelpText = "Merge items (instead of force)", Required = false, DefaultValue = false)]
        //public bool MergeItems { get; set; }

        [Option('m', "MapFile", HelpText = "CSV file to remap folders from source to a destination", Required = false)]
        public string MapFile { get; set; }

        [HelpOption]
        public string GetHelp()
        {
            var help = new HelpText
            {
                Heading = new HeadingInfo("TFSReplay", "0.0.3"),
                AdditionalNewLineAfterOption = true,
                AddDashesToOption = true
            };
            help.AddPreOptionsLine("Usage: TFSReplay");
            help.AddOptions(this);
            return help;
        }
    }
}
