#region usings

using System;
using System.Collections.Generic;
using System.IO;
using KCDModMerger.Logging;

#endregion

namespace KCDModMerger.Mods
{
    [LogInterceptor]
    internal class ModConfig
    {
        private readonly List<string> config = new List<string>();
        internal readonly string file;

        internal ModConfig(string file)
        {
            this.file = file;

            if (File.Exists(file)) ReadConfig();
        }

        private void ReadConfig()
        {
            using (var sr = new StreamReader(file))
            {
                var content = sr.ReadToEnd();
                var lines = content.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None);

                foreach (var line in lines)
                {
                    var parts = line.Split('=');

                    if (parts.Length > 1) config.Add(parts[0].Trim());
                }
            }
        }

        /// <summary>
        ///     Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///     <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (obj.GetType() == GetType())
            {
                var converted = (ModConfig) obj;

                foreach (var key in converted.config)
                    if (config.Contains(key))
                        return true;
            }

            return false;
        }

        public override int GetHashCode()
        {
            var hashCode = -1627070430;
            hashCode = hashCode * -1521134295 +
                       EqualityComparer<List<string>>.Default.GetHashCode(config);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(file);
            return hashCode;
        }
    }
}