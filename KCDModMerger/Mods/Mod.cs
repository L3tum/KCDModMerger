#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using KCDModMerger.Logging;
using Schematrix;

#endregion

namespace KCDModMerger.Mods
{
    [LogInterceptor]
    internal class Mod
    {
        private readonly string disabledFolderName;

        private readonly string[] DISALLOWED_FILETYPES =
            {".tbl", ".skin", ".dds", ".cgf", ".cdf", ".chr", ".usm", ".sqcb", ".1", ".2", ".3", ".4", ".5"};

        internal ModManifest manifest;

        internal ModConfig config;

        internal ModStatus Status;


        /// <summary>
        ///     Initializes a new instance of the <see cref="Mod" /> class.
        /// </summary>
        /// <param name="folderName">Name of the folder.</param>
        public Mod(string folderName)
        {
            manifest = new ModManifest(folderName + "\\mod.manifest");
            config = new ModConfig(folderName + "\\mod.cfg");

            if (folderName.Contains(ModManager.directoryManager.disabledModDirectory))
            {
                FolderName = ModManager.directoryManager.modDirectory + "\\" + folderName.Split('\\').Last();
                disabledFolderName = folderName;
                Status = ModStatus.Disabled;
            }
            else
            {
                FolderName = folderName;
                disabledFolderName = ModManager.directoryManager.disabledModDirectory + "\\" +
                                     FolderName.Split('\\').Last();
                Folders = GetFolders(FolderName);
                // self-documented
                ManagePaks();
                Status = ModStatus.Enabled;
            }
        }

        /// <summary>
        ///     Changes the status.
        /// </summary>
        /// <param name="status">The status.</param>
        internal void ChangeStatus(ModStatus status)
        {
            if (status == ModStatus.Enabled && Status == ModStatus.Disabled)
            {
                ModManager.directoryManager.CopyDirectory(disabledFolderName, FolderName);
                Directory.Delete(disabledFolderName, true);
                Folders = GetFolders(FolderName);
                ManagePaks();
                Status = ModStatus.Enabled;
            }
            else if (status == ModStatus.Disabled && Status == ModStatus.Enabled)
            {
                ModManager.directoryManager.CopyDirectory(FolderName, disabledFolderName);
                Directory.Delete(FolderName, true);
                DataFiles = Array.Empty<ModFile>();
                Status = ModStatus.Disabled;
            }
        }

        /// <summary>
        ///     Finds the paks.
        /// </summary>
        private void FindPaks()
        {
            var folderFiles = new List<ModFile>();

            foreach (var folder in Folders)
            {
                var paks = GetPaks(folder);

                foreach (var pak in paks)
                {
                    var parts = pak.Split('\\');
                    var pakFileName = parts.Last();
                    var pakFilePath = pak.Replace("\\" + pakFileName, "");
                    var isLocalization = parts[parts.Length - 2].Equals("Localization");

                    folderFiles.Add(new ModFile(manifest.DisplayName, "", pakFileName, pakFilePath, false,
                        isLocalization));
                }
            }

            Files = folderFiles.ToArray();
        }

        /// <summary>
        ///     Finds the files in zip.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        private List<ModFile> FindFilesInZIP(ModFile file)
        {
            var zippedFiles = new List<ModFile>();

            using (var fs = File.Open(file.PakFilePath + "\\" + file.PakFileName, FileMode.Open))
            {
                using (var zip = new ZipArchive(fs))
                {
                    foreach (var entry in zip.Entries)
                        if (entry.FullName.Contains(".") && !DISALLOWED_FILETYPES.Any(s => entry.FullName.EndsWith(s)))
                            if (file.IsLocalization)
                                zippedFiles.Add(new ModFile(manifest.DisplayName, entry.FullName, file.PakFileName,
                                    file.PakFilePath, false, true));
                            else
                                zippedFiles.Add(new ModFile(manifest.DisplayName, entry.FullName, file.PakFileName,
                                    file.PakFilePath, false, false));
                }
            }

            return zippedFiles;
        }

        /// <summary>
        ///     Manages the paks.
        /// </summary>
        private void ManagePaks()
        {
            FindPaks();

            var zippedFiles = new List<ModFile>();
            foreach (var file in Files)
            {
                var newPath = file.PakFilePath + "\\" + file.PakFileName + ".extracted";
                var oldPath = file.PakFilePath + "\\" + file.PakFileName;

                // Replace RAR with ZIP
                if (CheckForRar(oldPath))
                {
                    Directory.CreateDirectory(file.PakFilePath + "\\TEMP_EXTRACT");
                    using (var rar = new Unrar(file.FileName))
                    {
                        rar.Open(Unrar.OpenMode.Extract);
                        rar.ReadHeader();
                        while (rar.CurrentFile != null)
                        {
                            rar.ExtractToDirectory(file.PakFilePath + "\\TEMP_EXTRACT");
                            rar.ReadHeader();
                        }

                        rar.Close();
                    }

                    ZipFile.CreateFromDirectory(file.PakFilePath + "\\TEMP_EXTRACT", newPath);

                    Directory.Delete(file.PakFilePath + "\\TEMP_EXTRACT", true);

                    if (File.Exists(newPath) &&
                        new FileInfo(newPath).Length > 30)
                    {
                        File.Move(oldPath, oldPath + ".backup");
                        File.Move(newPath, oldPath);
                    }
                    else
                    {
                        MessageBox.Show(
                            "Critical Error: ZIP does not contain any data! Check the file format and perhaps manually convert it to ZIP!",
                            "KCDModMerger", MessageBoxButton.OK);
                    }
                }

                zippedFiles.AddRange(FindFilesInZIP(file));
            }

            DataFiles = zippedFiles.ToArray();
        }

        /// <summary>
        ///     Checks for rar.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        private bool CheckForRar(string file)
        {
            if (!File.Exists(file)) return false;

            using (var sr = new StreamReader(file))
            {
                if (sr.Peek() >= 0)
                {
                    var buffer = new char[5];

                    sr.Read(buffer, 0, 5);

                    return string.Join("", buffer).Replace(" ", "").ToLower().Contains("rar");
                }

                return false;
            }
        }

        /// <summary>
        ///     Gets all folders.
        /// </summary>
        /// <param name="baseFolder">The base folder.</param>
        /// <returns></returns>
        private string[] GetFolders(string baseFolder)
        {
            return Directory.GetDirectories(baseFolder);
        }

        /// <summary>
        ///     Gets all paks.
        /// </summary>
        /// <param name="folder">The folder.</param>
        /// <returns></returns>
        private string[] GetPaks(string folder)
        {
            return Directory.GetFiles(folder, "*.pak");
        }

        #region Properties

        /// <summary>
        ///     Paks in the Mod Directory
        /// </summary>
        /// <value>
        ///     The files.
        /// </value>
        public ModFile[] Files { get; set; } = Array.Empty<ModFile>();

        public string FolderName { get; set; } = "";

        public string[] Folders { get; set; } = Array.Empty<string>();

        public string[] MergedFiles { get; set; } = Array.Empty<string>();

        /// <summary>
        ///     Actual files in the Paks in the Mod Directory
        /// </summary>
        /// <value>
        ///     The data files.
        /// </value>
        public ModFile[] DataFiles { get; set; } = Array.Empty<ModFile>();

        #endregion
    }
}