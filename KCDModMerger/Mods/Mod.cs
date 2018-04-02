#region usings

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows;
using Schematrix;

#endregion

namespace KCDModMerger.Mods
{
    internal class Mod
    {
        private readonly string[] DISALLOWED_FILETYPES =
            {".tbl", ".skin", ".dds", ".cgf", ".cdf", ".chr", ".usm", ".sqcb", ".1", ".2", ".3", ".4", ".5"};

        internal ModManifest manifest;


        /// <summary>
        ///     Initializes a new instance of the <see cref="Mod" /> class.
        /// </summary>
        /// <param name="folderName">Name of the folder.</param>
        /// <param name="logger">The logger.</param>
        public Mod(string folderName, StringBuilder logger = null)
        {
            var flush = false;
            StringBuilder logBuilder;

            if (logger != null)
            {
                logBuilder = logger;
            }
            else
            {
                logBuilder = new StringBuilder();
                flush = true;
            }

            logBuilder.AppendLine(Logger.BuildLogWithDate("Initializing Mod " + folderName.Split('\\').Last()));
            logBuilder.AppendLine(Logger.BuildLogWithDate("Folder: " + folderName, true));

            FolderName = folderName;

            logBuilder.AppendLine(Logger.BuildLogWithDate("Initializing Manifest"));

            manifest = new ModManifest(folderName + "\\mod.manifest", logBuilder);
            manifest.DisplayName = folderName;
            manifest.ReadManifest();

            logBuilder.AppendLine(Logger.BuildLogWithDate("Initialized Manifest!"));

            Folders = GetFolders(folderName);

            // self-documented
            ManagePaks(logBuilder);

            logBuilder.AppendLine(Logger.BuildLogWithDate("Initialized Mod " + folderName, true));

            if (flush) Logger.Log(logBuilder);
        }

        /// <summary>
        ///     Finds the paks.
        /// </summary>
        /// <param name="logBuilder">The log builder.</param>
        private void FindPaks(StringBuilder logBuilder)
        {
            logBuilder.AppendLine(Logger.BuildLogWithDate("Searching for Paks"));

            var folderFiles = new List<ModFile>();

            foreach (var folder in Folders)
            {
                logBuilder.AppendLine(Logger.BuildLogWithDate("Searching for Paks in " + folder.Split('\\').Last()));
                var paks = GetPaks(folder);

                logBuilder.AppendLine(
                    Logger.BuildLogWithDate("Found " + paks.Length + " Paks in " + folder.Split('\\').Last(), true));

                foreach (var pak in paks)
                {
                    var parts = pak.Split('\\');
                    var filename = parts[parts.Length - 3] + "\\" + parts[parts.Length - 2] + "\\" +
                                   parts[parts.Length - 1];

                    logBuilder.AppendLine(Logger.BuildLogWithDate("Found " + filename, true));
                    folderFiles.Add(new ModFile(pak, manifest.DisplayName, folder, pak));
                }
            }

            logBuilder.AppendLine(Logger.BuildLogWithDate("Found total of " + folderFiles.Count, true));

            Files = folderFiles.ToArray();
        }

        /// <summary>
        ///     Finds the files in zip.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="filename">The filename.</param>
        /// <param name="logBuilder">The log builder.</param>
        /// <returns></returns>
        private List<ModFile> FindFilesInZIP(ModFile file, string filename, StringBuilder logBuilder)
        {
            logBuilder.AppendLine(Logger.BuildLogWithDate("Searching in " + filename));
            var zippedFiles = new List<ModFile>();

            using (var fs = File.Open(file.FileName, FileMode.Open))
            {
                using (var zip = new ZipArchive(fs))
                {
                    foreach (var entry in zip.Entries)
                        if (entry.FullName.Contains(".") && !DISALLOWED_FILETYPES.Any(s => entry.FullName.EndsWith(s)))
                            if (file.FilePath.EndsWith("Localization"))
                            {
                                logBuilder.AppendLine(Logger.BuildLogWithDate(
                                    "Found Localization File " + file.FileName.Split('\\').Last() + "\\" +
                                    entry.FullName, true));
                                zippedFiles.Add(new ModFile(
                                    file.FileName.Split('\\').Last() + "\\" + entry.FullName,
                                    manifest.DisplayName,
                                    file.FilePath, file.FileName));
                            }
                            else
                            {
                                logBuilder.AppendLine(Logger.BuildLogWithDate(
                                    "Found Data File " + entry.FullName,
                                    true));
                                zippedFiles.Add(new ModFile(entry.FullName, manifest.DisplayName, file.FilePath,
                                    file.FileName));
                            }
                }
            }

            if (zippedFiles.Count == 0) logBuilder.AppendLine(Logger.BuildLogWithDate("Found 0 Files!"));

            return zippedFiles;
        }

