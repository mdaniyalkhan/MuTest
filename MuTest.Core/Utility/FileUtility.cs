using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace MuTest.Core.Utility
{
    public static class FileUtility
    {
        /// <summary>
        /// Gets c# files Info
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static FileInfo[] GetCSharpFileInfos(string path)
        {
            if (Directory.Exists(path))
            {
                return new DirectoryInfo(path).EnumerateFiles("*.cs", SearchOption.AllDirectories)
                    .Where(x => x.Name.EndsWith(".cs", StringComparison.CurrentCulture) &&
                                !x.Name.StartsWith("f.") &&
                                !x.Name.StartsWith("TemporaryGeneratedFile_") &&
                                !x.Name.StartsWith("AssemblyInfo")).ToArray();
            }

            return null;
        }

        public static FileInfo FindFile(this string path, string fileName)
        {
            if (Directory.Exists(path))
            {
                return new DirectoryInfo(path).EnumerateFiles(fileName, SearchOption.AllDirectories)
                    .FirstOrDefault(x => x.Name.EndsWith(fileName, StringComparison.InvariantCulture) &&
                                         !x.Name.StartsWith("f.") &&
                                         !x.Name.StartsWith("TemporaryGeneratedFile_") &&
                                         !x.Name.StartsWith("AssemblyInfo"));
            }

            return null;
        }

        public static FileInfo FindProjectFile(this string path)
        {
            if (Directory.Exists(path))
            {
                return new DirectoryInfo(path).EnumerateFiles("*.csproj", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(x => x.Name.EndsWith(".csproj", StringComparison.InvariantCulture) &&
                                         !x.Name.StartsWith("f.") &&
                                         !x.Name.StartsWith("TemporaryGeneratedFile_") &&
                                         !x.Name.StartsWith("AssemblyInfo"));
            }

            return null;
        }

        public static FileInfo FindProjectFile(this FileInfo file)
        {
            var projectFile = file.DirectoryName.FindProjectFile();
            if (projectFile == null)
            {
                var parentDirectory = file.Directory?.Parent;
                while (parentDirectory != null)
                {
                    projectFile = parentDirectory.FullName.FindProjectFile();
                    if (projectFile != null)
                    {
                        break;
                    }

                    parentDirectory = parentDirectory.Parent;
                }
            }

            return projectFile;
        }

        public static FileInfo FindLibraryPath(this FileInfo project, string configuration = "Debug")
        {
            if (project != null && project.Exists)
            {
                var projectXml = project.GetProjectDocument();
                var assembly = projectXml.SelectSingleNode("/Project/PropertyGroup/AssemblyName")?.InnerText ?? Path.GetFileNameWithoutExtension(project.Name);
                var outputPath = projectXml.SelectSingleNode("/Project/PropertyGroup/OutputPath")?.InnerText ?? Path.Combine("bin", "Debug");
                var targetPlatform = projectXml.SelectSingleNode("/Project/PropertyGroup/TargetFramework");
                if (!string.IsNullOrWhiteSpace(project.DirectoryName))
                {
                    outputPath = outputPath.Replace("$(Configuration)", configuration);
                    var sourceDllPath = Path.GetFullPath(Path.Combine(project.DirectoryName, outputPath));
                    var library = sourceDllPath.FindFile($"{assembly}.dll");
                    library = library ?? sourceDllPath.FindFile($"{assembly}.exe");

                    if (library == null && targetPlatform != null)
                    {
                        library = Path.Combine(sourceDllPath, targetPlatform.InnerText).FindFile($"{assembly}.dll");
                        library = library ?? Path.Combine(sourceDllPath, targetPlatform.InnerText).FindFile($"{assembly}.exe");
                    }

                    return library;
                }
            }

            return null;
        }

        public static bool DoNetCoreProject(this string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                return false;
            }

            var project = new FileInfo(projectPath);
            if (project.Exists)
            {
                var projectXml = project.GetProjectDocument();
                var targetPlatform = projectXml.SelectSingleNode("/Project/PropertyGroup/TargetFramework");

                if (targetPlatform != null)
                {
                    return targetPlatform.InnerText.StartsWith("netcoreapp", StringComparison.InvariantCultureIgnoreCase);
                }
            }

            return false;
        }

        public static IList<string> GetProjectThirdPartyLibraries(this string projectPath)
        {
            var libs = new List<string>();
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                return libs;
            }

            var project = new FileInfo(projectPath);
            if (project.Exists)
            {
                var projectXml = project.GetProjectDocument();
                var references = projectXml.SelectNodes("/Project/ItemGroup/Reference/HintPath");

                if (references != null)
                {
                    foreach (XmlNode reference in references)
                    {
                        libs.Add(reference.InnerText);
                    }
                }
            }

            return libs;
        }

        public static string UpdateTestProject(this string projectPath, string testClassName)
        {
            var newPath = projectPath;
            var project = new FileInfo(projectPath);
            if (project.Exists)
            {
                var projectXml = project.GetProjectDocument();
                var references = projectXml.SelectNodes("/Project/ItemGroup/Compile");

                if (references != null)
                {
                    newPath = Path.Combine(project.DirectoryName, $"{Path.GetFileNameWithoutExtension(project.FullName)}.mutest.csproj");
                    for (var index = 0; index < references.Count; index++)
                    {
                        XmlNode reference = references[index];
                        var include = reference.Attributes?["Include"];
                        if (include != null)
                        {
                            var innerText = include.InnerText;
                            if (!Regex.IsMatch(innerText, $@"{testClassName}.cs|{testClassName}\..*\.cs", RegexOptions.IgnoreCase) &&
                                Regex.IsMatch(innerText, @".*Test.cs|.*Tests.cs|.*Test\..*\.cs|.*Tests\..*\.cs"))
                            {
                                reference.ParentNode?.RemoveChild(reference);
                            }
                        }
                    }

                    var newPathFile = new FileInfo(newPath);
                    if (newPathFile.Exists)
                    {
                        newPathFile.Delete();
                    }

                    projectXml.Save(newPathFile.FullName);

                    return newPathFile.FullName;
                }
            }


            return newPath;
        }

        public static NameValueCollection GetProjectFiles(this FileInfo project)
        {
            var dictionary = new NameValueCollection();
            if (project != null &&
                project.Exists)
            {
                var projectXml = project.GetProjectDocument();
                var classes = projectXml.SelectNodes("/Project/ItemGroup/Compile[@Include]/@Include");
                if (classes != null &&
                    project.DirectoryName != null)
                {
                    foreach (XmlNode path in classes)
                    {
                        var relativePath = path.InnerText;
                        var classPath = Path.GetFullPath(Path.Combine(project.DirectoryName, relativePath));
                        if (dictionary[relativePath] == null)
                        {
                            dictionary.Add(relativePath, classPath);
                        }
                    }
                }
            }

            return dictionary;
        }

        public static XmlDocument GetProjectDocument(this FileSystemInfo project)
        {
            var projectXmlFile = File.ReadAllText(project.FullName);
            projectXmlFile = Regex.Replace(projectXmlFile, "xmlns=.*\"", string.Empty);
            var projectXml = new XmlDocument();
            projectXml.LoadXml(projectXmlFile);

            return projectXml;
        }

        /// <summary>
        ///  Get Code File Content
        /// </summary>
        /// <param name="info">File Info</param>
        /// <returns></returns>
        public static string GetCodeFileContent(this FileInfo info)
        {
            return new StringBuilder().Append(File.ReadAllText(info.FullName)).ToString();
        }

        public static string GetCodeFileContent(this string file)
        {
            return GetCodeFileContent(new FileInfo(file));
        }

        public static void DirectoryCopy(this string sourceDirName, string destDirName, string extensionsToCopy = "")
        {
            var dir = new DirectoryInfo(sourceDirName);
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDirName}");
            }

            var dirs = dir.GetDirectories();
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            var files = string.IsNullOrWhiteSpace(extensionsToCopy)
                ? dir.EnumerateFiles().ToList()
                : dir.EnumerateFiles().Where(x => extensionsToCopy.Contains(x.Extension)).ToList();
            foreach (var file in files)
            {
                var combine = Path.Combine(destDirName, file.Name);
                file.CopyTo(combine, true);
            }

            foreach (var subDirectory in dirs)
            {
                var combine = Path.Combine(destDirName, subDirectory.Name);
                DirectoryCopy(subDirectory.FullName, combine);
            }
        }

        public static string RelativePath(this string absolutePath, string root)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || string.IsNullOrWhiteSpace(root))
            {
                throw new ArgumentNullException($"{nameof(absolutePath)} and ${nameof(root)} is required!");
            }

            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                root += Path.DirectorySeparatorChar;
            }

            var fromUri = new Uri(root);
            var toUri = new Uri(absolutePath);

            if (fromUri.Scheme != toUri.Scheme)
            {
                return root;
            }

            var relativeUri = fromUri.MakeRelativeUri(toUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }

        public static string GithubPath(this string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(path));
            }

            var file = new FileInfo(path);
            var dir = file.Directory;
            var parent = dir?.Parent;
            while (parent != null) 
            {
                if (parent.GetDirectories().Any(x => x.Name == ".git"))
                {
                    break;
                }

                parent = parent.Parent;
            }

            if (parent == null)
            {
                return path;
            }

            return path.RelativePath(parent.FullName);
        }

        public static void WriteLines(this string path, IList<string> lines)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            using (var writer = new StreamWriter(path))
            {
                foreach (var fileLine in lines)
                {
                    writer.WriteLine(fileLine);
                }
            }
        }
    }
}