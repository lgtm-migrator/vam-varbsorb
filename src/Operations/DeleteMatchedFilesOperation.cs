﻿using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Varbsorb.Models;

namespace Varbsorb.Operations
{
    public class DeleteMatchedFilesOperation : OperationBase, IDeleteMatchedFilesOperation
    {
        protected override string Name => "Delete matched files";

        private readonly IFileSystem _fs;

        public DeleteMatchedFilesOperation(IConsoleOutput output, IFileSystem fs)
            : base(output)
        {
            _fs = fs;
        }

        public Task ExecuteAsync(IList<FreeFile> files, IList<FreeFilePackageMatch> matches, IFilter filter, bool verbose, bool noop)
        {
            var filesToDelete = new HashSet<FreeFile>();

            foreach (var match in matches)
            {
                foreach (var file in match.FreeFiles.Where(f => !filter.IsFiltered(f.LocalPath)).SelectMany(f => f.SelfAndChildren()))
                {
                    filesToDelete.Add(file);
                }
            }

            if (filesToDelete.Count == 0)
            {
                Output.WriteLine("Good news, there's nothing to delete!");
                return Task.CompletedTask;
            }

            var mbSaved = filesToDelete.Sum(f => (long)(f.Size ?? 0)) / 1024f / 1024f;
            Output.WriteLine($"{filesToDelete.Count} files will be deleted. Estimated {mbSaved:0.00}MB saved.");
            using (var reporter = new ProgressReporter<ProgressInfo>(StartProgress, ReportProgress, CompleteProgress))
            {
                var processed = 0;
                foreach (var file in filesToDelete)
                {
                    if (verbose) Output.WriteLine($"{(noop ? "[NOOP]" : "DELETE")}: {file.LocalPath}");
                    if (!noop) _fs.File.Delete(file.Path);
                    files.Remove(file);
                    reporter.Report(new ProgressInfo(++processed, filesToDelete.Count, file.LocalPath));
                }

                if (!noop)
                {
                    foreach (var folder in filesToDelete.Select(f => _fs.Path.GetDirectoryName(f.Path)).Distinct().OrderByDescending(f => f.Length))
                    {
                        if (_fs.Directory.Exists(folder) && _fs.Directory.GetFileSystemEntries(folder).Length == 0)
                        {
                            if (verbose) Output.WriteLine($"DELETE (empty folder): {folder}");
                            _fs.Directory.Delete(folder);
                        }
                    }
                }
            }

            Output.WriteLine($"Deleted {filesToDelete.Count} matched files.");

            return Task.CompletedTask;
        }
    }

    public interface IDeleteMatchedFilesOperation : IOperation
    {
        Task ExecuteAsync(IList<FreeFile> files, IList<FreeFilePackageMatch> matches, IFilter filter, bool verbose, bool noop);
    }
}
