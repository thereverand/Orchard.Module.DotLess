using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Hosting;
using DotLess.Models;
using Orchard.Environment.Features;
using dotless.Core.configuration;

namespace DotLess.Services {
    public class LessCompilerService : ILessCompilerService {
        private readonly IFeatureManager _features;
        //Global Import Paths
        public List<string> ThemeFolders { get; set; }
        
        //Per Compile Paths
        private string BasePath { get; set; }
        private List<string> LibPaths { get; set; }

        public LessCompilerService(IFeatureManager features) {
            _features = features;
            ThemeFolders = new List<string>();
            ThemeFolders.AddRange(_features.GetEnabledFeatures().Select(x => x.Extension.Location + "/" + x.Extension.Id + "/Styles").ToList());
        }

        private string ReadFile(string path) {
            return new StreamReader(path).ReadToEnd();
        }

        private string SanitizePath(string path) {
            return HostingEnvironment.MapPath(path);
        }

        private string ResolveImports(string less, string basePath, params string[] libPaths)
        {
            BasePath = basePath;

            if (LibPaths == null || (libPaths != null && libPaths.Length > 0)) {
                var hash = new HashSet<string>();
                foreach (var importPath in ThemeFolders)
                    hash.Add(importPath);
                foreach (var libPath in libPaths)
                    hash.Add(libPath);
                hash.Add(BasePath);
                LibPaths = hash.ToList();
            }
            
            var importedExpression = new Regex("@import\\s*\\\"(?<Filename>[^\"]*\\.less)\\\";");
            return importedExpression.Replace(less, ReplaceImport);
        }

        private string ReplaceImport(Match import)
        {
            return FindImport(import.Groups["Filename"].Value, BasePath);
        }

        private string FindImport(string filename, string basePath) {

            basePath = !Path.IsPathRooted(basePath) ? SanitizePath(basePath) : basePath;
            var libPaths = LibPaths;

            if (Path.IsPathRooted(filename) && new FileInfo(filename).Exists) {
                var rootedPath = SanitizePath(filename);
                return ResolveImports(ReadFile(rootedPath), Path.GetDirectoryName(rootedPath));
            }
            
            var combinedPath = Path.Combine(basePath, filename);
            if (new FileInfo(combinedPath).Exists)
                return ResolveImports(ReadFile(combinedPath), Path.GetDirectoryName(combinedPath));
            
            var files =
                libPaths.Where(
                    x => {
                        var path = SanitizePath(Path.Combine(x, filename));
                        return new FileInfo(path).Exists;
                    }
                ).ToList();

            if (files.Count > 0) {
                return
                    files.Aggregate(
                        "",
                        (result, item) => {
                            var path = SanitizePath(Path.Combine(item, filename));
                            return result + ResolveImports(ReadFile(path), item);
                        }
                    );
            }

            return string.Format("@import \"{0}\";", filename); //defer to our less engine for a definitive failure
        }

        public string CompileString(string less, string basePath, params string[] libPaths) {
            var lessFactory = new dotless.Core.EngineFactory(new DotlessConfiguration() { MinifyOutput = true });
            less = ResolveImports(less, basePath, libPaths);
            return lessFactory.GetEngine().TransformToCss(less, SanitizePath(basePath));
            
        }

        public string CompileFrom(string path, string basePath, params string[] libPaths) {
            return CompileString(SanitizePath(ReadFile(path)), basePath, libPaths);
        }

        public string CompileString(LessParameters parameters) {
            return CompileString(parameters.Source, parameters.BasePath, parameters.LibPaths);
        }

        public string CompileFrom(LessParameters parameters) {
            return CompileFrom(parameters.Path, parameters.BasePath, parameters.LibPaths);
        }
    }
}