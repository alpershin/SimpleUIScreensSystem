using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimpleUIScreensSystem.Tests
{
    public sealed class UINavigatorTests
    {
        private static readonly ScreenId Settings = new ScreenId("settings");
        private static readonly ScreenId Inventory = new ScreenId("inventory");
        private static readonly FieldInfo IdField =
            typeof(UIScreen).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<GameObject> _objects = new List<GameObject>();
        private UINavigator _navigator;

        [SetUp]
        public void SetUp()
        {
            Assert.That(IdField, Is.Not.Null, "UIScreen no longer serializes its ID in '_id'; update the test.");
            _navigator = new UINavigator();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in _objects)
                if (gameObject != null) Object.DestroyImmediate(gameObject);
            _objects.Clear();
        }

        [Test]
        public void AddRejectsScreensWithoutIdAndDuplicateIds()
        {
            var unnamed = CreateScreen(null);
            Assert.That(() => _navigator.Add(unnamed), Throws.ArgumentException);

            var first = CreateScreen("settings");
            var duplicate = CreateScreen("settings");
            _navigator.Add(first);
            Assert.DoesNotThrow(() => _navigator.Add(first));
            Assert.That(() => _navigator.Add(duplicate), Throws.InvalidOperationException);
            Assert.That(() => _navigator.Add(null), Throws.ArgumentNullException);
        }

        [Test]
        public void OpenAndCloseTrackTheOpenListAndUnknownKeysThrow()
        {
            var screen = CreateScreen("settings");
            _navigator.Add(screen);

            _navigator.Open(Settings);
            Assert.That(screen.IsOpen, Is.True);
            Assert.That(_navigator.OpenedScreensCount, Is.EqualTo(1));

            _navigator.Close(Settings);
            Assert.That(screen.IsOpen, Is.False);
            Assert.That(_navigator.OpenedScreensCount, Is.EqualTo(0));

            Assert.That(() => _navigator.Open(Inventory), Throws.ArgumentException);
            Assert.That(() => _navigator.Open(default), Throws.ArgumentException);
            Assert.That(_navigator.TryGetScreen(Inventory, out _), Is.False);
        }

        [Test]
        public void CloseCallbackFiresOnceForItsOwnOpeningOnly()
        {
            var screen = CreateScreen("settings");
            _navigator.Add(screen);
            var firstCallbacks = 0;

            _navigator.Open(Settings, () => firstCallbacks++);
            _navigator.Open(Settings, () => Assert.Fail("A repeated open must not replace the callback."));
            _navigator.Close(Settings);
            Assert.That(firstCallbacks, Is.EqualTo(1));

            _navigator.Open(Settings);
            _navigator.Close(Settings);
            Assert.That(firstCallbacks, Is.EqualTo(1), "Stale callbacks must not fire for later openings.");
        }

        [Test]
        public void CloseAllClosesEveryScreenEvenWhenCallbacksChangeTheOpenList()
        {
            var settings = CreateScreen("settings");
            var inventory = CreateScreen("inventory");
            _navigator.Add(settings);
            _navigator.Add(inventory);

            _navigator.Open(Settings, () => _navigator.Close(Inventory));
            _navigator.Open(Inventory);
            Assert.DoesNotThrow(() => _navigator.CloseAll());

            Assert.That(settings.IsOpen, Is.False);
            Assert.That(inventory.IsOpen, Is.False);
            Assert.That(_navigator.OpenedScreensCount, Is.EqualTo(0));
        }

        [Test]
        public void ExternalDeactivationCountsAsClosing()
        {
            var screen = CreateScreen("settings");
            _navigator.Add(screen);
            var closed = false;

            _navigator.Open(Settings, () => closed = true);
            screen.gameObject.SetActive(false);

            Assert.That(closed, Is.True);
            Assert.That(_navigator.OpenedScreensCount, Is.EqualTo(0));
        }

        [Test]
        public void RemoveDropsSubscriptionsAndDestroyedScreensAreForgotten()
        {
            var screen = CreateScreen("settings");
            _navigator.Add(screen);
            _navigator.Open(Settings, () => Assert.Fail("Removed screens must not report closing."));
            _navigator.Remove(screen);

            Assert.That(_navigator.OpenedScreensCount, Is.EqualTo(0));
            Assert.That(_navigator.TryGetScreen(Settings, out _), Is.False);
            Assert.DoesNotThrow(() => screen.Close());

            var replacement = CreateScreen("settings");
            _navigator.Add(replacement);
            Object.DestroyImmediate(replacement.gameObject);
            Assert.That(_navigator.TryGetScreen(Settings, out _), Is.False);
            Assert.That(() => _navigator.Open(Settings), Throws.ArgumentException);
        }

        private UIScreen CreateScreen(string id)
        {
            var gameObject = new GameObject("Screen " + (id ?? "unnamed"));
            gameObject.SetActive(false);
            _objects.Add(gameObject);
            var screen = gameObject.AddComponent<UIScreen>();
            IdField.SetValue(screen, id);
            return screen;
        }
    }
}
