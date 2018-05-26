#region usings

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml;
using Microsoft.Win32;

#endregion

namespace KCDModMerger
{
    internal static class Utilities
    {
        /// <summary>
        /// Invokes if required.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <param name="action">The action.</param>
        /// <param name="priority">The priority.</param>
        internal static void InvokeIfRequired(this Control control, Action action,
            DispatcherPriority priority = DispatcherPriority.Background)
        {
            if (!control.Dispatcher.CheckAccess())
                control.Dispatcher.Invoke(action, priority);
            else
                action.Invoke();
        }

        /// <summary>
        /// Disables the button.
        /// </summary>
        /// <param name="button">The button.</param>
        /// <param name="tooltip">The tooltip.</param>
        internal static void DisableButton(this Button button, string tooltip = null)
        {
            button.InvokeIfRequired(() =>
            {
                button.IsEnabled = false;
                button.ToolTip = tooltip;
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// Enables the button.
        /// </summary>
        /// <param name="button">The button.</param>
        /// <param name="tooltip">The tooltip.</param>
        internal static void EnableButton(this Button button, string tooltip = null)
        {
            button.InvokeIfRequired(() =>
            {
                button.IsEnabled = true;
                button.ToolTip = tooltip;
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// Determines whether this instance is default.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        /// <returns>
        ///   <c>true</c> if the specified value is default; otherwise, <c>false</c>.
        /// </returns>
        internal static bool IsDefault<T>(this T value) where T : struct
        {
            bool isDefault = value.Equals(default(T));

            return isDefault;
        }

        /// <summary>
        /// Creates a directory.
        /// </summary>
        /// <param name="directory">The directory.</param>
        /// <returns></returns>
        internal static DirectoryInfo CreateDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                return null;
            }

            return Directory.CreateDirectory(directory);
        }

        /// <summary>
        /// Writes a manifest.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="name">The name.</param>
        /// <param name="desc">The description.</param>
        /// <param name="version">The version.</param>
        /// <param name="author">The author.</param>
        /// <param name="createdOn">The date it was created on.</param>
        /// <returns></returns>
        internal static bool WriteManifest(string fileName, string name, string desc, string version, string author,
            string createdOn)
        {
            if (File.Exists(fileName))
            {
                return false;
            }

            using (XmlWriter xml = XmlWriter.Create(fileName))
            {
                xml.WriteStartDocument();
                xml.WriteStartElement("kcd_mod");
                xml.WriteStartElement("info");
                xml.WriteElementString("name", name);
                xml.WriteElementString("description", desc);
                xml.WriteElementString("author", author);
                xml.WriteElementString("version", version);
                xml.WriteElementString("created_on", createdOn);
                xml.WriteEndElement();
                xml.WriteEndElement();
                xml.WriteEndDocument();
            }

            return true;
        }

        /// <summary>
        /// Copies a file.
        /// </summary>
        /// <param name="srcFile">The source file.</param>
        /// <param name="destFile">The dest file.</param>
        /// <param name="deleteOldFile">if set to <c>true</c> [deletes src file].</param>
        /// <returns></returns>
        internal static bool CopyFile(string srcFile, string destFile, bool deleteOldFile)
        {
            if (File.Exists(destFile) || !File.Exists(srcFile))
            {
                return false;
            }

            File.Copy(srcFile, destFile);

            if (!File.Exists(destFile))
            {
                return false;
            }

            if (deleteOldFile)
            {
                File.Delete(srcFile);
            }

            return true;
        }

        /// <summary>
        /// Runs the kdiff3.
        /// </summary>
        /// <param name="args">The arguments.</param>
        internal static void RunKDiff3(string args)
        {
            Process process = new Process
            {
                StartInfo =
                {
                    FileName = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) +
                               "\\Tools\\KDiff3\\kdiff3.exe",
                    Arguments = args
                }
            };
            process.Start();
            process.WaitForExit();
        }

        /// <summary>
        /// Deletes a folder.
        /// </summary>
        /// <param name="folder">The folder.</param>
        internal static void DeleteFolder(string folder)
        {
            if (Directory.Exists(folder))
            {
                for (var i = 0; i < 2; i++)
                {
                    try
                    {
                        Directory.Delete(folder, true);
                    }
                    catch (Exception e)
                    {
                        var result =
                            MessageBox.Show("Could not clean " + folder +
                                            " Directory! If you have it open in Window Explorer, please close it!",
                                "KCDModMerger",
                                MessageBoxButton.OK);

                        if (result == MessageBoxResult.OK || result == MessageBoxResult.Yes)
                        {
                            // Try again after 10 Seconds
                            Thread.Sleep(10000);
                        }
                        else if (result == MessageBoxResult.Cancel || result == MessageBoxResult.No)
                        {
                            MessageBox.Show("Could not clean " + folder +
                                            " Directory! Future Operation of this program is not guaranteed!",
                                "KCDModMerger", MessageBoxButton.OK);
                            return;
                        }
                    }

                    if (!Directory.Exists(folder))
                    {
                        break;
                    }
                }

                if (!Directory.Exists(folder))
                {
                    Logging.Logger.Log("Deleted " + folder + " Directory!");
                }
                else
                {
                    Logging.Logger.Log("Tried deleting " + folder + " Directory but failed!");
                    MessageBox.Show("Could not clean " + folder +
                                    " Directory! Future Operation of this program is not guaranteed!",
                        "KCDModMerger", MessageBoxButton.OK);
                }
            }
        }

        /// <summary>
        /// Prints the information.
        /// </summary>
        internal static void PrintInfo()
        {
            Logger.Log("Operating System Information!");
            Logger.Log("----------------------------");
            Logger.Log(string.Format("Name: {0}", OSVersionInfo.Name), true);
            Logger.Log(string.Format("Edition: {0}", OSVersionInfo.Edition), true);
            if (!string.IsNullOrEmpty(OSVersionInfo.ServicePack))
                Logger.Log(string.Format("Service Pack: {0}", OSVersionInfo.ServicePack), true);
            else
                Logger.Log("Service Pack: None!");
            Logger.Log(string.Format("Version: {0}", OSVersionInfo.VersionString), true);
            Logger.Log(".Net Version: " + GetDotNetVerson(), true);

            Logger.Log(GetProcessorInfo());

            Logger.Log(GetGPUInfo());

            NativeMethods.GetPhysicallyInstalledSystemMemory(out var memKb);
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

        /// <summary>
        /// Gets the processor information.
        /// </summary>
        /// <returns></returns>
        private static string GetProcessorInfo()
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

        /// <summary>
        /// Gets the gpu information.
        /// </summary>
        /// <returns></returns>
        private static string GetGPUInfo()
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

        /// <summary>
        /// Converts to highest.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Gets the dot net verson.
        /// </summary>
        /// <returns></returns>
        internal static string GetDotNetVerson()
        {
            var maxDotNetVersion = GetVersionFromRegistry();
            if (string.Compare(maxDotNetVersion, "4.5") >= 0)
            {
                var v45Plus = Get45PlusFromRegistry();
                if (v45Plus != "") maxDotNetVersion = v45Plus;
            }

            return maxDotNetVersion;
        }

        private static string Get45PlusFromRegistry()
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
        private static string CheckFor45PlusVersion(int releaseKey)
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

        private static string GetVersionFromRegistry()
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
    }
}