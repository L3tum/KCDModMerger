#region usings

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#endregion

namespace KCDModMerger.Mods
{
    /// <summary>
    /// ModFile class
    /// </summary>
    public class ModFile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModFile"/> class.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="modName">Name of the mod.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="pakFile">The pak file.</param>
        public ModFile(string fileName, string modName, string filePath, string pakFile)
        {
            FileName = fileName;
            ModName = modName;
            FilePath = filePath;
            PakFile = pakFile;
        }

        /// <summary>
        /// Gets or sets the name of the file.
        /// </summary>
        /// <value>
        /// The name of the file.
        /// </value>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets the name of the mod.
        /// </summary>
        /// <value>
        /// The name of the mod.
        /// </value>
        public string ModName { get; set; }

        /// <summary>
        /// Gets or sets the file path.
        /// </summary>
        /// <value>
        /// The file path.
        /// </value>
        public string FilePath { get; set; }

        /// <summary>
        /// Gets or sets the pak file.
        /// </summary>
        /// <value>
        /// The pak file.
        /// </value>
        public string PakFile { get; set; }

        /// <summary>
        /// Deletes this instance.
        /// </summary>
        public void Delete()
        {
            Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine(Logger.BuildLogWithDate("Deleting " + FileName + " in " + PakFile.Split('\\').Last() +
                                                      "(" +
                                                      ModName +
                                                      ")"));
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
                                sb.AppendLine(Logger.BuildLogWithDate(
                                    "Deleted " + FileName + " in " + PakFile.Split('\\').Last() + "(" + ModName +
                                    ")!"));
                            }
                            else
                            {
                                sb.AppendLine(Logger.BuildLogWithDate(
                                    "Could not find " + FileName + " in " + PakFile.Split('\\').Last() + "(" + ModName +
                                    ")!"));
                            }
                        }
                    }
                }
                else
                {
                    sb.AppendLine(
                        Logger.BuildLogWithDate(PakFile.Split('\\').Last() + "(" + ModName + ") does not exist!"));
                }

                return sb;
            }).ContinueWith(t => { Logger.Log(t.Result); });
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            var hashCode = 1811777985;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FileName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ModName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FilePath);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(PakFile);
            return hashCode;
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (obj.GetType() == GetType())
            {
                var converted = ((ModFile) obj);
                return converted.FileName == FileName && converted.ModName == ModName &&
                       converted.FilePath == FilePath && PakFile == converted.PakFile;
            }

            return false;
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return "{\"fileName\": \"" + FileName + "\", \"modName\": \"" + ModName + "\", \"filePath\": \"" +
                   FilePath + "\", \"pakFile\": \"" +
                   PakFile + "\"}";
        }
    }
}