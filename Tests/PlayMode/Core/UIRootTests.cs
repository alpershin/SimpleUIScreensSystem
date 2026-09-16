using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SimpleUIScreensSystem.Tests
{
    public sealed class UIRootTests
    {
        private static readonly ScreenId Settings = new ScreenId("settings");
        private static readonly ScreenId Inventory = new ScreenId("inventory");
        private static readonly ScreenId SceneOnly = new ScreenId("scene-only");
        private static readonly ScreenId Unknown = new ScreenId("unknown");

        private static readonly FieldInfo SourcesField =
            typeof(UIRoot).GetField("_sources", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SceneScreensField =
            typeof(UIRoot).GetField("_includeSceneScreens", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo IdField =
            typeof(UIScreen).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<UIScreen> _registered = new List<UIScreen>();

        [SetUp]
        public void SetUp()
        {
            Assert.That(SourcesField, Is.Not.Null, "UIRoot no longer serializes '_sources'; update the test.");
            Assert.That(SceneScreensField, Is.Not.Null, "UIRoot no longer serializes '_includeSceneScreens'; update the test.");
            Assert.That(IdField, Is.Not.Null, "UIScreen no longer serializes '_id'; update the test.");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var screen in _registered) UINavigator.Instance.Remove(screen);
            _registered.Clear();
            foreach (var gameObject in _objects)
                if (gameObject != null) Object.DestroyImmediate(gameObject);
            _objects.Clear();
        }

        [Test]
        public void RoutesEachKeyToTheSourceThatOwnsIt()
        {
            var settings = CreateSource(Settings);
            var inventory = CreateSource(Inventory);
            var root = CreateRoot(false, settings, inventory);

            root.Open(Settings);
            root.Close(Inventory);

            Assert.That(settings.Calls, Is.EqualTo(new[] { "Open:settings" }));
            Assert.That(inventory.Calls, Is.EqualTo(new[] { "Close:inventory" }));
            Assert.That(root.Contains(Settings), Is.True);
            Assert.That(root.TryResolve(Inventory, out var owner), Is.True);
            Assert.That(owner, Is.SameAs(inventory));
        }

        [Test]
        public void EarlierSourcesWinOverLaterOnes()
        {
            var first = CreateSource(Settings);
            var second = CreateSource(Settings);
            var root = CreateRoot(false, first, second);

            root.Open(Settings);

            Assert.That(first.Calls, Is.EqualTo(new[] { "Open:settings" }));
            Assert.That(second.Calls, Is.Empty);
        }

        [Test]
        public void SceneScreensServeKeysNoSourceOwnsAndCanBeTurnedOff()
        {
            var screen = CreateRegisteredScreen("scene-only");
            var source = CreateSource(Settings);

            var root = CreateRoot(true, source);
            Assert.That(root.Contains(SceneOnly), Is.True);
            root.Open(SceneOnly);
            Assert.That(screen.IsOpen, Is.True);
            Assert.That(root.TryGetScreen(SceneOnly, out var found), Is.True);
            Assert.That(found, Is.SameAs(screen));

            var isolated = CreateRoot(false, source);
            Assert.That(isolated.Contains(SceneOnly), Is.False);
            Assert.That(() => isolated.Open(SceneOnly), Throws.ArgumentException);
        }

        [Test]
        public void UnknownAndInvalidKeysAreNotOwnedAndThrowOnOpen()
        {
            var root = CreateRoot(false, CreateSource(Settings));

            Assert.That(root.Contains(Unknown), Is.False);
            Assert.That(root.Contains(default), Is.False);
            Assert.That(root.TryGetScreen(Unknown, out var screen), Is.False);
            Assert.That(screen, Is.Null);
            Assert.That(() => root.Open(Unknown), Throws.ArgumentException);
            Assert.That(() => root.Close(default), Throws.ArgumentException);
        }

        [Test]
        public void ScreenOpenedIsForwardedOnlyWhileEnabled()
        {
            var source = CreateSource(Settings);
            var root = CreateRoot(false, source);
            var seen = new List<ScreenId>();
            root.ScreenOpened += (id, _) => seen.Add(id);

            source.RaiseOpened(Settings);
            Assert.That(seen, Is.EqualTo(new[] { Settings }));

            root.enabled = false;
            source.RaiseOpened(Settings);
            Assert.That(seen.Count, Is.EqualTo(1), "A disabled root must not forward events.");

            root.enabled = true;
            source.RaiseOpened(Settings);
            Assert.That(seen.Count, Is.EqualTo(2));
        }

        [Test]
        public void CloseAllReachesEverySource()
        {
            var first = CreateSource(Settings);
            var second = CreateSource(Inventory);
            var screen = CreateRegisteredScreen("scene-only");
            var root = CreateRoot(true, first, second);
            root.Open(SceneOnly);

            root.CloseAll();

            Assert.That(first.Calls, Is.EqualTo(new[] { "CloseAll" }));
            Assert.That(second.Calls, Is.EqualTo(new[] { "CloseAll" }));
            Assert.That(screen.IsOpen, Is.False);
        }

        [Test]
        public void ComponentsThatAreNotSourcesAreReportedAndSkipped()
        {
            var stranger = new GameObject("Stranger", typeof(BoxCollider));
            _objects.Add(stranger);
            LogAssert.Expect(LogType.Error, new Regex("does not implement IScreenSource"));

            var source = CreateSource(Settings);
            var root = CreateRoot(false, stranger.GetComponent<BoxCollider>(), source);

            Assert.That(root.Sources, Is.EqualTo(new[] { source }));
            root.Open(Settings);
            Assert.That(source.Calls, Is.EqualTo(new[] { "Open:settings" }));
        }

        [Test]
        public void SourcesCanBeRegisteredAndDroppedAtRuntime()
        {
            var root = CreateRoot(false);
            var source = CreateSource(Settings);
            var seen = 0;
            root.ScreenOpened += (_, __) => seen++;

            Assert.That(root.Contains(Settings), Is.False);
            root.AddSource(source);
            root.AddSource(source);
            Assert.That(root.Sources.Count, Is.EqualTo(1), "Adding the same source twice must not duplicate it.");

            root.Open(Settings);
            source.RaiseOpened(Settings);
            Assert.That(seen, Is.EqualTo(1));

            Assert.That(root.RemoveSource(source), Is.True);
            Assert.That(root.RemoveSource(source), Is.False);
            Assert.That(root.Contains(Settings), Is.False);
            source.RaiseOpened(Settings);
            Assert.That(seen, Is.EqualTo(1), "A dropped source must not forward events.");
            Assert.That(() => root.AddSource(null), Throws.ArgumentNullException);
        }

        private UIRoot CreateRoot(bool includeSceneScreens, params Component[] sources)
        {
            var gameObject = new GameObject("UI root");
            gameObject.SetActive(false);
            _objects.Add(gameObject);
            var root = gameObject.AddComponent<UIRoot>();
            SourcesField.SetValue(root, sources);
            SceneScreensField.SetValue(root, includeSceneScreens);
            gameObject.SetActive(true);
            return root;
        }

        private FakeScreenSource CreateSource(params ScreenId[] owned)
        {
            var gameObject = new GameObject("Source");
            _objects.Add(gameObject);
            var source = gameObject.AddComponent<FakeScreenSource>();
            source.Owned.AddRange(owned);
            return source;
        }

        private UIScreen CreateRegisteredScreen(string id)
        {
            var gameObject = new GameObject("Screen " + id);
            gameObject.SetActive(false);
            _objects.Add(gameObject);
            var screen = gameObject.AddComponent<UIScreen>();
            IdField.SetValue(screen, id);
            UINavigator.Instance.Add(screen);
            _registered.Add(screen);
            return screen;
        }

        public sealed class FakeScreenSource : MonoBehaviour, IScreenSource
        {
            public readonly List<ScreenId> Owned = new List<ScreenId>();
            public readonly List<string> Calls = new List<string>();

            public event Action<ScreenId, UIScreen> ScreenOpened;

            public void RaiseOpened(ScreenId screenId) => ScreenOpened?.Invoke(screenId, null);

            public bool Contains(ScreenId screenId) => Owned.Contains(screenId);
            public void Open(ScreenId screenId) => Calls.Add("Open:" + screenId);
            public void Close(ScreenId screenId) => Calls.Add("Close:" + screenId);
            public void CloseAll() => Calls.Add("CloseAll");

            public bool TryGetScreen(ScreenId screenId, out UIScreen screen)
            {
                screen = null;
                return false;
            }
        }
    }
}
