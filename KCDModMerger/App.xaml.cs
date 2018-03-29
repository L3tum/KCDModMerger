using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using KCDModMerger.Properties;
using Microsoft.Win32;

namespace KCDModMerger
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
#if DEBUG
            Logger.Log("Cleared User-defined Settings!");
            Settings.Default.Reset();
#endif
            if (File.Exists(Logger.LOG_FILE)) File.Delete(Logger.LOG_FILE);

            AppDomain.CurrentDomain.UnhandledException += Logger.LogException;

            Logger.Log("Initializing ModMerger");
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
            Logger.Log(string.Format("Processor: {0}", string.Join("", ("" + OSVersionInfo.ProcessorBits).Reverse())),
                true);
            Logger.Log("Cores: " + Environment.ProcessorCount, true);
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
            Logger.Log("Initialized ModMerger!");
        }

        private void App_OnExit(object sender, ExitEventArgs e)
        {
            Logger.LogToFile(null);
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
        static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);
    }
}