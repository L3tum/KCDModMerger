using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace KCDModMerger.Tests
{
    [TestFixture]
    class Utilties
    {
        [TestCase((long) 1, "1 Bytes")]
        [TestCase((long) 1024, "1 KiB")]
        [TestCase((long) 1024 * 1024, "1 MiB")]
        [TestCase((long) 1024 * 1024 * 1024, "1 GiB")]
        [TestCase((long) 1024 * 1024 * 1024 * 1024, "1 TiB")]
        public void ConvertToHighest(long bytes, string result)
        {
            var res = KCDModMerger.Utilities.ConvertToHighest(bytes);

            Assert.AreEqual(result, res);
        }

        [Test]
        public void DeleteFolder()
        {
            var folder = AppDomain.CurrentDomain.BaseDirectory + "\\TestDeleteFolder";

            Directory.CreateDirectory(folder);

            Utilities.DeleteFolder(folder);

            DirectoryAssert.DoesNotExist(folder);

            if (Directory.Exists(folder))
            {
                Directory.Delete(folder);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CopyFile(bool deleteOld)
        {
            var src = AppDomain.CurrentDomain.BaseDirectory + "\\test.txt";
            var dest = AppDomain.CurrentDomain.BaseDirectory + "\\test.dest.txt";

            File.CreateText(src).Dispose();

            var res = Utilities.CopyFile(src, dest, deleteOld);

            Assert.IsTrue(res);

            if (deleteOld)
            {
                FileAssert.DoesNotExist(src);
            }
            else
            {
                File.Delete(src);
            }

            FileAssert.Exists(dest);

            File.Delete(dest);
        }
    }
}