        /// <summary>
        ///     Manages the paks.
        /// </summary>
        private void ManagePaks(StringBuilder logBuilder)
        {
            logBuilder.AppendLine(Logger.BuildLogWithDate("Starting to manage Paks"));

            FindPaks(logBuilder);

            logBuilder.AppendLine(Logger.BuildLogWithDate("Searching for actual files in Paks"));

            var zippedFiles = new List<ModFile>();
            foreach (var file in Files)
            {
                var parts = file.FileName.Split('\\');
                var filename = parts[parts.Length - 3] + "\\" + parts[parts.Length - 2] + "\\" +
                               parts[parts.Length - 1];

                var isRAR = CheckForRar(file.FileName, filename, logBuilder);

                // Replace RAR with ZIP
                if (isRAR)
                {
                    logBuilder.AppendLine(Logger.BuildLogWithDate("Replacing RAR with ZIP"));

                    Directory.CreateDirectory(file.FilePath + "\\TEMP_EXTRACT");
                    using (var rar = new Unrar(file.FileName))
                    {
                        rar.Open(Unrar.OpenMode.Extract);
                        rar.ReadHeader();
                        while (rar.CurrentFile != null)
                        {
                            rar.ExtractToDirectory(file.FilePath + "\\TEMP_EXTRACT");
                            rar.ReadHeader();
                        }

                        rar.Close();
                    }

                    logBuilder.AppendLine(Logger.BuildLogWithDate("Extracted RAR!"));

                    ZipFile.CreateFromDirectory(file.FilePath + "\\TEMP_EXTRACT", file.FileName + ".extracted");

                    Directory.Delete(file.FilePath + "\\TEMP_EXTRACT", true);

                    logBuilder.AppendLine(Logger.BuildLogWithDate("Created ZIP!"));

                    if (File.Exists(file.FileName + ".extracted") &&
                        new FileInfo(file.FileName + ".extracted").Length > 30)
                    {
                        logBuilder.AppendLine(Logger.BuildLogWithDate("ZIP seems valid! Replacing RAR"));
                        File.Move(file.FileName, file.FileName + ".backup");
                        File.Move(file.FileName + ".extracted", file.FileName);
                        logBuilder.AppendLine(Logger.BuildLogWithDate("Replaced RAR!"));
                    }
                    else
                    {
                        logBuilder.AppendLine(Logger.BuildLogWithDate("ZIP does not contain any data!"));
                        logBuilder.AppendLine(Logger.BuildLogWithDate("Critical Error: This should not be the case. Check the file format and perhaps manually convert it to ZIP!"));
                        MessageBox.Show(
                            "Critical Error: ZIP does not contain any data! Check the file format and perhaps manually convert it to ZIP!",
                            "KCDModMerger", MessageBoxButton.OK);
                    }
                }

                zippedFiles.AddRange(FindFilesInZIP(file, filename, logBuilder));
            }

            logBuilder.AppendLine(Logger.BuildLogWithDate("Found total of " + zippedFiles.Count, true));

            DataFiles = zippedFiles.ToArray();
        }

        /// <summary>
        ///     Checks for rar.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="filename">The filename.</param>
        /// <param name="logBuilder">The log builder.</param>
        /// <returns></returns>
        private bool CheckForRar(string file, string filename, StringBuilder logBuilder)
        {
            logBuilder.AppendLine(Logger.BuildLogWithDate("Checking " + filename + " for RAR Encoding"));

            if (File.Exists(file))
                using (var sr = new StreamReader(file))
                {
                    if (sr.Peek() >= 0)
                    {
                        var buffer = new char[5];

                        sr.Read(buffer, 0, 5);

                        logBuilder.AppendLine(Logger.BuildLogWithDate("The buffer: " + string.Join(", ", buffer),
                            true));

                        if (string.Join("", buffer).Replace(" ", "").ToLower().Contains("rar"))
                        {
                            logBuilder.AppendLine(Logger.BuildLogWithDate(filename + " is a RAR (F*** you)!"));
                            return true;
                        }

                        logBuilder.AppendLine(Logger.BuildLogWithDate(filename + " is not a RAR at least!"));
                        return false;
                    }

                    logBuilder.AppendLine(Logger.BuildLogWithDate(filename + " is empty?"));
                    return false;
                }

            logBuilder.AppendLine(Logger.BuildLogWithDate(
                filename +
                " does not exist anymore! This is bad and means someone else tampered with the file system during this run."));

            return false;
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

        public ModFile[] Files { get; set; } = new ModFile[0];

        public string FolderName { get; set; } = "";

        public string[] Folders { get; set; } = new string[0];

        public string[] MergedFiles { get; set; } = new string[0];

        public ModFile[] DataFiles { get; set; } = new ModFile[0];

        #endregion
    }
}