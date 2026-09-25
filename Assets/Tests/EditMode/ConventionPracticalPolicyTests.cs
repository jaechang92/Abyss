using CodeConvention.Editor;
using NUnit.Framework;

namespace Abyss.Tests.EditMode
{
    public class ConventionPracticalPolicyTests
    {
        [TestCase("changed")]
        [TestCase("requiresAlive")]
        [TestCase("WasClosedThisFrame")]
        [TestCase("applicationIsQuitting")]
        [TestCase("cameraShakeLookupAttempted")]
        [TestCase("prettyPrint")]
        public void ClearStateAndOptionNamesAreAllowed(string name)
        {
            Assert.IsTrue(ConventionPracticalPolicy.IsDescriptiveBoolean(name));
        }

        [TestCase("ok")]
        [TestCase("foo")]
        [TestCase("flag")]
        [TestCase("value")]
        [TestCase("data")]
        public void VagueNamesStillNeedReview(string name)
        {
            Assert.IsFalse(ConventionPracticalPolicy.IsDescriptiveBoolean(name));
        }

        [TestCase("[System.Serializable] public class Save")]
        [TestCase("public class Settings : ScriptableObject")]
        [TestCase("public class Actor : MonoBehaviour")]
        [TestCase("private struct ButtonHandle")]
        public void DataFieldsAllowCamelCase(string declaration)
        {
            var context = new ConventionDataContext(declaration + " {\npublic string name;\n}");
            Assert.IsTrue(context.IsDataField(2, "name"));
        }

        [Test]
        public void OrdinaryClassDoesNotGetExemptionFromDataSuffix()
        {
            var context = new ConventionDataContext("public class UnknownData {\npublic string name;\n}");
            Assert.IsFalse(context.IsDataField(2, "name"));
        }

        [Test]
        public void NestedClassDoesNotInheritContainingTypesExemption()
        {
            var context = new ConventionDataContext("[Serializable] class Save { class Worker {\npublic string name;\n} }");
            Assert.IsFalse(context.IsDataField(2, "name"));
        }

        [Test]
        public void PropertiesAndStaticFieldsRemainOutsideExemption()
        {
            var context = new ConventionDataContext("[Serializable] class Save {\npublic string name => null;\npublic static string shared;\npublic readonly string fixedName;\n}");
            Assert.IsFalse(context.IsDataField(2, "name"));
            Assert.IsFalse(context.IsDataField(3, "shared"));
            Assert.IsFalse(context.IsDataField(4, "fixedName"));
        }

        [Test]
        public void NonSerializedExclusionDoesNotLeakToNextField()
        {
            var context = new ConventionDataContext("[Serializable] class Save {\n[NonSerialized] public string cache;\npublic string name;\n}");
            Assert.IsFalse(context.IsDataField(2, "cache"));
            Assert.IsTrue(context.IsDataField(3, "name"));
        }

        [Test]
        public void KnownBaseInSameFilePreservesDataContract()
        {
            var context = new ConventionDataContext("class Base : ScriptableObject {}\nclass Derived : Base {\npublic string name;\n}");
            Assert.IsTrue(context.IsDataField(3, "name"));
        }

        [Test]
        public void CommentDoesNotCreateDataExemption()
        {
            var context = new ConventionDataContext("// [Serializable]\nclass Worker {\npublic string name;\n}");
            Assert.IsFalse(context.IsDataField(3, "name"));
        }
    }
}
