using CMS.EventLog;
using System;

namespace Kentico.EMS.Kontent.Publishing
{
    public static class SyncLog
    {
        public static Exception lastLoggedException;
        public static LogContext CurrentLog;
        
        public static void Log(string text, bool newLine = true)
        {
            var log = CurrentLog;
            if (log != null)
            {
                log.AppendText(text, newLine);
            }
        }

        public static void LogEvent(string eventType, string source, string eventCode, string eventDescription = null)
        {
            EventLogProvider.LogEvent(eventType, source, eventCode, eventDescription);
        }

        public static void LogException(string source, string eventCode, Exception ex, int siteId = 0, string additionalMessage = null)
        {
            // Do not log the same exception twice
            if (ex == lastLoggedException)
            {
                return;
            }
            EventLogProvider.LogException(source, eventCode, ex, siteId, additionalMessage);
            lastLoggedException = ex;
        }
    }
}
