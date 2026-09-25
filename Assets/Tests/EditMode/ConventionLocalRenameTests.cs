using System;
using System.Linq;
using CodeConvention.Editor;
using NUnit.Framework;

namespace Abyss.Tests.EditMode
{
    public class ConventionLocalRenameTests
    {
        [Test]
        public void AssetPathAcceptsWindowsSeparators()
        {
            string path = UnityEngine.Application.dataPath.Replace('/', '\\') + "\\Plugins\\Example.cs";
            Assert.IsTrue(CodeConventionWindow.TryGetAssetPath(path, out string result));
            Assert.AreEqual("Assets/Plugins/Example.cs", result);
        }

        [Test]
        public void AssetPathAcceptsProjectRelativePath()
        {
            Assert.IsTrue(CodeConventionWindow.TryGetAssetPath("Assets/Plugins/Example.cs", out string result));
            Assert.AreEqual("Assets/Plugins/Example.cs", result);
        }

        [Test]
        public void AssetPathRejectsSiblingAndTraversal()
        {
            Assert.IsFalse(CodeConventionWindow.TryGetAssetPath(UnityEngine.Application.dataPath + "Backup/Example.cs", out _));
            Assert.IsFalse(CodeConventionWindow.TryGetAssetPath("Assets/../Library/Example.cs", out _));
        }

        [Test]
        public void LocalBoolRenamesDeclarationAndUsesButNotMemberOrComment()
        {
            const string SOURCE = "class Demo { bool changed; private bool Run() {\n" +
                "bool changed = false;\n// changed is local\nthis.changed = changed;\nreturn changed;\n} }";
            var violation = Violation(SOURCE, "bool changed = false;", "changed", "BoolNamingConvention");
            Assert.IsTrue(ConventionLocalRename.TryCreate(SOURCE, violation, "isChanged", out var plan, out var reason), reason);
            Assert.AreEqual(3, plan.ReplacementCount);
            Assert.That(plan.Updated, Does.Contain("this.changed = isChanged;"));
            Assert.That(plan.Updated, Does.Contain("// changed is local"));
            Assert.That(plan.Updated, Does.Contain("return isChanged;"));
        }

        [Test]
        public void LocalConstantDoesNotRenameSameNameInAnotherMethod()
        {
            const string SOURCE = "class Demo { private string First() {\nconst string garbage = \"bad\";\nreturn garbage;\n}\n" +
                "private string Second() {\nconst string garbage = \"other\";\nreturn garbage;\n} }";
            var violation = Violation(SOURCE, "const string garbage = \"bad\";", "garbage", "ConstantUpperCase");
            Assert.IsTrue(ConventionLocalRename.TryCreate(SOURCE, violation, "GARBAGE", out var plan, out var reason), reason);
            Assert.AreEqual(2, plan.ReplacementCount);
            Assert.That(plan.Updated, Does.Contain("return GARBAGE;"));
            Assert.That(plan.Updated, Does.Contain("const string garbage = \"other\";\nreturn garbage;"));
        }

        [Test]
        public void FieldWithoutAccessModifierIsNotMistakenForLocal()
        {
            const string SOURCE = "class Demo {\nbool changed = false;\n}";
            Assert.IsFalse(ConventionLocalRename.TryCreate(SOURCE,
                Violation(SOURCE, "bool changed = false;", "changed", "BoolNamingConvention"),
                "isChanged", out _, out _));
        }

        [Test]
        public void PrimaryConstructorDoesNotMakeFieldALocal()
        {
            const string SOURCE = "public class Demo(int value) {\nbool changed = false;\n}";
            Assert.IsFalse(ConventionLocalRename.TryCreate(SOURCE,
                Violation(SOURCE, "bool changed = false;", "changed", "BoolNamingConvention"),
                "isChanged", out _, out _));
        }

        [Test]
        public void ExistingCandidateNameBlocksRename()
        {
            const string SOURCE = "class Demo { private void Run() {\nbool changed = false;\nbool isChanged = true;\n} }";
            Assert.IsFalse(ConventionLocalRename.TryCreate(SOURCE,
                Violation(SOURCE, "bool changed = false;", "changed", "BoolNamingConvention"),
                "isChanged", out _, out _));
        }

