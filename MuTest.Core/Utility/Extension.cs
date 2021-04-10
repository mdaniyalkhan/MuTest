using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MuTest.Core.Model;
using static System.Char;

namespace MuTest.Core.Utility
{
    public static class Extension
    {
        /// <summary>
        /// Get CSharp Files
        /// </summary>
        /// <param name="folderPath"></param>
        /// <returns></returns>
        public static ConcurrentBag<SyntaxFile> GetCSharpClassDeclarations(this string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                throw new InvalidOperationException("Source/Test Project Folder Not Exist");
            }

            var cus = new ConcurrentBag<SyntaxFile>();
            var files = FileUtility.GetCSharpFileInfos(folderPath).ToList();
            Parallel.ForEach(files, currentFile =>
            {
                if (currentFile != null)
                {
                    SyntaxNode codeFileContent = currentFile.GetCodeFileContent()?.RootNode();

                    if (codeFileContent is CompilationUnitSyntax syntax)
                    {
                        cus.Add(new SyntaxFile
                        {
                            CompilationUnitSyntax = syntax,
                            FileName = currentFile.FullName
                        });
                    }
                }
            });

            return cus;
        }

        /// <summary>
        /// Get CSharp Files
        /// </summary>
        /// <param name="projectPath"></param>
        /// <returns></returns>
        public static ConcurrentBag<SyntaxFile> GetCSharpClassDeclarationsFromProject(this string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
            {
                throw new InvalidOperationException("Project File Not Exist");
            }

            var cus = new ConcurrentBag<SyntaxFile>();
            var projectFile = new FileInfo(projectPath);

            if (projectPath.DoNetCoreProject())
            {
                return GetCSharpClassDeclarations(projectFile.DirectoryName);
            }

            var codeFiles = projectFile.GetProjectFiles();
            var cSharpFileInfos = codeFiles.AllKeys.Where(key => codeFiles[key].EndsWith(".cs")).Select(key => new FileInfo(codeFiles[key])).ToList();

            Task.WaitAll(cSharpFileInfos
                .Select(cSharpFileInfo => Task.Run(() =>
                {
                    if (cSharpFileInfo.Exists)
                    {
                        var codeFileContent = cSharpFileInfo.GetCodeFileContent()?.RootNode();
                        if (codeFileContent is CompilationUnitSyntax syntax)
                        {
                            cus.Add(new SyntaxFile
                            {
                                CompilationUnitSyntax = syntax,
                                FileName = cSharpFileInfo.FullName
                            });
                        }
                    }
                })).ToArray());

            return cus;
        }

        public static bool IsNumeric(this string text)
        {
            return double.TryParse(text, out _);
        }

        public static string RemoveUnnecessaryWords(this string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            text = text.Replace("this.", string.Empty);

            if (text[0] == '_')
            {
                text = text.Remove(0, 1);
            }

            return text;
        }

        public static string Encode(this string text)
        {
            return HttpUtility.HtmlEncode(text);
        }

