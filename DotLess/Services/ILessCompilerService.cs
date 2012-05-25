using System;
using System.Collections.Generic;
using System.Text;
using Orchard;

namespace DotLess.Services
{
    public interface ILessCompilerService : IDependency {
        string CompileString(string less, string basePath, params string[] libPath);
        string CompileFrom(string path, string basePath, params string[] libPath);

        List<string> ThemeFolders { get; }
    }
}