        [Test]
        public void ChangedLineBlocksStaleDiagnostic()
        {
            const string SOURCE = "class Demo { private void Run() {\nbool changed = false;\n} }";
            var violation = Violation(SOURCE, "bool changed = false;", "changed", "BoolNamingConvention");
            Assert.IsFalse(ConventionLocalRename.TryCreate(SOURCE.Replace("false", "true"), violation,
                "isChanged", out _, out _));
        }

        [TestCase("return nameof(changed);")]
        [TestCase("return new { changed };")]
        [TestCase("return $\"{changed}\";")]
        [TestCase("return \"changed\";")]
        public void NameSensitiveExpressionsAreNotAutomaticallyChanged(string expression)
        {
            string source = "class Demo { private object Run() {\nbool changed = false;\n" + expression + "\n} }";
            Assert.IsFalse(ConventionLocalRename.TryCreate(source,
                Violation(source, "bool changed = false;", "changed", "BoolNamingConvention"),
                "isChanged", out _, out _));
        }

        [Test]
        public void NewNameMustStillSatisfyRule()
        {
            const string SOURCE = "class Demo { private void Run() {\nbool changed = false;\n} }";
            Assert.IsFalse(ConventionLocalRename.TryCreate(SOURCE,
                Violation(SOURCE, "bool changed = false;", "changed", "BoolNamingConvention"),
                "changedAgain", out _, out _));
        }

        [Test]
        public void MultipleDeclaratorsAreNotChanged()
        {
            const string SOURCE = "class Demo { private void Run() {\nbool changed = false, other = true;\n} }";
            Assert.IsFalse(ConventionLocalRename.TryCreate(SOURCE,
                Violation(SOURCE, "bool changed = false, other = true;", "changed", "BoolNamingConvention"),
                "isChanged", out _, out _));
        }

        private static ConventionViolation Violation(string source, string declaration, string name, string rule)
        {
            int index = source.IndexOf(declaration, StringComparison.Ordinal);
            return new ConventionViolation { LineNumber = source.Substring(0, index).Count(c => c == '\n') + 1,
                LineContent = declaration, MatchedName = name, RuleName = rule, Severity = ViolationSeverity.Warning };
        }

        [Test]
        public void BatchCombinesMultipleLocalsInOneFile()
        {
            const string SOURCE = "class Demo { private bool Run() {\nbool changed = false;\nbool ready = true;\nreturn changed && ready;\n} }";
            var changed = Violation(SOURCE, "bool changed = false;", "changed", "BoolNamingConvention");
            var ready = Violation(SOURCE, "bool ready = true;", "ready", "BoolNamingConvention");
            var batch = ConventionBatchPlanner.Build("Demo.cs", SOURCE, new[] { changed, ready });
            Assert.AreEqual(2, batch.AcceptedCount);
            Assert.AreEqual(SOURCE, batch.Plan.Original);
            Assert.That(batch.Plan.Updated, Does.Contain("return isChanged && isReady;"));
            Assert.AreEqual(0, batch.Skipped.Count);
        }

        [Test]
        public void BatchDoesNotApplyDuplicateDiagnosticsTwice()
        {
            const string SOURCE = "class Demo { private bool Run() {\nbool changed = false;\nreturn changed;\n} }";
            var violation = Violation(SOURCE, "bool changed = false;", "changed", "BoolNamingConvention");
            var batch = ConventionBatchPlanner.Build("Demo.cs", SOURCE, new[] { violation, violation });
            Assert.AreEqual(1, batch.AcceptedCount);
            Assert.AreEqual(2, batch.Plan.ReplacementCount);
        }

        [Test]
        public void BatchChecksCollisionsIntroducedByEarlierRename()
        {
            const string SOURCE = "class Demo { private bool Run() {\nbool changed = false;\nbool Changed = true;\nreturn changed && Changed;\n} }";
            var first = Violation(SOURCE, "bool changed = false;", "changed", "BoolNamingConvention");
            var second = Violation(SOURCE, "bool Changed = true;", "Changed", "BoolNamingConvention");
            var batch = ConventionBatchPlanner.Build("Demo.cs", SOURCE, new[] { first, second });
            Assert.AreEqual(1, batch.AcceptedCount);
            Assert.AreEqual(1, batch.Skipped.Count);
            Assert.That(batch.Plan.Updated, Does.Contain("return isChanged && Changed;"));
        }
    }
}
