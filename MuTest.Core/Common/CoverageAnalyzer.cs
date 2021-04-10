using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Coverage.Analysis;
using MuTest.Core.Model;
using MuTest.Core.Model.ClassDeclarations;
using MuTest.Core.Mutants;
using MuTest.Core.Utility;
using static MuTest.Core.Common.Constants;

namespace MuTest.Core.Common
{
    public class CoverageAnalyzer : ICoverageAnalyzer
    {
        private const string InitializecomponentMethod = "InitializeComponent";
        public string Output { get; set; }

        public void FindCoverage(SourceClassDetail source, CoverageDS codeCoverage)
        {
            if (codeCoverage != null)
            {
                source.ExternalCoveredClasses.Clear();
                source.ExternalCoveredClasses.AddRange(FindExternalCoveredClasses(source, codeCoverage));
                var parentClassName = string.Join(".", source.Claz.Syntax.Ancestors<ClassDeclarationSyntax>().Select(x => x.ClassName()));
                var className = $"{parentClassName}.{source.Claz.Syntax.ClassName()}".TrimStart('.');
                var coverages = codeCoverage
                    .Class
                    .Where(x => x.NamespaceTableRow.NamespaceName == source.Claz.Syntax.NameSpace() &&
                                (x.ClassName == className ||
                                 x.ClassName.StartsWith($"{className}.{GenericMethodStart}") ||
                                 x.ClassName.StartsWith($"{className}{GenericMethodStart}"))).ToList();

                if (coverages.Any())
                {
                    source.Coverage = new Coverage
                    {
                        LinesCovered = (uint)coverages.Sum(x => x.LinesCovered),
                        LinesNotCovered = (uint)coverages.Sum(x => x.LinesNotCovered),
                        BlocksCovered = (uint)coverages.Sum(x => x.BlocksCovered),
                        BlocksNotCovered = (uint)coverages.Sum(x => x.BlocksNotCovered)
                    };

                    var methodsWithCoverage = new List<MethodDetail>();
                    PrintClassCoverage(source, className);
                    foreach (var coverage in coverages)
                    {
                        var coverageClassName = coverage.ClassName;
                        var methods = codeCoverage
                            .Method.Where(x => x.ClassKeyName == coverage.ClassKeyName)
                            .ToList();

                        foreach (CoverageDSPriv.MethodRow mCoverage in methods)
                        {
                            var methodFullName = mCoverage.MethodFullName;
                            if (methodFullName.StartsWith(GenericMethodStart) && methodFullName.Contains(GenericMethodEnd))
                            {
                                var startIndex = methodFullName.IndexOf(GenericMethodStart, StringComparison.InvariantCulture) + 1;
                                var length = methodFullName.IndexOf(GenericMethodEnd, StringComparison.InvariantCulture) - 1;
                                methodFullName = $"{methodFullName.Substring(startIndex, length)}(";
                            }

                            var numberOfOverloadedMethods = source.MethodDetails.Where(x =>
                                methodFullName.StartsWith($"{x.Method.MethodName()}(") ||
                                methodFullName.StartsWith($"set_{x.Method.MethodName()}(") ||
                                methodFullName.StartsWith($"get_{x.Method.MethodName()}(")).ToList();
                            MethodDetail methodDetail = null;
                            if (numberOfOverloadedMethods.Count == 1)
                            {
                                methodDetail = numberOfOverloadedMethods.First();
                            }

                            if (methodDetail == null)
                            {
                                methodDetail = source.MethodDetails
                                    .FirstOrDefault(x =>
                                        x.Method.MethodWithParameterTypes() == methodFullName.Replace("System.", string.Empty));
                            }

                            string methodName;
                            if (methodDetail == null && coverageClassName.Contains(GenericMethodStart))
                            {
                                var startIndex = coverageClassName.IndexOf(GenericMethodStart, StringComparison.InvariantCulture);
                                var endIndex = coverageClassName.IndexOf(GenericMethodEnd, StringComparison.InvariantCulture);
                                methodName = coverageClassName.Substring(startIndex + 1, endIndex - startIndex - 1);
                                methodDetail = source.MethodDetails.FirstOrDefault(x => x.Method.MethodName().Equals(methodName));
                            }

                            if (methodDetail != null)
                            {
                                if (methodDetail.Coverage == null)
                                {
                                    methodDetail.Coverage = new Coverage();
                                }

                                methodDetail.Coverage = new Coverage
                                {
                                    LinesCovered = methodDetail.Coverage.LinesCovered + mCoverage.LinesCovered,
                                    LinesNotCovered = methodDetail.Coverage.LinesNotCovered + mCoverage.LinesNotCovered,
                                    BlocksCovered = methodDetail.Coverage.BlocksCovered + mCoverage.BlocksCovered,
                                    BlocksNotCovered = methodDetail.Coverage.BlocksNotCovered + mCoverage.BlocksNotCovered
                                };

                                methodDetail.Lines.AddRange(mCoverage.GetLinesRows());
                                methodsWithCoverage.Add(methodDetail);
                            }
                        }
                    }

                    methodsWithCoverage = methodsWithCoverage.GroupBy(x => x.Method.MethodWithParameterTypes()).Select(x =>
                    {
                        var methodDetail = new MethodDetail
                        {
                            Coverage = x.Last().Coverage,
                            Method = x.First().Method,
                            MethodName = x.First().MethodName
                        };

                        methodDetail.Lines.AddRange(x.First().Lines);
                        return methodDetail;
                    }).ToList();
                    foreach (var methodDetail in methodsWithCoverage)
                    {
                        var methodName = methodDetail.Method.MethodWithParameterTypes();
                        Output += $"{methodName} {methodDetail.Coverage.ToString().Print(color: Colors.BlueViolet)}".PrintWithPreTagWithMarginImportant();
                    }
                }

                PrintExternalCoveredClasses(source);
            }
        }

