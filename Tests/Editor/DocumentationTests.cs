using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace DynamicDialog.Tests
{
    /// <summary>
    /// Keeps the manual (Documentation~) and README honest: every ```dlg block parses without errors or warnings,
    /// every ```csv block reads as a spreadsheet, and every ```csharp block appears (ignoring whitespace) in one of
    /// the DocumentationSnippets.cs files under Tests/, which Unity and the .NET harness compile.
    /// </summary>
    public class DocumentationTests
    {
        static readonly Regex Fence = new Regex(@"^[ \t]*```(\w*)[ \t]*\r?\n(.*?)\r?\n[ \t]*```[ \t]*\r?$",
            RegexOptions.Singleline | RegexOptions.Multiline);

        struct Block
        {
            public string File, Language, Text;
            public int Line;
            public override string ToString() => $"{System.IO.Path.GetFileName(File)}:{Line}";
        }

        static string FindPackageRoot([CallerFilePath] string thisFile = "")
        {
            try
            {
                string dir = Path.GetDirectoryName(thisFile);
                for (int i = 0; i < 4 && !string.IsNullOrEmpty(dir); i++)
                {
                    if (IsPackageRoot(dir)) return dir;
                    dir = Path.GetDirectoryName(dir);
                }
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is NotSupportedException) { }
            try
            {
                // In Unity, package paths resolve through Packages/<name>.
                string dir = Path.GetFullPath("Packages/com.dynamicdialog");
                if (IsPackageRoot(dir)) return dir;
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is NotSupportedException) { }
            return null;
        }

        static bool IsPackageRoot(string dir) =>
            File.Exists(Path.Combine(dir, "package.json")) && Directory.Exists(Path.Combine(dir, "Documentation~"));

        static string Root()
        {
            string root = FindPackageRoot();
            if (root == null) Assert.Ignore("Can't find the package folder with Documentation~ on disk.");
            return root;
        }

        static List<Block> Blocks(string root, string language)
        {
            var files = Directory.GetFiles(Path.Combine(root, "Documentation~"), "*.md").ToList();
            files.Add(Path.Combine(root, "README.md"));
            var blocks = new List<Block>();
            foreach (var file in files)
            {
                string text = File.ReadAllText(file);
                foreach (Match m in Fence.Matches(text))
                {
                    if (!string.Equals(m.Groups[1].Value, language, StringComparison.OrdinalIgnoreCase)) continue;
                    int line = 1 + text.Take(m.Index).Count(c => c == '\n');
                    blocks.Add(new Block { File = file, Language = language, Text = m.Groups[2].Value, Line = line });
                }
            }
            return blocks;
        }

        [Test]
        public void DlgExamples_ParseWithoutErrorsOrWarnings()
        {
            var blocks = Blocks(Root(), "dlg");
            Assert.That(blocks.Count, Is.GreaterThan(20), "Expected the manual to have many .dlg examples.");
            var problems = new List<string>();
            foreach (var b in blocks)
            {
                var source = DialogScriptParser.Parse(b.Text, b.ToString());
                foreach (var m in source.Messages) problems.Add($"{b}: {m}");
                if (source.Rules.Count == 0) problems.Add($"{b}: no rules");
                foreach (var r in source.Rules)
                {
                    try { r.ToRule(); }
                    catch (Exception e) { problems.Add($"{b}: rule {r.Name} doesn't build: {e.Message}"); }
                }
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void CsvExamples_ReadAsSpreadsheets()
        {
            var blocks = Blocks(Root(), "csv");
            Assert.That(blocks.Count, Is.GreaterThan(0));
            var problems = new List<string>();
            foreach (var b in blocks)
            {
                var source = SpreadsheetParser.Parse(b.Text, b.ToString());
                foreach (var m in source.Messages) problems.Add($"{b}: {m}");
                if (source.Rules.Count == 0) problems.Add($"{b}: no rules");
                foreach (var r in source.Rules) r.ToRule();
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void CSharpExamples_AppearInCompiledSnippetFiles()
        {
            string root = Root();
            var blocks = Blocks(root, "csharp");
            Assert.That(blocks.Count, Is.GreaterThan(10), "Expected the manual to have C# examples.");

            var snippetFiles = Directory.GetFiles(Path.Combine(root, "Tests"), "DocumentationSnippets.cs", SearchOption.AllDirectories);
            Assert.That(snippetFiles.Length, Is.GreaterThan(0), "No DocumentationSnippets.cs files found.");
            string compiled = string.Concat(snippetFiles.Select(f => Squash(File.ReadAllText(f))));

            var missing = blocks.Where(b => !compiled.Contains(Squash(b.Text))).Select(b => b.ToString()).ToList();
            Assert.That(missing, Is.Empty,
                "These C# examples aren't in any Tests/**/DocumentationSnippets.cs file, so nothing checks they compile:\n" + string.Join("\n", missing));
        }

        static string Squash(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                if (!char.IsWhiteSpace(c)) sb.Append(c);
            return sb.ToString();
        }
    }
}
