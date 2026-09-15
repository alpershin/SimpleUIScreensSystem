using System;
using NUnit.Framework;
using SimpleUIScreensSystem.Editor;

namespace SimpleUIScreensSystem.Tests
{
    public sealed class ScreenKeysGeneratorTests
    {
        private const string CatalogGuid = "a8f8b217f3a84d738ba3b89ef2b60111";

        [Test]
        public void GeneratedKeysKeepStableIdsAndDoNotDependOnEntryOrder()
        {
            var settings = Entry("settings-stable-id", "Settings");
            var inventory = Entry("inventory-stable-id", "Inventory");

            var source = Generate(settings, inventory);

            Assert.That(source, Is.EqualTo(Generate(inventory, settings)));
            Assert.That(source, Does.Contain("namespace Game.UI"));
            Assert.That(source, Does.Contain("public static class Screens"));
            Assert.That(source, Does.Contain("ScreenId Settings = new global::SimpleUIScreensSystem.ScreenId(\"settings-stable-id\");"));
            Assert.That(source.IndexOf("Inventory", StringComparison.Ordinal), Is.LessThan(source.IndexOf("Settings", StringComparison.Ordinal)));
        }

        [Test]
        public void RenamingCodeNameKeepsTheStoredIdentity()
        {
            Assert.That(Generate(Entry("stable-id", "Options")),
                Does.Contain("ScreenId Options = new global::SimpleUIScreensSystem.ScreenId(\"stable-id\");"));
        }

        [Test]
        public void StringLiteralsEscapeQuotesSlashesControlsAndUnicodeLineSeparators()
        {
            var source = Generate(Entry("id\"\\\n\r\t\0\u2028\u2029\ud800", "Settings"));

            Assert.That(source, Does.Contain("(\"id\\\"\\\\\\n\\r\\t\\u0000\\u2028\\u2029\\ud800\");"));
            Assert.That(source, Does.Not.Contain("\u2028"));
        }

        [TestCase("")]
        [TestCase("2Settings")]
        [TestCase("Settings Page")]
        [TestCase("Settings;}")]
        [TestCase("class")]
        [TestCase("@class")]
        [TestCase("record")]
        public void RejectsInvalidCodeNames(string name)
        {
            Assert.Throws<ArgumentException>(() => Generate(Entry("settings", name)));
        }

        [TestCase("")]
        [TestCase("Game..UI")]
        [TestCase("Game.class")]
        [TestCase("Game UI")]
        public void RejectsInvalidNamespaces(string namespaceName)
        {
            Assert.Throws<ArgumentException>(() => ScreenKeysGenerator.Generate(namespaceName, "Screens", CatalogGuid,
                new[] { Entry("settings", "Settings") }));
        }

        [TestCase("class")]
        [TestCase("9Screens")]
        public void RejectsInvalidClassNames(string className)
        {
            Assert.Throws<ArgumentException>(() => ScreenKeysGenerator.Generate("Game.UI", className, CatalogGuid,
                new[] { Entry("settings", "Settings") }));
        }

        [Test]
        public void RejectsDuplicateIdentitiesInsteadOfSilentlyReplacingThem()
        {
            Assert.Throws<InvalidOperationException>(() => Generate(Entry("same", "Settings"), Entry("same", "Inventory")));
        }

        [Test]
        public void RejectsDuplicateMemberNames()
        {
            Assert.Throws<InvalidOperationException>(() => Generate(Entry("first", "Settings"), Entry("second", "Settings")));
        }

        [Test]
        public void RejectsMemberWithTheClassName()
        {
            Assert.Throws<InvalidOperationException>(() => Generate(Entry("settings", "Screens")));
        }

        [Test]
        public void RejectsUninitializedIdentity()
        {
            Assert.Throws<InvalidOperationException>(() => Generate(new ScreenKeysGenerator.Entry(default, "Settings")));
        }

        [Test]
        public void OnlyRecognizesFilesGeneratedForTheSameCatalog()
        {
            var source = Generate(Entry("settings", "Settings"));
            Assert.That(ScreenKeysGenerator.IsOwnedByCatalog(source, CatalogGuid), Is.True);
            Assert.That(ScreenKeysGenerator.IsOwnedByCatalog(source.Replace("\n", "\r\n"), CatalogGuid), Is.True);
            Assert.That(ScreenKeysGenerator.IsOwnedByCatalog(source, "b8f8b217f3a84d738ba3b89ef2b60111"), Is.False);
            Assert.That(ScreenKeysGenerator.IsOwnedByCatalog("public static class Screens {}", CatalogGuid), Is.False);
            Assert.That(ScreenKeysGenerator.IsOwnedByCatalog(null, CatalogGuid), Is.False);
        }

        [Test]
        public void RejectsUnsavedCatalogWithoutAnAssetGuid()
        {
            Assert.Throws<ArgumentException>(() => ScreenKeysGenerator.Generate("Game.UI", "Screens", string.Empty,
                new[] { Entry("settings", "Settings") }));
        }

        private static ScreenKeysGenerator.Entry Entry(string id, string name)
        {
            return new ScreenKeysGenerator.Entry(new ScreenId(id), name);
        }

        private static string Generate(params ScreenKeysGenerator.Entry[] entries)
        {
            return ScreenKeysGenerator.Generate("Game.UI", "Screens", CatalogGuid, entries);
        }
    }
}
