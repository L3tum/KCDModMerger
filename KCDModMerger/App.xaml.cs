#region usings

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using KCDModMerger.Logging;
using KCDModMerger.Properties;

#endregion

namespace KCDModMerger
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// The main thread identifier
        /// </summary>
        internal static int MainThreadId = -1;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// </summary>
        public App()
        {
            Thread.CurrentThread.Name = "Main";
            Logging.Logger.Initialize();
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
#if DEBUG
            Logging.Logger.Log("Cleared User-defined Settings!");
            Settings.Default.Reset();
#endif
            //TODO: ILMerge?
            Logger.Log("Deleting Old Log File");
            if (File.Exists(Logger.LOG_FILE)) File.Delete(Logger.LOG_FILE);
            Logger.Log("Deleted Old Log File!");

            Logger.Log("Initializing ModMerger");

            Utilities.PrintInfo();

            LoadUnrar();

            Logger.Log("Initialized ModMerger!");
        }

        private void LoadUnrar()
        {
            Logger.Log("Determining Unrar Version to load");
            if (string.Join("", ("" + OSVersionInfo.ProcessorBits).Reverse()) == "64Bit" &&
                string.Join("", ("" + OSVersionInfo.OSBits).Reverse()) == "64Bit" &&
                string.Join("", ("" + OSVersionInfo.ProgramBits).Reverse()) == "64Bit")
            {
                Logger.Log("Loading 64Bit Unrar");
                var location = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
                File.Copy(location + "\\Tools\\Unrar\\64Bit\\unrar.dll", location + "\\unrar.dll", true);
                Logger.Log("Loaded 64Bit Unrar!");
            }
            else
            {
                Logger.Log("Loading 32Bit Unrar");
                var location = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
                File.Copy(location + "\\Tools\\Unrar\\32Bit\\unrar.dll", location + "\\unrar.dll", true);
                Logger.Log("Loaded 32Bit Unrar!");
            }
        }

        [Log]
        private void App_OnExit(object sender, ExitEventArgs e)
        {
            Settings.Default.Save();
            Logging.Logger.Finalize();
        }
    }
}