        private static IList<ClassCoverage> FindExternalCoveredClasses(SourceClassDetail source, CoverageDSPriv codeCoverage)
        {
            var data = new List<ClassCoverage>();
            var thirdPartyLibs = source.TestClaz.ClassProject
                .GetProjectThirdPartyLibraries()
                .Select(x => x
                    .Split('\\')
                    .Last()).ToList();
            thirdPartyLibs.Add("nunit");
            thirdPartyLibs.Add("microsoft.");
            if (codeCoverage != null)
            {
                var parentClassNameList = $"{source.Claz.Syntax.NameSpace()}.{string.Join(".", source.Claz.Syntax.Ancestors<ClassDeclarationSyntax>().Select(x => x.ClassNameWithoutGeneric()))}".TrimEnd('.');
                var nestedClassNameList = $"{parentClassNameList}.{source.Claz.Syntax.ClassNameWithoutGeneric()}.{string.Join(".", source.Claz.Syntax.DescendantNodes<ClassDeclarationSyntax>().Select(x => x.ClassNameWithoutGeneric()))}".TrimEnd('.');
                if (parentClassNameList == source.Claz.Syntax.NameSpace())
                {
                    parentClassNameList = $"{parentClassNameList}.{source.Claz.Syntax.ClassNameWithoutGeneric()}";
                }

                foreach (CoverageDSPriv.ClassRow claz in codeCoverage.Class)
                {
                    if (claz.LinesCovered > 0 && thirdPartyLibs.All(x => !claz.NamespaceTableRow.ModuleRow.ModuleName.StartsWith(x, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        var className = claz.ClassName;
                        var genericIndexLocation = claz.ClassName.IndexOf(GenericMethodStart, StringComparison.Ordinal);
                        if (genericIndexLocation != -1)
                        {
                            className = className.Substring(0, genericIndexLocation).TrimEnd('.');
                        }

                        var fullName = $"{claz.NamespaceTableRow.NamespaceName}.{className}";
                        if (data.All(x => x.ClassName != fullName) &&
                            !fullName.Contains(parentClassNameList) &&
                            !fullName.Contains(nestedClassNameList))
                        {
                            var coverages = codeCoverage
                                .Class
                                .Where(x => x.ClassName == className ||
                                            x.ClassName.StartsWith($"{className}.{GenericMethodStart}") ||
                                            x.ClassName.StartsWith($"{className}{GenericMethodStart}")).ToList();

                            coverages = coverages.Where(x => x.NamespaceTableRow.NamespaceKeyName == claz.NamespaceKeyName).ToList();

                            if (coverages.Any())
                            {
                                var methods = codeCoverage.Method.Where(x => x.ClassKeyName == claz.ClassKeyName).ToList();
                                var method = methods.FirstOrDefault();
                                var numberOfMutants = 0;
                                var excluded = false;
                                var mutantsLines = new List<int>();

                                uint autogeneratedLineCovered = 0;
                                uint autogeneratedLineNonCovered = 0;
                                uint autogeneratedBlockCovered = 0;
                                uint autogeneratedBlockNonCovered = 0;

                                var file = string.Empty;
                                if (method != null)
                                {
                                    file = codeCoverage.SourceFileNames.FirstOrDefault(x => x.SourceFileID == method.GetLinesRows().FirstOrDefault()?.SourceFileID)?.SourceFileName;
                                    if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
                                    {
                                        var root = file.GetCodeFileContent().RootNode().ClassNode(className.Split('.').Last());
                                        var classDeclaration = new ClassDeclaration(root);
                                        var classDetail = new SourceClassDetail
                                        {
                                            Claz = classDeclaration,
                                            TestClaz = new TestClassDetail()
                                        };

                                        if (root != null)
                                        {
                                            new MethodsInitializer().FindMethods(classDetail).Wait();
                                            var mutants = classDetail.MethodDetails
                                                .Where(x => !x.IsProperty && !x.IsConstructor && !x.IsOverrideMethod)
                                                .SelectMany(x => MutantOrchestrator.GetDefaultMutants(x.Method, classDetail.Claz));
                                            var coveredLines = claz.GetMethodRows().SelectMany(x => x.GetLinesRows()).Where(x => x.Coverage == 0).ToList();
                                            mutants = mutants.Where(x => coveredLines.Any(y => y.LnStart == x.Mutation.Location)).ToList();
                                            mutantsLines = mutants.Select(x => x.Mutation.Location ?? 0).ToList();
                                            numberOfMutants = mutants.Count();

                                            excluded = root.ExcludeFromExternalCoverage();

                                            var autogeneratedMethods = root.GetGeneratedCodeMethods();
                                            foreach (var methodSyntax in autogeneratedMethods)
                                            {
                                                var autoGeneratedCoverage = methods.FirstOrDefault(x => x.MethodFullName.Equals($"{methodSyntax.MethodName()}()") ||
                                                                                                        x.MethodName.Equals($"{methodSyntax.MethodName()}()"));
                                                if (autoGeneratedCoverage != null)
                                                {
                                                    autogeneratedLineCovered += autoGeneratedCoverage.LinesCovered;
                                                    autogeneratedLineNonCovered += autoGeneratedCoverage.LinesNotCovered;
                                                    autogeneratedBlockCovered += autoGeneratedCoverage.BlocksCovered;
                                                    autogeneratedBlockNonCovered += autoGeneratedCoverage.BlocksNotCovered;
                                                }
                                            }

                                            if (methods.Any(x => x.MethodFullName.Equals($"{InitializecomponentMethod}()")) &&
                                                !autogeneratedMethods.Any() &&
                                                !classDetail.MethodDetails.Any(x => x.Method.MethodName().Equals(InitializecomponentMethod)))
                                            {
                                                var autoGeneratedCoverage = methods.First(x => x.MethodFullName.Equals($"{InitializecomponentMethod}()"));

                                                autogeneratedLineCovered += autoGeneratedCoverage.LinesCovered;
                                                autogeneratedLineNonCovered += autoGeneratedCoverage.LinesNotCovered;
                                                autogeneratedBlockCovered += autoGeneratedCoverage.BlocksCovered;
                                                autogeneratedBlockNonCovered += autoGeneratedCoverage.BlocksNotCovered;
                                            }
                                        }
                                        else
                                        {
                                            excluded = true;
                                        }
                                    }
                                }

                                var classCoverage = new ClassCoverage
                                {
                                    ClassName = fullName,
                                    ClassPath = file,
                                    Coverage = new Coverage { 
                                        LinesCovered = (uint)coverages.Sum(x => x.LinesCovered) - autogeneratedLineCovered,
                                        LinesNotCovered = (uint)coverages.Sum(x => x.LinesNotCovered) - autogeneratedLineNonCovered,
                                        BlocksCovered = (uint)coverages.Sum(x => x.BlocksCovered) - autogeneratedBlockCovered,
                                        BlocksNotCovered = (uint)coverages.Sum(x => x.BlocksNotCovered) - autogeneratedBlockNonCovered
                                    },
                                    NumberOfMutants = numberOfMutants,
                                    Excluded = excluded
                                };

                                classCoverage.MutantsLines.AddRange(mutantsLines);
                                data.Add(classCoverage);
                            }
                        }
                    }
                }
            }

            return data;
        }

        private void PrintClassCoverage(SourceClassDetail source, string coverageClassName)
        {
            Output += " ".PrintWithPreTag();
            Output += "Class Coverage: ".PrintWithPreTagImportant(3, Colors.Gold);
            Output += $"Class Name: {coverageClassName} {source.Coverage.ToString().Print(color: Colors.BlueViolet)}".PrintWithPreTagImportant();
            Output += "Methods Coverage: ".PrintWithPreTagImportant();
        }

        private void PrintExternalCoveredClasses(SourceClassDetail source)
        {
            if (source.ExternalCoveredClasses.Any())
            {
                Output += " ".PrintWithPreTag();
                Output += "External Coverage: ".PrintWithPreTagImportant(3, Colors.Gold);
                foreach (var clz in source.ExternalCoveredClasses)
                {
                    Output += clz.ToString().PrintWithPreTagWithMarginImportant(color: Colors.BlueViolet);
                }
            }
        }
    }
}