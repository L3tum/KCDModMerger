using System;
using System.Collections.Generic;
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
    }
}