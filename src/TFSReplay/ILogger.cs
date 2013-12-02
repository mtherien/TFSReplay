using System;

namespace TFSReplay
{
    internal interface ILogger
    {
        void LogError(string message);
        void LogError(string messageFormat, params object[] parameters);
        void LogInfo(string message);
        void LogInfo(string messageFormat, params object[] parameters);
    }

    public class ConsoleLogger : ILogger
    {
        public void LogError(string message)
        {
            Console.Error.WriteLine(message);
            HasErrors = true;
        }

        public void LogError(string messageFormat, params object[] parameters)
        {
            Console.Error.WriteLine(messageFormat,parameters);
            HasErrors = true;
        }

        public void LogInfo(string message)
        {
            Console.WriteLine(message);
        }

        public void LogInfo(string messageFormat, params object[] parameters)
        {
            Console.WriteLine(messageFormat,parameters);
        }

        public bool HasErrors { get; private set; }
    }
}