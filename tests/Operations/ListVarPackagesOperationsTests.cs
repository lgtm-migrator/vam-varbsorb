using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;

namespace Varbsorb.Operations
{
    public class ListVarPackagesOperationsTests
    {
        private const string VamPath = @"C:\Vam";
        private Mock<IConsoleOutput> _consoleOutput;
        private MockFileSystem _fs;

        [SetUp]
        public void Setup()
        {
            _consoleOutput = new Mock<IConsoleOutput>(MockBehavior.Loose);
            _fs = new MockFileSystem();
        }

        [Test]
        public async Task CanExecute()
        {
            _fs.AddFile(@$"{VamPath}\AddonPackages\Author.Package.1.var", new MockFileData(CreateFakeZip()));
            var op = new ListVarPackagesOperation(_consoleOutput.Object, _fs, new SHA1HashingAlgo());

            var files = await op.ExecuteAsync(VamPath);

            Assert.That(files.Select(f => f.Path), Is.EqualTo(new[]{
                @$"{VamPath}\AddonPackages\Author.Package.1.var",
            }));
            Assert.That(files[0].Files.Select(f => $"{f.LocalPath}:{f.Hash}"), Is.EqualTo(new[]{
                @"Custom\Scripts\Author\Script.cslist:caaf6584e2785eaab5abe6403e87ad159e99304d",
                @"Custom\Scripts\Author\Script.cs:1ea5047cde4885663920f9941da15142a3e74919",
            }));
        }

        private byte[] CreateFakeZip()
        {
            using var ms = new MemoryStream();
            using var archive = new ZipArchive(ms, ZipArchiveMode.Create, true);
            CreateFakeZipEntry(archive, @"Custom\Scripts\Author\Script.cslist", "Script.cs");
            CreateFakeZipEntry(archive, @"Custom\Scripts\Author\Script.cs", "public class MyScript : MVRScript { }");
            CreateFakeZipEntry(archive, @"meta.json", "{}");
            archive.Dispose();
            ms.Seek(0, SeekOrigin.Begin);
            return ms.ToArray();
        }

        private static void CreateFakeZipEntry(ZipArchive archive, string path, string contents)
        {
            var scriptEntry = archive.CreateEntry(path);
            using var writer = new StreamWriter(scriptEntry.Open());
            writer.WriteLine(contents);
        }
    }
}