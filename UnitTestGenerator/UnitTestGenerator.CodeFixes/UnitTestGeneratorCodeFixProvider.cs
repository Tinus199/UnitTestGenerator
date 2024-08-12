using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestGenerator
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnitTestGeneratorCodeFixProvider)), Shared]
    public class UnitTestGeneratorCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get 
            { 
                return ImmutableArray.Create(UnitTestGeneratorAnalyzer.DiagnosticId); 
            }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Get the syntax root of the document
            var root = context.Document.GetSyntaxRootAsync(context.CancellationToken).Result;
            var syntaxNode = root.FindNode(diagnosticSpan);
            var classDeclaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().First();
            var compilationUnitSyntax = (CompilationUnitSyntax)root;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Generate Unit Test Template",
                    createChangedSolution: c => GenerateUnitTestTemplateAsync(context.Document, classDeclaration, compilationUnitSyntax, c),
                    equivalenceKey: nameof(UnitTestGeneratorCodeFixProvider)),
                diagnostic);

            return Task.CompletedTask;
        }

        private async Task<Solution> GenerateUnitTestTemplateAsync(
            Document document, 
            ClassDeclarationSyntax classDeclaration,
            CompilationUnitSyntax compilationUnitSyntax,
            CancellationToken cancellationToken)
        {
            var className = classDeclaration.Identifier.Text;

            var usings = compilationUnitSyntax.Usings;

            var namespaceName = compilationUnitSyntax.Members
                .OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault().Name.ToString();

            var publicMethods = classDeclaration.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(method => method.Modifiers.Any(SyntaxKind.PublicKeyword))
                .ToList();

            var constructor = classDeclaration.Members
                .OfType<ConstructorDeclarationSyntax>()
                .Where(c => c.Modifiers.Any(SyntaxKind.PublicKeyword))
                .FirstOrDefault();

            var constructorParams = constructor?.ParameterList.Parameters
                .ToList();

            var unitTestTemplate = GenerateUnitTestTemplate(namespaceName, className, usings, publicMethods, constructorParams);

            var folderPath = Path.GetDirectoryName(document.FilePath);
            if (string.IsNullOrEmpty(folderPath))
            {
                return document.Project.Solution;
            }

            var testFilePath = Path.Combine(folderPath, $"{className}Test.cs");
            File.WriteAllText(testFilePath, unitTestTemplate);

            return document.Project.Solution;
        }

        private string GenerateUnitTestTemplate(
            string namespaceName, 
            string className, 
            SyntaxList<UsingDirectiveSyntax> usings,
            List<MethodDeclarationSyntax> publicMethods,
            List<ParameterSyntax> constructorParams)
        {
            var testTemplate = string.Empty;

            var usingTemplate = string.Empty;
            if (usings != null)
            {
                for (int i = 0; i < usings.Count; i++)
                {
                    UsingDirectiveSyntax usingDirective = usings[i];
                    usingTemplate += $"{usingDirective}" + Environment.NewLine;
                }
            }

            var constructorParamTemplateGlobal = string.Empty;
            var constructorParamTemplateInitializer = string.Empty;
            var constructorParamTemplateLocal = new List<string>();

            if (constructorParamTemplateGlobal != null)
            {
                for (int i = 0; i < constructorParams.Count; i++)
                {
                    string space = string.Empty;
                    string spaceExtended = string.Empty;

                    if (i >= 1)
                    {
                        space = "        ";
                        spaceExtended = "            ";
                    }

                    var param = constructorParams[i];
                    constructorParamTemplateGlobal +=
$@"{space}private Mock<{param.Type}> m_{param.Identifier.Text};" + Environment.NewLine;
                    
                    constructorParamTemplateInitializer +=
$@"{spaceExtended}m_{param.Identifier.Text} = new Mock<{param.Type}>();" + Environment.NewLine;
                    constructorParamTemplateLocal.Add($"m_{param.Identifier.Text}.Object");
                }
            }

            var constructorString = string.Empty;

            if(constructorParamTemplateLocal.Count > 1)
            {
                constructorString += $"\n                {constructorParamTemplateLocal.First()}";
            }
            else if (constructorParamTemplateLocal.Count == 1)
            {
                constructorString += $"{constructorParamTemplateLocal.First()}";
            }

            for (int i = 0; i < constructorParamTemplateLocal.Count - 2; i++)
            {
                string constructorParam = constructorParamTemplateLocal[i+1];
                constructorString += $",\n                {constructorParam}";
            }

            if(constructorParamTemplateLocal.Count > 1)
            {
                constructorString += $",\n                {constructorParamTemplateLocal.Last()}";
            }

            var publicMethodsTemplate = string.Empty;

            if (publicMethods != null)
            {
                foreach (var method in publicMethods)
                {
                    publicMethodsTemplate += 
$@"
        [Test]
        public void {method.Identifier.Text}_Should_DoSomething()
        {{
            // Arrange

            // Act
            m_UnitUnderTest.{method.Identifier.Text}();                        

            // Assert
        }}
";
                }
            }


            testTemplate +=
$@"{usingTemplate}using Moq;
using NUnit.Framework;

namespace {namespaceName}
{{
    [TestFixture]
    internal class {className}Test
    {{
        // 10
        private {className} m_UnitUnderTest;

        {constructorParamTemplateGlobal}
        [SetUp]
        public void Setup()
        {{
            {constructorParamTemplateInitializer}
            m_UnitUnderTest = new {className}({constructorString});
        }}
        {publicMethodsTemplate}
    }}
}}";

            return testTemplate;
        }

        private string ShowFolderBrowserDialog()
        {
            using (var folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                var result = folderBrowserDialog.ShowDialog();

                return result == System.Windows.Forms.DialogResult.OK ? folderBrowserDialog.SelectedPath : null;
            }
        }
    }
}
