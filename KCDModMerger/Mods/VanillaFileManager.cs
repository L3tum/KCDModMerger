#region usings

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using KCDModMerger.Logging;
using Newtonsoft.Json;

#endregion

namespace KCDModMerger.Mods
{
    internal class VanillaFileManager
    {
        private readonly string kcdFolder;
        private Dictionary<string, List<string>> saved;

        internal VanillaFileManager(string kcdFolder)
        {
            this.kcdFolder = kcdFolder;

            LoadSavedPaths();
        }

        ~VanillaFileManager()
        {
            var file = AppDomain.CurrentDomain.BaseDirectory + "\\VanillaFiles.json";
            if (File.Exists(file))
            {
                File.Delete(file);
            }

            using (StreamWriter sw = File.CreateText(file))
            {
                sw.Write(JsonConvert.SerializeObject(saved, Formatting.Indented));
            }
        }

        /// <summary>
        /// Finds a pak by file.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        internal string FindPakByFile(string file)
        {
            var savedPak = saved.FirstOrDefault(pair => pair.Value.Contains(file));

            if (!savedPak.IsDefault())
            {
                if (File.Exists(savedPak.Key))
                {
                    using (FileStream fs = File.OpenRead(savedPak.Key))
                    {
                        using (ZipArchive zip = new ZipArchive(fs))
                        {
                            var entry = zip.Entries.FirstOrDefault(zipEntry => zipEntry.FullName == file);

                            if (entry != null)
                            {
                                // No need to save, nothing changed
                                return savedPak.Key;
                            }

                            savedPak.Value.Remove(file);
                        }
                    }
                }
                else
                {
                    saved.Remove(savedPak.Key);
                }
            }

            return SearchForPakByFile(file);
        }

        private string SearchForPakByFile(string file)
        {
            var paks = Directory.GetFiles(kcdFolder + "\\Data", "*.pak", SearchOption.AllDirectories);

            foreach (string pak in paks)
            {
                using (FileStream fs = File.Open(pak, FileMode.Open))
                {
                    try
                    {
                        using (ZipArchive zip = new ZipArchive(fs))
                        {
                            if (zip.Entries.Count > 0)
                            {
                                saved[pak] = new List<string>();
                                foreach (ZipArchiveEntry zipArchiveEntry in zip.Entries)
                                {
                                    saved[pak].Add(zipArchiveEntry.FullName);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.Logger.LogWarn(e.Message, WarnSeverity.Low, false);
                    }
                }
            }


            var srcFile = saved.FirstOrDefault(pair => pair.Value.Contains(file));

            if (!srcFile.IsDefault())
            {
                return srcFile.Key;
            }

            return "";
        }

        /// <summary>
        /// Extracts a vanilla file.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        internal string ExtractVanillaFile(ModFile file)
        {
            if (file.IsLocalization)
            {
                return ExtractLocalizationFile(file);
            }

            return ExtractDataFile(file);
        }

        private string ExtractDataFile(ModFile file)
        {
            var pakFile = FindPakByFile(file.FileName);

            if (!string.IsNullOrEmpty(pakFile))
            {
                using (FileStream fs = new FileStream(pakFile, FileMode.Open))
                {
                    using (ZipArchive zip = new ZipArchive(fs))
                    {
                        var zippedFile = zip.Entries.FirstOrDefault(entry => entry.FullName == file.FileName);

                        if (zippedFile != null)
                        {
                            var destFolder = ModManager.directoryManager.CreateDirectories(file);

                            using (FileStream destFile =
                                File.Open(destFolder + "\\" + file.FileName.Split('/').Last(), FileMode.OpenOrCreate))
                            {
                                using (Stream srcFile = zippedFile.Open())
                                {
                                    srcFile.CopyTo(destFile);
                                }
                            }

                            return destFolder + "\\" + file.FileName.Split('/').Last();
                        }
                    }
                }
            }

            return "";
        }

        private string ExtractLocalizationFile(ModFile file)
        {
            var pakFilePath = kcdFolder + "\\Localization\\" + file.PakFileName;

            if (File.Exists(pakFilePath))
            {
                var fileName = file.IsLocalization
                    ? file.FileName.Split('\\').Last()
                    : file.FileName;

                using (FileStream fs = new FileStream(pakFilePath, FileMode.Open))
                {
                    using (ZipArchive zip = new ZipArchive(fs))
                    {
                        var zippedFile = zip.Entries.FirstOrDefault(entry => entry.FullName == fileName);

                        if (zippedFile != null)
                        {
                            var destFolder = ModManager.directoryManager.CreateDirectories(file);

                            using (FileStream destFile = File.Open(destFolder + "\\" + file.FileName.Split('/').Last(),
                                FileMode.OpenOrCreate))
                            {
                                using (Stream srcFile = zippedFile.Open())
                                {
                                    srcFile.CopyTo(destFile);
                                }
                            }

                            return destFolder + "\\" + file.FileName.Split('/').Last();
                        }
                    }
                }
            }

            return "";
        }

        private void LoadSavedPaths()
        {
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\VanillaFiles.json"))
            {
                using (StreamReader fs =
                    new StreamReader(AppDomain.CurrentDomain.BaseDirectory + "\\VanillaFiles.json"))
                {
                    var json = fs.ReadToEnd();
                    saved = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
                }
            }
            else
            {
                saved = new Dictionary<string, List<string>>();
            }
        }
    }
}