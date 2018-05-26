#region usings

using System.IO;
using System.IO.Compression;
using System.Linq;
using KCDModMerger.Properties;

#endregion

namespace KCDModMerger.Mods
{
    internal class DirectoryManager
    {
        internal const string TEMP_FILES = "\\TempFiles";
        internal const string TEMP_MERGED_DIR = "\\MergedFiles";
        internal const string MODMANAGER_DIR = "\\ModMerger";
        internal const string MERGED_MOD = "\\zzz_ModMerger";

        private readonly string kcdFolder;
        private readonly string kcdModManager;
        private readonly string kcdTempFiles;
        private string kcdMerged;
        private readonly string kcdTempMerged;

        internal DirectoryManager(string kcdFolder)
        {
            this.kcdFolder = kcdFolder;
            kcdModManager = kcdFolder + MODMANAGER_DIR;
            kcdTempFiles = kcdFolder + MODMANAGER_DIR + TEMP_FILES;
            kcdTempMerged = kcdFolder + MODMANAGER_DIR + TEMP_MERGED_DIR;
            kcdMerged = kcdFolder + "\\Mods" + MERGED_MOD;

            LogTotalFreeSpace();

            CreateModdingDirectories();
        }

        private void CreateModdingDirectories()
        {
            Utilities.DeleteFolder(kcdModManager);

            var dir = Directory.CreateDirectory(kcdModManager);

            var subDir = dir.CreateSubdirectory(TEMP_FILES.Replace("\\", ""));

            var mergedDir = dir.CreateSubdirectory(TEMP_MERGED_DIR.Replace("\\", ""));
            mergedDir.CreateSubdirectory("Localization");
            mergedDir.CreateSubdirectory("Data");

            var vanillaDir = subDir.CreateSubdirectory("Vanilla");
            vanillaDir.CreateSubdirectory("Localization");
            vanillaDir.CreateSubdirectory("Data");
        }

        internal string CreateDirectories(ModFile file)
        {
            var rootFolder = kcdTempFiles + "\\" + file.ModName;

            var dir = Directory.CreateDirectory(rootFolder);
            var finalPath = "";

            if (file.IsLocalization)
            {
                var subDir = dir.CreateSubdirectory("Localization");

                subDir = subDir.CreateSubdirectory(file.PakFileName.Replace(".pak", ""));
                finalPath = subDir.FullName;
            }
            else
            {
                var subDir = dir.CreateSubdirectory("Data");
                var filePath = file.FileName.Split('/');

                if (filePath.Length > 1)
                {
                    for (var i = 0; i < filePath.Length - 1; i++)
                    {
                        subDir = subDir.CreateSubdirectory(filePath[i]);
                    }
                }

                finalPath = subDir.FullName;
            }

            return finalPath;
        }

        /// <summary>
        /// Extracts the file from zip.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        internal string ExtractFile(ModFile file)
        {
            var filePath = file.PakFilePath + "\\" + file.PakFileName;
            if (File.Exists(filePath))
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open))
                {
                    using (ZipArchive zip = new ZipArchive(fs))
                    {
                        var zippedFile = zip.Entries.FirstOrDefault(entry => entry.FullName == file.FileName);

                        if (zippedFile != null)
                        {
                            var destFolder = CreateDirectories(file);

                            using (FileStream destFile = File.Open(destFolder + "\\" + file.FileName,
                                FileMode.OpenOrCreate))
                            {
                                using (Stream srcFile = zippedFile.Open())
                                {
                                    srcFile.CopyTo(destFile);
                                }
                            }

                            return destFolder + "\\" + file.FileName;
                        }
                    }
                }
            }

            return "";
        }

        /// <summary>
        /// Paks the directory.
        /// </summary>
        /// <param name="directory">The directory.</param>
        /// <returns></returns>
        internal string PakDirectory(string directory)
        {
            var dirName = directory.Split('\\').Last();
            var rootDir = kcdTempMerged + "\\Merged";
            var dir = Directory.CreateDirectory(rootDir);

            dir = dir.CreateSubdirectory(dirName);
            
            ZipFile.CreateFromDirectory(directory, dir.FullName + "\\" + dirName.ToLower() + ".pak");
            Logging.Logger.Log($"Packed {dirName} Archive!");

            return dir.FullName + "\\" + dirName.ToLower() + ".pak";
        }

        /// <summary>
        /// Logs the total free space.
        /// </summary>
        private void LogTotalFreeSpace()
        {
            var driveName = kcdFolder.Split('\\').First() + "\\";

            var drives = DriveInfo.GetDrives();

            var drive = drives.FirstOrDefault(info => info.IsReady && info.Name == driveName);

            if (drive != null)
            {
                Logging.Logger.Log(
                    "Available Space on " + driveName + " is " + Utilities.ConvertToHighest(drive.AvailableFreeSpace),
                    true);
            }
        }

        ~DirectoryManager()
        {
            Utilities.DeleteFolder(kcdModManager);
        }
    }
}