        public static string WithLineBreaks(this string text)
        {
            return text
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        public static string FindKey(this NameValueCollection collection, string keyToFind)
        {
            string resultKey = null;
            string matchByName = null;
            string inNestedFolder = null;

            if (keyToFind.Contains('\\'))
            {
                inNestedFolder = keyToFind.Split('\\').Last();
            }

            foreach (string key in collection)
            {
                if (key.Equals(keyToFind, StringComparison.InvariantCultureIgnoreCase) ||
                    collection[key].Equals(keyToFind, StringComparison.InvariantCultureIgnoreCase))
                {
                    return key;
                }

                if (key.EndsWith($"\\{keyToFind}"))
                {
                    return key;
                }

                if (key.EndsWith(keyToFind, StringComparison.InvariantCultureIgnoreCase))
                {
                    resultKey = key;
                }

                if (Regex.IsMatch(key, keyToFind.Replace("\\", "\\\\"), RegexOptions.IgnoreCase))
                {
                    resultKey = key;
                }

                if (!string.IsNullOrWhiteSpace(inNestedFolder) && Regex.IsMatch(key, inNestedFolder, RegexOptions.IgnoreCase))
                {
                    matchByName = key;
                }
            }

            if (string.IsNullOrWhiteSpace(resultKey))
            {
                resultKey = matchByName;
            }

            return resultKey;
        }

        public static string ToCamelCase(this string str)
        {
            if (!string.IsNullOrWhiteSpace(str) && str.Length > 1)
            {
                return ToLowerInvariant(str[0]) + str.Substring(1);
            }
            return str;
        }

        public static string ToPascalCase(this string str)
        {
            if (!string.IsNullOrWhiteSpace(str) && str.Length > 1)
            {
                return ToUpperInvariant(str[0]) + str.Substring(1);
            }
            return str;
        }

        public static bool IsSimple(this string x)
        {
            return x.IsNumber() ||
                   x.IsBoolean() ||
                   x.IsText() ||
                   x == "enum";
        }

        public static string StandardBooleanAssert(this string assert)
        {
            return assert
                .Replace(".ShouldBeTrue()", ".ShouldBe(true)")
                .Replace(".ShouldBeFalse()", ".ShouldBe(false)");
        }

        private static bool IsNumber(this string type)
        {
            return type == "sbyte" ||
                   type == "sbyte?" ||
                   type == "short" ||
                   type == "short?" ||
                   type == "ushort" ||
                   type == "ushort?" ||
                   type == "int" ||
                   type == "int?" ||
                   type == "uint" ||
                   type == "uint?" ||
                   type == "long" ||
                   type == "long?" ||
                   type == "ulong" ||
                   type == "ulong?" ||
                   type == "double" ||
                   type == "double?" ||
                   type == "float" ||
                   type == "float?" ||
                   type == "decimal" ||
                   type == "decimal?";
        }

        private static bool IsText(this string type)
        {
            return type == "string" ||
                   type == "char?" ||
                   type == "char";
        }

        private static bool IsBoolean(this string type)
        {
            return type == "bool" ||
                   type == "bool?";
        }

        private static readonly Random Random = new Random();

        public static string GetRandomValue(this string type)
        {
            switch (UnBoxType(type))
            {
                case "int":
                case "uint":
                case "decimal":
                case "long":
                case "ulong":
                    return Random.Next(int.MaxValue).ToString();
                case "double":
                    return $"{Random.Next(int.MaxValue)}d";
                case "short":
                    return $"{Random.Next(short.MaxValue)}";
                case "ushort":
                    return $"{Random.Next(ushort.MaxValue)}";
                case "float":
                    return $"{Random.Next(int.MaxValue)}f";
                case "byte":
                    return $"{Random.Next(byte.MaxValue)}";
                case "sbyte":
                    return $"{Random.Next(sbyte.MaxValue)}";
                case "char":
                    return $"'{Guid.NewGuid().ToString("n").Substring(0, 1)}'";
                case "string":
                    return $"\"{Guid.NewGuid().ToString("n").Substring(0, 8)}\"";
                case "Guid":
                    return "Guid.NewGuid()";
                case "bool":
                    return "false";
                default:
                    return $"default({type})";
            }
        }

        public static string TrimToTraceLimit(this string message)
        {
            if (message == null || 
                message.Length <= 30000)
            {
                return message;
            }

            return string.Concat(message.Take(30000));
        }

        public static string ComputeHash(this string content)
        {
            if (content == null)
            {
                return string.Empty;
            }

            using (var sha256Hash = SHA256.Create())
            {
                var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(content));

                var builder = new StringBuilder();
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static string UnBoxType(this string type)
        {
            type = type.Trim('?');
            switch (type)
            {
                case "Int32":
                    return "int";
                case "UInt32":
                    return "uint";
                case "Double":
                    return "double";
                case "Int16":
                    return "short";
                case "UInt16":
                    return "ushort";
                case "Decimal":
                    return "decimal";
                case "Float":
                    return "float";
                case "Byte":
                    return "byte";
                case "SByte":
                    return "sbyte";
                case "Int64":
                    return "long";
                case "UInt64":
                    return "ulong";
                case "Boolean":
                    return "bool";
                case "Char":
                    return "char";
                case "String":
                    return "string";
                default:
                    return type;
            }
        }
    }
}