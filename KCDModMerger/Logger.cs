using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KCDModMerger
{
    static class Logger
    {
        private static StringBuilder sb = new StringBuilder();
        private static Timer timer = new Timer(LogToFile, null, 0, 10000);

        internal static string LOG_FILE =
            Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(ModManager)).Location) +
            "\\KCDModMerger.log";

        public static void Log(string message, bool addExclamation = false)
        {
            if (!addExclamation && !message.EndsWith("!") && !message.EndsWith(".") && !message.EndsWith(":") &&
                !message.EndsWith("}") && !message.EndsWith("]") && !message.EndsWith("-"))
            {
                message += "...";
            }
            else if (addExclamation)
            {
                message += "!";
            }

            sb.AppendLine("[" + DateTime.Now.ToString() + "] " + message);

            if (MainWindow.CurrentActionLabel != null)
            {
                MainWindow.CurrentActionLabel.Content = message;
            }
        }

        internal static void LogException(object sender, UnhandledExceptionEventArgs args)
        {
            var exception = (Exception) args.ExceptionObject;

            CreateExceptionString(exception);
        }

        private static void CreateExceptionString(Exception e, string indent = "")
        {
            if (indent != "")
            {
                sb.AppendFormat("{0}Inner ", indent);
            }

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
            if (sb.Length > 0)
            {
                File.AppendAllText(LOG_FILE, sb.ToString());
                sb.Clear();
            }
        }
    }
}