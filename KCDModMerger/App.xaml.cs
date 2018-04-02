#region usings

using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using KCDModMerger.Properties;
using Microsoft.Win32;

#endregion

namespace KCDModMerger
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static int MainThreadId = -1;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// </summary>
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += Logger.LogException;
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
#if DEBUG
            Logger.Log("Cleared User-defined Settings!");
            Settings.Default.Reset();
#endif
            //TODO: ILMerge?
            Logger.Log("Deleting Old Log File");
            if (File.Exists(Logger.LOG_FILE)) File.Delete(Logger.LOG_FILE);
            Logger.Log("Deleted Old Log File!");

            Logger.Log("Initializing ModMerger");

            PrintInfo();

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

            Logger.Log("Initialized ModMerger!");
        }

        private void PrintInfo()
        {
            Logger.Log("Operating System Information!");
            Logger.Log("----------------------------");
            Logger.Log(string.Format("Name: {0}", OSVersionInfo.Name), true);
            Logger.Log(string.Format("Edition: {0}", OSVersionInfo.Edition), true);
            if (OSVersionInfo.ServicePack != string.Empty)
                Logger.Log(string.Format("Service Pack: {0}", OSVersionInfo.ServicePack), true);
            else
                Logger.Log("Service Pack: None!");
            Logger.Log(string.Format("Version: {0}", OSVersionInfo.VersionString), true);
            Logger.Log(".Net Version: " + GetDotNetVerson(), true);

            Logger.Log(GetProcessorInfo());

            Logger.Log(GetGPUInfo());

            long memKb;
            GetPhysicallyInstalledSystemMemory(out memKb);
            Logger.Log("Memory: " + (memKb / 1024 / 1024) + " GiB!");
            Logger.Log(string.Format("OS: {0}", string.Join("", ("" + OSVersionInfo.OSBits).Reverse())), true);
            Logger.Log(string.Format("Program: {0}", string.Join("", ("" + OSVersionInfo.ProgramBits).Reverse())),
                true);
            Logger.Log("PC-Name: " + Environment.MachineName, true);
            Logger.Log("User: " + Environment.UserName, true);
            Logger.Log("Run As Administrator: " + new WindowsPrincipal(WindowsIdentity.GetCurrent())
                           .IsInRole(WindowsBuiltInRole.Administrator), true);
            Logger.Log("Running in: " + Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), true);
        }

        private string GetProcessorInfo()
        {
            var indentation = Logger.BuildLogWithDate("").Length;
            var indent = "";

            for (var i = 0; i < indentation; i++)
            {
                indent += " ";
            }

            var log = Logger.BuildLog("Processors: " + Environment.NewLine);

            ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "select * from Win32_Processor");
            var props = searcher.Get();

            foreach (ManagementBaseObject prop in props)
            {
                var name = "";
                var cores = "";
                var clockSpeed = "";
                var adressWidth = "";

                foreach (PropertyData propProperty in prop.Properties)
                {
                    switch (propProperty.Name)
                    {
                        case "Name":
                        {
                            name = (string) propProperty.Value;
                            break;
                        }
                        case "MaxClockSpeed":
                        {
                            clockSpeed = "" + propProperty.Value;
                            break;
                        }
                        case "AddressWidth":
                        {
                            adressWidth = "" + propProperty.Value;
                            break;
                        }
                        case "NumberOfLogicalProcessors":
                        {
                            cores = "" + propProperty.Value;
                            break;
                        }
                    }
                }

                log += indent + "Name: " + name + Environment.NewLine;
                log += indent + "Logical Cores: " + cores + Environment.NewLine;
                log += indent + "Clock Speed: " + clockSpeed + "Mhz" + Environment.NewLine;
                log += indent + "Address Width: " + adressWidth + "Bit" + Environment.NewLine;
            }

            return log;
        }

        private string GetGPUInfo()
        {
            var indentation = Logger.BuildLogWithDate("").Length;
            var indent = "";

            for (var i = 0; i < indentation; i++)
            {
                indent += " ";
            }

            var log = Logger.BuildLog("GPUs: " + Environment.NewLine);

            ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "select * from Win32_VideoController");
            var props = searcher.Get();

            foreach (ManagementBaseObject prop in props)
            {
                var name = "";
                var horizontalRes = "";
                var verticalRes = "";
                var vram = "";

                foreach (PropertyData propProperty in prop.Properties)
                {
                    switch (propProperty.Name)
                    {
                        case "Name":
                        {
                            name = (string) propProperty.Value;
                            break;
                        }
                        case "CurrentHorizontalResolution":
                        {
                            horizontalRes = "" + propProperty.Value;
                            break;
                        }
                        case "CurrentVerticalResolution":
                        {
                            verticalRes = "" + propProperty.Value;
                            break;
                        }
                        case "AdapterRAM":
                        {
                            vram = ConvertToHighest(long.Parse("" + propProperty.Value)) +
                                   " (This might be wrong because this is using Windows Driver instead of AMD/Nvidia Driver)";
                            break;
                        }
                    }
                }

                log += indent + "Name: " + name + Environment.NewLine;
                log += indent + "Resolution: " + horizontalRes + "x" + verticalRes + Environment.NewLine;
                log += indent + "VRAM: " + vram + Environment.NewLine;
            }

            return log;
        }

        private void App_OnExit(object sender, ExitEventArgs e)
        {
            Logger.LogToFile(null);
            Settings.Default.Save();

            File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\unrar.dll");
        }

        internal static string ConvertToHighest(long bytes)
        {
            var kb = bytes / 1024f;

            if (kb > 1)
            {
                var mb = kb / 1024f;

                if (mb > 1)
                {
                    var gb = mb / 1024f;

                    if (gb > 1)
                    {
                        var tb = gb / 1024f;

                        if (tb > 1)
                            return Math.Round(tb) + " TiB"
                                ;

                        return Math.Round(gb) + " GiB";
                    }

                    return Math.Round(mb) + " MiB";
                }

                return Math.Round(kb) + " KiB";
            }

            return Math.Round((decimal) bytes) + " Bytes";
        }

        private string GetDotNetVerson()
        {
            var maxDotNetVersion = GetVersionFromRegistry();
            if (string.Compare(maxDotNetVersion, "4.5") >= 0)
            {
                var v45Plus = Get45PlusFromRegistry();
                if (v45Plus != "") maxDotNetVersion = v45Plus;
            }

            return maxDotNetVersion;
        }

        private string Get45PlusFromRegistry()
        {
            var dotNetVersion = "";
            const string subkey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";
            using (var ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)
                .OpenSubKey(subkey))
            {
                if (ndpKey != null && ndpKey.GetValue("Release") != null)
                    dotNetVersion = CheckFor45PlusVersion((int) ndpKey.GetValue("Release"));
            }

            return dotNetVersion;
        }

        // Checking the version using >= will enable forward compatibility.
        private string CheckFor45PlusVersion(int releaseKey)
        {
            if (releaseKey >= 461308) return "4.7.1 or later";
            if (releaseKey >= 460798) return "4.7";
            if (releaseKey >= 394802) return "4.6.2";
            if (releaseKey >= 394254) return "4.6.1";
            if (releaseKey >= 393295) return "4.6";
            if (releaseKey >= 379893) return "4.5.2";
            if (releaseKey >= 378675) return "4.5.1";
            if (releaseKey >= 378389) return "4.5";

            // This code should never execute. A non-null release key should mean
            // that 4.5 or later is installed.
            return "No 4.5 or later version detected";
        }

        private string GetVersionFromRegistry()
        {
            var maxDotNetVersion = "";
            // Opens the registry key for the .NET Framework entry.
            using (var ndpKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, "")
                .OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\"))
            {
                // As an alternative, if you know the computers you will query are running .NET Framework 4.5 
                // or later, you can use:
                // using (RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, 
                // RegistryView.Registry32).OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\"))
                foreach (var versionKeyName in ndpKey.GetSubKeyNames())
                    if (versionKeyName.StartsWith("v"))
                    {
                        var versionKey = ndpKey.OpenSubKey(versionKeyName);
                        var name = (string) versionKey.GetValue("Version", "");
                        var sp = versionKey.GetValue("SP", "").ToString();
                        var install = versionKey.GetValue("Install", "").ToString();
                        if (install == "") //no install info, must be later.
                        {
                            if (string.Compare(maxDotNetVersion, name) < 0) maxDotNetVersion = name;
                        }
                        else
                        {
                            if (sp != "" && install == "1")
                                if (string.Compare(maxDotNetVersion, name) < 0)
                                    maxDotNetVersion = name;
                        }

                        if (name != "") continue;

                        foreach (var subKeyName in versionKey.GetSubKeyNames())
                        {
                            var subKey = versionKey.OpenSubKey(subKeyName);
                            name = (string) subKey.GetValue("Version", "");
                            if (name != "") sp = subKey.GetValue("SP", "").ToString();

                            install = subKey.GetValue("Install", "").ToString();
                            if (install == "")
                            {
                                //no install info, must be later.
                                if (string.Compare(maxDotNetVersion, name) < 0) maxDotNetVersion = name;
                            }
                            else
                            {
                                if (sp != "" && install == "1")
                                {
                                    if (string.Compare(maxDotNetVersion, name) < 0) maxDotNetVersion = name;
                                }
                                else if (install == "1")
                                {
                                    if (string.Compare(maxDotNetVersion, name) < 0) maxDotNetVersion = name;
                                }
                            }
                        }
                    }
            }

            return maxDotNetVersion;
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);
    }
}