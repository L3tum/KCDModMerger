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

        /// <summary>
        /// Initializes a new instance of the <see cref="ModLoader"/> class.
        /// </summary>
        /// <param name="modFolder">The mod folder.</param>
        internal ModLoader(string modFolder)
        {
            modfolder = modFolder;
        }

        /// <summary>
        /// Loads the mods.
        /// </summary>
        /// <returns></returns>
        internal Mod[] LoadMods()
        {
            List<Mod> mods = new List<Mod>();

            if (!Directory.Exists(modfolder))
            {
                return Array.Empty<Mod>();
            }

            var modFolders = Directory.GetDirectories(modfolder);

            Logging.Logger.Log("Found " + modFolders.Length + " Folders in Mods Directory!");

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