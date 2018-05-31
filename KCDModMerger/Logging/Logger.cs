#region usings

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using KCDModMerger.Properties;

#endregion

namespace KCDModMerger.Logging
{
    // Reentrant Logger written with Producer/Consumer pattern.
    // It creates a thread that receives write commands through a Queue (a BlockingCollection).
    // The user of this log has just to call Logger.WriteLine() and the log is transparently written asynchronously.

    internal static class Logger
    {
        private static readonly StringBuilder sb = new StringBuilder();
        private static readonly Timer timer = new Timer(CheckMemUsage, null, 0, 1000 * 10);
        private static Task task;

        internal static string LOG_FILE =
            Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) +
            "\\KCDModMerger.log";

        private static readonly List<LogEntry> final = new List<LogEntry>();

        private static readonly BlockingCollection<LogEntry> bc = new BlockingCollection<LogEntry>();

        // Constructor create the thread that wait for work on .GetConsumingEnumerable()
        [Log]
        internal static void Initialize()
        {
            Log("Starting KCDModMerger!");
            if (File.Exists(LOG_FILE)) File.Delete(LOG_FILE);

            AppDomain.CurrentDomain.UnhandledException += LogException;

            task = Task.Factory.StartNew(() =>
            {
                foreach (LogEntry p in bc.GetConsumingEnumerable())
                {
                    lock (final)
                    {
                        if (p.ThreadName != "Main")
                        {
                            var lastEntry = final.FindLastIndex(entry => entry.ThreadName == p.ThreadName);

                            if (lastEntry != -1 && lastEntry + 1 < final.Count)
                            {
                                final.Insert(lastEntry + 1, p);
                            }
                            else
                            {
                                final.Add(p);
                            }
                        }
                        else
                        {
                            final.Add(p);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Opens the log file.
        /// </summary>
        [Log]
        internal static void OpenLogFile()
        {
            LogToFile();
            Process.Start(@"" + LOG_FILE);
        }

        [Log]
        internal static void LogException(object sender, UnhandledExceptionEventArgs args)
        {
            task.Wait(1000);

            var exception = (Exception) args.ExceptionObject;

            CreateExceptionString(exception);

            var result = MessageBox.Show(
                "Program encountered a critical exception!" + Environment.NewLine + exception.Message +
                Environment.NewLine + "Clear Cache?",
                "KCDModMerger", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                Settings.Default.Reset();
                if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\VanillaFiles.json"))
                {
                    File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\VanillaFiles.json");
                }
            }
            else
            {
                Settings.Default.Save();
            }

            Finalize();
        }

        [Log]
        internal static void LogToFile()
        {
            lock (final)
            {
                foreach (LogEntry logEntry in final)
                {
                    sb.AppendLine(logEntry.ToString());
                }
            }

            File.AppendAllText(LOG_FILE, sb.ToString());
            sb.Clear();
        }

        [Log]
        internal static void Finalize()
        {
            timer.Dispose();
            Log("Stopping KCDModMerger!");
            task.Wait(1000);
            // Free the writing thread
            bc.CompleteAdding();
            LogToFile();
            File.Delete(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) + "\\unrar.dll");
        }

        #region Utility

        [Log]
        internal static void CreateExceptionString(Exception e, string indent = "", StringBuilder sb = null)
        {
            if (sb == null)
            {
                sb = Logger.sb;
            }

            if (!string.IsNullOrEmpty(indent)) sb.AppendFormat("{0}Inner ", indent);

            sb.AppendFormat("Exception Encountered:{0}Type: {1}", Environment.NewLine + indent,
                e.GetType().FullName);
            sb.AppendFormat("{0}Message: {1}", Environment.NewLine + indent, e.Message);
            sb.AppendFormat("{0}Source: {1}", Environment.NewLine + indent, e.Source);
            sb.AppendFormat("{0}Stacktrace: {1}", Environment.NewLine + indent, Environment.NewLine + e.StackTrace);

            if (e.InnerException != null)
            {
                sb.Append(Environment.NewLine);
                CreateExceptionString(e.InnerException, indent + "  ", sb);
            }

            sb.Append(Environment.NewLine);
        }

        [Log]
        private static void CheckMemUsage(object state)
        {
            lock (final)
            {
                if (final.Count > 500)
                {
                    LogToFile();
                    final.Clear();
                }
            }
        }

        #endregion

        #region LogMethods

        [Log]
        private static void AddEntry(LogEntry entry)
        {
            try
            {
                bc.TryAdd(entry);
            }
            catch (InvalidOperationException e)
            {
                // Catch all
            }
        }

        // Just call this method to log something (it will return quickly because it just queue the work with bc.Add(p))
        [Log]
        internal static void Log(string msg, bool addExclamation = false)
        {
            LogEntry p = new LogEntry(msg, addExclamation, new StackTrace());
            AddEntry(p);
        }

        [Log]
        internal static void LogWarn(string msg, WarnSeverity severity = WarnSeverity.Low, bool addExclamation = false)
        {
            switch (severity)
            {
                case WarnSeverity.Low:
                {
                    msg = "*** Info: " + msg;
                    break;
                }
                case WarnSeverity.Mid:
                {
                    msg = "*** Warning: " + msg;
                    break;
                }
                case WarnSeverity.High:
                {
                    msg = "*** Error: " + msg;
                    break;
                }
                case WarnSeverity.Critical:
                {
                    msg = "*** Critical: " + msg;
                    break;
                }
            }

            LogEntry p = new LogEntry(msg, addExclamation, new StackTrace());
            AddEntry(p);
        }

        /// <summary>
        /// Logs the specified method name.
        /// </summary>
        /// <param name="methodName">Name of the method.</param>
        /// <param name="stack">The stack.</param>
        /// <param name="callTime">The call time.</param>
        /// <param name="elapsed">The elapsed.</param>
        [Log]
        internal static void Log(string methodName, StackTrace stack, DateTime callTime, TimeSpan elapsed)
        {
            AddEntry(new LogEntry(methodName, stack, callTime, elapsed));
        }

        /// <summary>
        /// Logs the specified method exception.
        /// </summary>
        /// <param name="methodName">Name of the method.</param>
        /// <param name="callTime">The call time.</param>
        /// <param name="elapsed">The elapsed time.</param>
        /// <param name="parameters">The parameters.</param>
        /// <param name="e">The exception.</param>
        [Log]
        internal static void Log(string methodName, DateTime callTime, TimeSpan elapsed, object[] parameters,
            Exception e)
        {
            AddEntry(new LogEntry(methodName, callTime, elapsed, parameters, e));
        }

        /// <summary>
        /// Logs the specified method name.
        /// </summary>
        /// <param name="methodName">Name of the method.</param>
        /// <param name="stack">The stack.</param>
        /// <param name="callTime">The call time.</param>
        /// <param name="parameters">The parameters.</param>
        [Log]
        internal static void Log(string methodName, StackTrace stack, DateTime callTime, object[] parameters)
        {
            AddEntry(new LogEntry(methodName, stack, callTime, parameters));
        }

        #endregion
    }

    internal enum WarnSeverity
    {
        Low,
        Mid,
        High,
        Critical
    }
}