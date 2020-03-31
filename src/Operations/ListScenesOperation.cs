using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Varbsorb.Models;

namespace Varbsorb.Operations
{
    public class ListScenesOperation : OperationBase, IListScenesOperation
    {
        private static readonly Regex _findFilesFastRegex = new Regex(
            ": ?\"(?<path>[^\"]+\\.[a-zA-Z]{2,6})\"",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(10));
        private readonly IFileSystem _fs;

        public ListScenesOperation(IConsoleOutput output, IFileSystem fs)
            : base(output)
        {
            _fs = fs;
        }

        public async Task<IList<SceneFile>> ExecuteAsync(string vam, IList<FreeFile> files)
        {
            var scenes = new List<SceneFile>();
            var filesIndex = files.Where(f => f.Extension != ".json").ToDictionary(f => f.Path, f => f);
            using (var reporter = new ProgressReporter<ListScenesProgress>(StartProgress, ReportProgress, CompleteProgress))
            {
                var scenesScanned = 0;
                var potentialScenes = files.Where(f => f.Extension == ".json").ToList();
                foreach (var potentialScene in potentialScenes)
                {
                    var potentialSceneJson = await _fs.File.ReadAllTextAsync(potentialScene.Path);
                    var potentialSceneReferences = _findFilesFastRegex.Matches(potentialSceneJson).Where(m => m.Success).Select(m => m.Groups["path"]);
                    var sceneFolder = _fs.Path.GetDirectoryName(potentialScene.Path);
                    var references = new List<SceneReference>();
                    foreach (var reference in potentialSceneReferences)
                    {
                        if (!reference.Success) continue;
                        var refPath = reference.Value;
                        if (refPath.Contains(":")) continue;
                        if (filesIndex.TryGetValue(_fs.Path.GetFullPath(_fs.Path.Combine(sceneFolder, refPath)), out var f1))
                        {
                            references.Add(new SceneReference(f1, reference.Index, reference.Length));
                        }
                        else if (filesIndex.TryGetValue(_fs.Path.GetFullPath(_fs.Path.Combine(vam, refPath)), out var f2))
                        {
                            references.Add(new SceneReference(f2, reference.Index, reference.Length));
                        }
                    }
                    if (references.Count > 0)
                        scenes.Add(new SceneFile(potentialScene, references));
                    reporter.Report(new ListScenesProgress(++scenesScanned, potentialScenes.Count, potentialScene.FilenameLower));
                }
            }

            Output.WriteLine($"Found {files.Count} files in the Saves and Custom folders.");

            return scenes;
        }

        public class ListScenesProgress
        {
            public int ScenesProcessed { get; }
            public int TotalScenes { get; }
            public string Current { get; }

            public ListScenesProgress(int scenesProcessed, int totalScenes, string current)
            {
                ScenesProcessed = scenesProcessed;
                TotalScenes = totalScenes;
                Current = current;
            }
        }

        private void ReportProgress(ListScenesProgress progress)
        {
            Output.WriteAndReset($"Parsing scenes... {progress.ScenesProcessed} / {progress.TotalScenes} ({progress.ScenesProcessed / (float)progress.TotalScenes * 100:0}%): {progress.Current}");
        }
    }

    public interface IListScenesOperation : IOperation
    {
        Task<IList<SceneFile>> ExecuteAsync(string vam, IList<FreeFile> files);
    }
}
