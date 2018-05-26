#region usings

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using KCDModMerger.Properties;

#endregion

namespace KCDModMerger
{
    internal static class Logger
    {
        private static readonly StringBuilder sb = new StringBuilder();
        private static Timer timer = new Timer(LogToFile, null, 0, 10000);

        internal static string LOG_FILE =
            Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) +
            "\\KCDModMerger.log";

        /// <summary>
        /// Writes the given string to the log file
        /// </summary>
        /// <param name="message"></param>
        /// <param name="addExclamation"></param>
        internal static void Log(string message, bool addExclamation = false)
        {
            message = BuildLogWithDate(message, addExclamation);
            lock (sb)
            {
                sb.AppendLine(message);
            }

            if (MainWindow.CurrentActionLabel != null && MainWindow.isInformationVisible)
                MainWindow.CurrentActionLabel.InvokeIfRequired(
                    () => { MainWindow.CurrentActionLabel.Content = message; },
                    DispatcherPriority.ApplicationIdle);
        }

        /// <summary>
        /// Writes a StringBuilder to the log file
        /// </summary>
        /// <param name="stringBuilder"></param>
        internal static void Log(StringBuilder stringBuilder)
        {
            if (stringBuilder.Length > 0)
            {
                lock (sb)
                {
                    sb.Append(stringBuilder);
                }

                if (MainWindow.CurrentActionLabel != null && MainWindow.isInformationVisible)
                    MainWindow.CurrentActionLabel.InvokeIfRequired(
                        () => { MainWindow.CurrentActionLabel.Content = stringBuilder[stringBuilder.Length - 1]; },
                        DispatcherPriority.ApplicationIdle);
            }
        }

        /// <summary>
        /// Returns the processed message without a date
        /// </summary>
        /// <param name="message"></param>
        /// <param name="addExclamation"></param>
        /// <returns></returns>
        internal static string BuildLog(string message, bool addExclamation = false)
        {
            message = CheckForExclamation(message, addExclamation);
            message = CheckForThread(message);

            return message;
        }

        /// <summary>
        /// Returns the processed message with a date
        /// </summary>
        /// <param name="message"></param>
        /// <param name="addExclamation"></param>
        /// <returns></returns>
        internal static string BuildLogWithDate(string message, bool addExclamation = false)
        {
            message = BuildLog(message, addExclamation);

            message = "[" + DateTime.Now + "] " + message;

            return message;
        }

        /// <summary>
        /// Opens the log file.
        /// </summary>
        internal static void OpenLogFile()
        {
            LogToFile(null);
            Process.Start(@"" + LOG_FILE);
        }

        private static string CheckForExclamation(string message, bool addExclamation)
        {
            if (!addExclamation && !message.EndsWith("!") && !message.EndsWith(".") && !message.EndsWith(":") &&
                !message.EndsWith("}") && !message.EndsWith("]") && !message.EndsWith("-") && !message.EndsWith("?") &&
                !message.EndsWith(")") && !message.EndsWith(" ") && !message.EndsWith(Environment.NewLine))
                return message + "...";

            if (addExclamation)
            {
                return message + "!";
            }

            return message;
        }

        private static string CheckForThread(string message)
        {
            if (SynchronizationContext.Current == null && Thread.CurrentThread.ManagedThreadId != App.MainThreadId)
            {
                return "[Threaded] " + message;
            }

            return message;
        }

        internal static void LogException(object sender, UnhandledExceptionEventArgs args)
        {
            var exception = (Exception) args.ExceptionObject;

            CreateExceptionString(exception);

            var result = MessageBox.Show(
                "Program encountered a critical exception!" + Environment.NewLine + "Clear Cache?",
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
        }

        private static void CreateExceptionString(Exception e, string indent = "")
        {
            if (!string.IsNullOrEmpty(indent)) sb.AppendFormat("{0}Inner ", indent);

            sb.AppendFormat("Critical Exception Encountered:{0}Type: {1}", Environment.NewLine + indent,
                e.GetType().FullName);
            sb.AppendFormat("{0}Message: {1}", Environment.NewLine + indent, e.Message);
            sb.AppendFormat("{0}Source: {1}", Environment.NewLine + indent, e.Source);
            sb.AppendFormat("{0}Stacktrace: {1}", Environment.NewLine + indent, Environment.NewLine + e.StackTrace);

            if (e.InnerException != null)
            {
                sb.Append(Environment.NewLine);
                CreateExceptionString(e.InnerException, indent + "  ");
            }

            LogToFile(null);
        }

        internal static void LogToFile(object state)
        {
            Task.Run(() =>
            {
                if (sb.Length > 0)
                {
                    lock (sb)
                    {
                        File.AppendAllText(LOG_FILE, sb.ToString());
                        sb.Clear();
                    }
                }
            });
        }
    }
}