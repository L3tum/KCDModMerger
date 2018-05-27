#region usings

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#endregion

namespace KCDModMerger.Mods
{
    internal class ModLoader
    {
        private readonly string modfolder;
        private readonly string disabledModFolder;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModLoader"/> class.
        /// </summary>
        /// <param name="modFolder">The mod folder.</param>
        internal ModLoader(string modFolder, string disabledModFolder)
        {
            modfolder = modFolder;
            this.disabledModFolder = disabledModFolder;
        }

        /// <summary>
        /// Loads the mods.
        /// </summary>
        /// <returns></returns>
        internal Mod[] LoadMods()
        {
            List<Mod> mods = new List<Mod>();

            if (!Directory.Exists(modfolder) && !Directory.Exists(disabledModFolder))
            {
                return Array.Empty<Mod>();
            }

            var modFolders = Directory.GetDirectories(modfolder);
            modFolders = modFolders.Concat(Directory.GetDirectories(disabledModFolder)).ToArray();

            Logging.Logger.Log("Found " + modFolders.Length + " Folders!");

            foreach (string modFolder in modFolders)
            {
                var files = Directory.GetFiles(modFolder);

                if (files.Any(entry => entry.EndsWith(".manifest") || entry.EndsWith(".pak")))
                {
                    mods.Add(new Mod(modFolder));
                }
            }

            return mods.ToArray();
        }
    }
}