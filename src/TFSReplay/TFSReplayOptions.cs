using System;
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

        [Option('s', "ServerUrl", HelpText = "TFS Server Url", Required = true)]
        public string ServerUrl { get; set; }

        [Option('d', "Destination", HelpText = "Destination Folder", Required = true)]
        public string Destination { get; set; }

        [Option('b', "BasePath", HelpText = "Base Path", Required = false)]
        public string BaseServerPath { get; set; }

        [Option("checkin", HelpText = "Checkin items after moved", Required = false, DefaultValue = false)]
        public bool Checkin { get; set; }

        [Option("noprompt", HelpText = "Do not prompt before checking in", Required = false, DefaultValue = false)]
        public bool NoPrompt { get; set; }

        [HelpOption]
        public string GetHelp()
        {
            var help = new HelpText
            {
                Heading = new HeadingInfo("TFSReplay", "0.0.1"),
                AdditionalNewLineAfterOption = true,
                AddDashesToOption = true
            };
            help.AddPreOptionsLine("Usage: TFSReplay");
            help.AddOptions(this);
            return help;
        }
    }
}
