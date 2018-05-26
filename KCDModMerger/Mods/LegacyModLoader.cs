#region usings

using System;
using System.IO;
using System.Linq;
using KCDModMerger.Logging;

#endregion

namespace KCDModMerger.Mods
{
    internal class LegacyModLoader
    {
        private readonly string dataFolder;
        private readonly string modsFolder;

        /// <summary>
        /// Initializes a new instance of the <see cref="LegacyModLoader"/> class.
        /// </summary>
        /// <param name="datafolder">The data folder.</param>
        /// <param name="modsFolder">The mods folder.</param>
        internal LegacyModLoader(string datafolder, string modsFolder)
        {
            dataFolder = datafolder;
            this.modsFolder = modsFolder;
        }

        /// <summary>
        /// Updates the legacy mods.
        /// </summary>
        internal void UpdateLegacyMods()
        {
            if (!Directory.Exists(dataFolder))
            {
                return;
            }

            var files = Directory.GetFiles(dataFolder, "zzz*.pak");

            Logging.Logger.Log("Found " + files.Length + " Legacy Mods!");

            foreach (string file in files)
            {
                UpdateLegacyMod(file);
            }
        }

        /// <summary>
        /// Updates the legacy mod.
        /// </summary>
        /// <param name="file">The file.</param>
        private void UpdateLegacyMod(string file)
        {
            var fileName = file.Split('\\').Last();
            var modName = fileName.Replace("zzz_", "").Replace("zzz", "").Replace(".pak", "");
            var modDirectory = modsFolder + "\\" + modName;
            var dir = Utilities.CreateDirectory(modDirectory);

            if (dir == null)
            {
                Logging.Logger.LogWarn("Something went wrong when creating Directory " + modDirectory, WarnSeverity.Mid, true);
                return;
            }

            var dataDir = Utilities.CreateDirectory(dir.Name + "\\" + "Data");

            if (dataDir == null)
            {
                Logging.Logger.LogWarn("Something went wrong when creating Directory " + dir.Name + "\\" + "Data", WarnSeverity.Mid, true);
                return;
            }

            if (!Utilities.WriteManifest(dir.Name + "\\mod.manifest", modName, "Extracted by KCDModMerger",
                ModManager.VERSION, "KCDModMerger", DateTime.Now.ToLongDateString()))
            {
                Logging.Logger.LogWarn("Something went wrong when writing manifest!");
            }

            if (!Utilities.CopyFile(file, dataDir.Name + "\\" + fileName, true))
            {
                Logging.Logger.LogWarn(
                    "Something went wrong when copying " + file + " to " + dataDir.Name + "\\" + fileName,
                    WarnSeverity.High, true);
            }
        }
    }
}