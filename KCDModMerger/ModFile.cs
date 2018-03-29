using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace KCDModMerger
{
    public class ModFile
    {
        public string FileName { get; set; }
        public string ModName { get; set; }
        public string FilePath { get; set; }
        public string PakFile { get; set; }

        public ModFile(string fileName, string modName, string filePath, string pakFile)
        {
            FileName = fileName;
            ModName = modName;
            FilePath = filePath;
            PakFile = pakFile;
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() == this.GetType())
            {
                var converted = ((ModFile) obj);
                return converted.FileName == this.FileName && converted.ModName == this.ModName &&
                       converted.FilePath == this.FilePath && this.PakFile == converted.PakFile;
            }

            return false;
        }

        public void Delete()
        {
            Logger.Log("Deleting " + FileName + " in " + PakFile.Split('\\').Last() + "(" + ModName + ")");
            if (File.Exists(PakFile))
            {
                using (FileStream fs = File.Open(PakFile, FileMode.Open))
                {
                    using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Update))
                    {
                        var entry = zip.Entries.FirstOrDefault(archiveEntry => archiveEntry.FullName == FileName);

                        if (entry != null)
                        {
                            entry.Delete();
                            Logger.Log(
                                "Deleted " + FileName + " in " + PakFile.Split('\\').Last() + "(" + ModName + ")!");
                        }
                        else
                        {
                            Logger.Log(
                                "Could not find " + FileName + " in " + PakFile.Split('\\').Last() + "(" + ModName +
                                ")!");
                        }
                    }
                }
            }
            else
            {
                Logger.Log(PakFile.Split('\\').Last() + "(" + ModName + ") does not exist!");
            }
        }
    }
}