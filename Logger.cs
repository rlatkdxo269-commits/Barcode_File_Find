using System;
using System.IO;
using System.Text;

namespace Barcode_File_Find
{
    public static class Logger
    {
        private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");
        private static readonly object _lock = new object();

        // Event to notify the UI when a new log entry is added
        public static event Action<string>? OnLogAdded;

        public static void Log(string message)
        {
            string timestampedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            
            lock (_lock)
            {
                try
                {
                    File.AppendAllText(LogFile, timestampedMessage + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // Ignore logging errors to prevent crashes
                }
            }

            // Fire event on a background thread or let the subscriber handle dispatching
            OnLogAdded?.Invoke(timestampedMessage);
        }
    }
}

