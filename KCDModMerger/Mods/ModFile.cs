#region usings

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using KCDModMerger.Logging;
using Newtonsoft.Json;

#endregion

namespace KCDModMerger.Mods
{
    /// <summary>
    /// ModFile class
    /// </summary>
    [LogInterceptor]
    public class ModFile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModFile"/> class.
        /// </summary>
        /// <param name="modName">Name of the mod.</param>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="pakFileName">Name of the pak file.</param>
        /// <param name="pakFilePath">The pak file path.</param>
        /// <param name="isVanilla">if set to <c>true</c> [is vanilla].</param>
        /// <param name="isLocalization">if set to <c>true</c> [is localization].</param>
        /// <param name="isExtracted">if set to <c>true</c> [is extracted].</param>
        public ModFile(string modName, string fileName, string pakFileName, string pakFilePath, bool isVanilla,
            bool isLocalization, bool isExtracted = false)
        {
            ModName = modName;
            FileName = fileName;
            PakFileName = pakFileName;
            PakFilePath = pakFilePath;
            IsVanilla = isVanilla;
            IsLocalization = isLocalization;
            IsExtracted = isExtracted;

            DisplayName = (IsLocalization ? PakFileName + "\\" : "") + FileName;
        }

        internal string ModName { get; }
        internal string FileName { get; }
        internal string PakFileName { get; }
        internal string PakFilePath { get; }
        internal string DisplayName { get; }
        internal bool IsVanilla { get; }
        internal bool IsLocalization { get; }
        internal bool IsExtracted { get; }

        /// <summary>
        /// Deletes this instance.
        /// </summary>
        [Log]
        public void Delete()
        {
            Logging.Logger.Log("Deleting " + DisplayName + "(" + ModName + ")");
            var file = PakFilePath + "\\" + PakFileName;

            if (File.Exists(file))
            {
                using (FileStream fs = File.Open(file, FileMode.Open))
                {
                    using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Update))
                    {
                        var entry = zip.Entries.FirstOrDefault(archiveEntry => archiveEntry.FullName == FileName);

                        entry?.Delete();
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        [Log]
        public override bool Equals(object obj)
        {
            if (obj.GetType() == GetType())
            {
                var converted = ((ModFile) obj);
                return converted.FileName == FileName && 
                       converted.ModName == ModName &&
                       converted.PakFileName == PakFileName && 
                       converted.IsVanilla == IsVanilla && 
                       converted.IsLocalization == IsLocalization && 
                       PakFilePath == converted.PakFilePath;
            }

            return false;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        [Log]
        public override int GetHashCode()
        {
            var hashCode = -1445040340;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ModName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FileName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(PakFileName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(PakFilePath);
            hashCode = hashCode * -1521134295 + IsVanilla.GetHashCode();
            hashCode = hashCode * -1521134295 + IsLocalization.GetHashCode();
            return hashCode;
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        [Log]
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }


    }
}