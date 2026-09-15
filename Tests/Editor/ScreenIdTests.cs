using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace SimpleUIScreensSystem.Tests
{
    public sealed class ScreenIdTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        [TestCase("\t\r\n")]
        public void ConstructorRejectsMissingIdentity(string value)
        {
            Assert.That(() => new ScreenId(value), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void ConstructorPreservesIdentityExactly()
        {
            var id = new ScreenId(" Settings/v2 ");
            Assert.That(id.IsValid, Is.True);
            Assert.That(id.Value, Is.EqualTo(" Settings/v2 "));
            Assert.That(id.ToString(), Is.EqualTo(id.Value));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void FromSerializedTreatsBlankAsInvalidInsteadOfThrowing(string value)
        {
            Assert.That(ScreenId.FromSerialized(value).IsValid, Is.False);
            Assert.That(ScreenId.FromSerialized("settings"), Is.EqualTo(new ScreenId("settings")));
        }

        [Test]
        public void DefaultIsInvalidAndCanBeComparedAndHashedSafely()
        {
            var empty = default(ScreenId);
            Assert.That(empty.IsValid, Is.False);
            Assert.That(empty.Equals(default(ScreenId)), Is.True);
            Assert.That(empty == default, Is.True);
            Assert.That(empty != new ScreenId("settings"), Is.True);
            Assert.That(empty.Equals(null), Is.False);
            Assert.DoesNotThrow(() => empty.GetHashCode());
            Assert.DoesNotThrow(() => empty.ToString());
        }

        [Test]
        public void EquivalentValuesHaveEqualKeysAndHashes()
        {
            var first = new ScreenId("settings");
            var second = new ScreenId(new string("settings".ToCharArray()));
            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.Equals("settings"), Is.False);
        }

        [Test]
        public void EqualityIsCaseSensitiveAndDoesNotNormalizeWhitespace()
        {
            var settings = new ScreenId("settings");
            Assert.That(settings == new ScreenId("Settings"), Is.False);
            Assert.That(settings == new ScreenId(" settings "), Is.False);
            Assert.That(settings.Equals(new ScreenId("inventory")), Is.False);
        }

        [Test]
        public void DictionaryFindsEquivalentKeyAndSeparatesCaseVariants()
        {
            var screens = new Dictionary<ScreenId, string>
            {
                [new ScreenId("settings")] = "lowercase",
                [new ScreenId("Settings")] = "uppercase"
            };
            Assert.That(screens[new ScreenId("settings")], Is.EqualTo("lowercase"));
            Assert.That(screens[new ScreenId("Settings")], Is.EqualTo("uppercase"));
            Assert.That(screens.ContainsKey(default), Is.False);
            Assert.That(screens.Count, Is.EqualTo(2));
        }
    }
}
