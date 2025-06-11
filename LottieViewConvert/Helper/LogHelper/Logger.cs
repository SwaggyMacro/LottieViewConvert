using System;
using System.IO;
using System.Runtime.CompilerServices;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Layout;

namespace LottieViewConvert.Helper.LogHelper
{
    public class Logger
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Logger));

        static Logger()
        {
            // Enable log4net internal debugging if needed.
            log4net.Util.LogLog.InternalDebugging = false;

            var fileAppender = new FileAppender
            {
                // Set locking model to MinimalLock to reduce file contention.
                LockingModel = new FileAppender.MinimalLock()
            };

            var docPath = AppDomain.CurrentDomain.BaseDirectory;
            var now = DateTime.Now;
            var date = now.ToString("yyyy-MM-dd");
            var logDir = Path.Combine(docPath, "Logs", now.Year.ToString(), now.Month.ToString());

            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            var logPath = Path.Combine(logDir, $"{date}.log");
            fileAppender.File = logPath;
            fileAppender.AppendToFile = true;
            fileAppender.ImmediateFlush = true; // Ensures logs are written immediately.
            fileAppender.Layout =
                new PatternLayout(
                    "%date [%thread] (%property{file}:%property{line}) %-5level %logger{2} - %message%newline");

            // Set the threshold before activating options.
            fileAppender.Threshold = log4net.Core.Level.All;
            fileAppender.ActivateOptions();

            BasicConfigurator.Configure(fileAppender);
        }

        private static void SetContextProperties(string file, int line)
        {
            GlobalContext.Properties["file"] = Path.GetFileName(file);
            GlobalContext.Properties["line"] = line;
        }

        public static void Debug(string message, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            SetContextProperties(file, line);
            Log.Debug(message);
            System.Diagnostics.Debug.WriteLine(message);
        }

        public static void Info(string message, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            SetContextProperties(file, line);
            Log.Info(message);
        }

        public static void Warn(string message, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            SetContextProperties(file, line);
            Log.Warn(message);
        }

        public static void Error(string message, string? stackTrace = null, [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            SetContextProperties(file, line);
            Log.Error(stackTrace == null ? message : $"{message}\nStackTrace:\n{stackTrace}");
        }

        public static void Fatal(string message, string? stackTrace = null, [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            SetContextProperties(file, line);
            Log.Fatal(stackTrace == null ? message : $"{message}\nStackTrace:\n{stackTrace}");
        }
    }
}