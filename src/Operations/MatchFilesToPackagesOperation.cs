﻿using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Varbsorb.Operations
{
    public interface IMatchFilesToPackagesOperation : IOperation
    {
        Task<IList<FreeFilePackageMatch>> ExecuteAsync(IList<VarPackage> packages, IList<FreeFile> freeFiles);
    }

    public class MatchFilesToPackagesOperation : OperationBase, IMatchFilesToPackagesOperation
    {
        private readonly IFileSystem _fs;
        private readonly IHashingAlgo _hashingAlgo;

        public MatchFilesToPackagesOperation(IConsoleOutput output, IFileSystem fs, IHashingAlgo hashingAlgo)
            : base(output)
        {
            _fs = fs;
            _hashingAlgo = hashingAlgo;
        }

        public async Task<IList<FreeFilePackageMatch>> ExecuteAsync(IList<VarPackage> packages, IList<FreeFile> freeFiles)
        {
            var freeFilesSet = freeFiles.GroupBy(ff => _fs.Path.GetFileName(ff.Path).ToLowerInvariant()).ToDictionary(f => f.Key, f => f.ToList());
            var matches = new List<FreeFilePackageMatch>();
            using (var reporter = new ProgressReporter<MatchFilesProgress>(StartProgress, ReportProgress, CompleteProgress))
            {
                var packagesComplete = 0;
                foreach (var package in packages)
                {
                    foreach (var packageFile in package.Files)
                    {
                        if (freeFilesSet.TryGetValue(_fs.Path.GetFileName(packageFile.LocalPath).ToLowerInvariant(), out var matchingFreeFiles))
                        {
                            foreach (var matchingFreeFile in matchingFreeFiles)
                            {
                                if (matchingFreeFile.Hash != null) continue;
                                matchingFreeFile.Hash = _hashingAlgo.GetHash(await _fs.File.ReadAllBytesAsync(matchingFreeFile.Path));
                            }
                            var matchedFreeFiles = matchingFreeFiles.Where(ff => ff.Hash == packageFile.Hash).ToList();

                            if (matchedFreeFiles.Count == 0) continue;

                            // TODO: Select the _best_ match (hash and complete set)
                            //  1. Find all hash matches
                            //  2. Find the most recent version of all matching packages
                            //  3. Find the package with the least files
                            //  4. Take the first
                            var match = new FreeFilePackageMatch
                            {
                                Package = package,
                                PackageFile = packageFile,
                                FreeFiles = matchedFreeFiles
                            };
                            matches.Add(match);
                            // TODO: Progress
                        }
                    }

                    reporter.Report(new MatchFilesProgress { PackagesComplete = ++packagesComplete, PackagesTotal = packages.Count });
                }
            }

            _output.WriteLine($"Found {matches.Count} matching files.");

            return matches;
        }

        public class MatchFilesProgress
        {
            public int PackagesComplete { get; set; }
            public int PackagesTotal { get; set; }
        }

        private void ReportProgress(MatchFilesProgress progress)
        {
            _output.WriteAndReset($"Matching packages to files... {progress.PackagesComplete} / {progress.PackagesTotal} ({progress.PackagesComplete / (float)progress.PackagesTotal * 100:0}%)");
        }
    }
}