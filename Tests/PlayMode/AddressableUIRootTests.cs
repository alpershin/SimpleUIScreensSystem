using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SimpleUIScreensSystem.AddressableUI.Tests
{
    public sealed class AddressableUIRootTests
    {
        private static readonly ScreenId First = new ScreenId("first");
        private static readonly ScreenId Second = new ScreenId("second");
        private static readonly ScreenId Third = new ScreenId("third");

        private readonly List<GameObject> _prefabs = new List<GameObject>();
        private readonly Dictionary<ScreenId, string> _keys = new Dictionary<ScreenId, string>();
        private AddressableUIRoot _root;
        private AddressableScreenCatalog _catalog;
        private MemoryScreenProvider _provider;
        private ResourceLocationMap _locator;
        private object _addressablesImplementation;
        private FieldInfo _initializationField;
        private bool _wasInitialized;
        private Action<AsyncOperationHandle, Exception> _exceptionHandler;

        [SetUp]
        public void SetUp()
        {
            // The fixture supplies its own locator. Bypass only the catalog bootstrap so
            // these tests also run in projects with no built Addressables content.
            var resourceManager = Addressables.ResourceManager;
            var implementationField = typeof(Addressables).GetField("m_AddressablesInstance",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(implementationField, Is.Not.Null, "Update the fixture for this Addressables version.");
            _addressablesImplementation = implementationField.GetValue(null);
            _initializationField = _addressablesImplementation.GetType().GetField("hasStartedInitialization",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(_initializationField, Is.Not.Null, "Update the fixture for this Addressables version.");
            _wasInitialized = (bool)_initializationField.GetValue(_addressablesImplementation);
            _initializationField.SetValue(_addressablesImplementation, true);
            // InitializeAsync assigns these after the flag above; TrackHandle subscribes them to every load.
            EnsureTrackingCallback("m_OnHandleCompleteAction", "OnHandleCompleted");
            EnsureTrackingCallback("m_OnSceneHandleCompleteAction", "OnSceneHandleCompleted");
            EnsureTrackingCallback("m_OnHandleDestroyedAction", "OnHandleDestroyed");

            _exceptionHandler = ResourceManager.ExceptionHandler;
            _provider = new MemoryScreenProvider();
            _locator = new ResourceLocationMap("ui-tests-" + Guid.NewGuid().ToString("N"));
            resourceManager.ResourceProviders.Add(_provider);
            Addressables.AddResourceLocator(_locator);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ScreenFixture.Initialized = null;
            if (_root != null) Object.Destroy(_root.gameObject);
            yield return null;
            _provider?.CompleteAll();
            // Deferred destruction releases must outlive the root itself.
            for (var i = 0; i < 4; i++) yield return null;

            if (_locator != null) Addressables.RemoveResourceLocator(_locator);
            if (_provider != null) Addressables.ResourceManager.ResourceProviders.Remove(_provider);
            if (_initializationField != null)
                _initializationField.SetValue(_addressablesImplementation, _wasInitialized);
            ResourceManager.ExceptionHandler = _exceptionHandler;
            foreach (var prefab in _prefabs)
                if (prefab != null) Object.Destroy(prefab);
            if (_catalog != null) Object.Destroy(_catalog);
            _prefabs.Clear();
            _keys.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedOpenSharesLoadAndInstanceAndReleasesAfterClose()
        {
            CreateRoot(CreateScreen(First));
            var opened = 0;
            _root.ScreenOpened += (_, __) => opened++;

            _root.Open(First);
            _root.Open(new ScreenId(First.Value));
            yield return Until(() => IsPending(First), "The requested load did not start.");
            Assert.That(LoadCount(First), Is.EqualTo(1));
            Complete(First);
            yield return Until(() => opened == 1, "The screen was not opened exactly once.");
            Assert.That(_root.TryGetScreen(First, out var original), Is.True);

            _root.Open(First);
            yield return null;
            Assert.That(opened, Is.EqualTo(1));
            Assert.That(_root.TryGetScreen(new ScreenId(First.Value), out var repeated), Is.True);
            Assert.That(repeated, Is.SameAs(original));

            var releasedAfterDestruction = false;
            _provider.Released += key =>
            {
                if (key == _keys[First]) releasedAfterDestruction = original == null;
            };

            _root.Close(First);
            // Trimming in the same frame must defer release, then finish without another trim call.
            _root.TrimCache();
            Assert.That(ReleaseCount(First), Is.Zero, "Destroy has not completed in this frame.");
            yield return Until(() => ReleaseCount(First) == 1, "Prefab handle leaked after cache trim.");
            Assert.That(releasedAfterDestruction, Is.True, "The clone must be destroyed before releasing its prefab.");
        }

        [UnityTest]
        public IEnumerator InvalidOrUnknownKeysCannotStartLoads()
        {
            CreateRoot(CreateScreen(First));
            var unknown = new ScreenId("First");
            Assert.Throws<ArgumentException>(() => _root.Open(default));
            Assert.Throws<ArgumentException>(() => _root.Close(default));
            Assert.Throws<ArgumentException>(() => _root.Open(unknown));
            Assert.Throws<ArgumentException>(() => _root.Close(unknown));
            Assert.That(_root.TryGetScreen(default, out _), Is.False);
            Assert.That(_root.TryGetScreen(unknown, out _), Is.False);
            yield return null;
            Assert.That(LoadCount(First), Is.Zero);
            Assert.That(_root.ResidentScreenCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ClosingPendingRequestPreventsLateActivation()
        {
            CreateRoot(CreateScreen(First));
            var opened = 0;
            _root.ScreenOpened += (_, __) => opened++;
            _root.Open(First);
            yield return Until(() => IsPending(First), "The requested load did not start.");

            _root.Close(First);
            Complete(First);
            for (var i = 0; i < 3; i++) yield return null;
            Assert.That(opened, Is.Zero);
            Assert.That(_root.TryGetScreen(First, out _), Is.False);
            _root.TrimCache();
            yield return Until(() => ReleaseCount(First) == 1, "Cancelled request retained its prefab after trim.");
        }

        [UnityTest]
        public IEnumerator OpenFromScreenInitializationDoesNotCreateAnotherInstance()
        {
            CreateRoot(CreateScreen(First));
            var opened = 0;
            _root.ScreenOpened += (_, __) => opened++;
            ScreenFixture.Initialized = _ => _root.Open(First);
            _root.Open(First);
            yield return Until(() => IsPending(First), "The requested load did not start.");
            Complete(First);
            yield return Until(() => opened > 0, "The screen did not open.");
            for (var i = 0; i < 3; i++) yield return null;

            Assert.That(opened, Is.EqualTo(1), "Reentrant Open during Init created duplicate instances.");
        }

        [UnityTest]
        public IEnumerator PrefetchLeavesSlotForUserAndCanBePromotedWithoutAnotherLoad()
        {
            CreateRoot(CreateScreen(First, true, 10), CreateScreen(Second, true, 5), CreateScreen(Third));
            var openedFirst = 0;
            _root.ScreenOpened += (id, _) => { if (id == First) openedFirst++; };
            yield return Until(() => IsPending(First), "Highest-priority prefetch did not start.");
            Assert.That(IsPending(Second), Is.False, "Prefetch consumed the foreground slot.");

            _root.Open(Third);
            yield return Until(() => IsPending(Third), "User request was blocked by prefetch.");
            _root.Open(First);
            yield return null;
            Assert.That(LoadCount(First), Is.EqualTo(1), "Promotion started another provider request.");
            Assert.That(IsPending(Second), Is.False, "Promotion exceeded the total load limit.");

            Complete(First);
            Complete(Third);
            yield return Until(() => openedFirst == 1 && _root.TryGetScreen(Third, out _),
                "Foreground requests did not activate after their loads completed.");
        }

        [UnityTest]
        public IEnumerator OpenScreensStayPinnedBeyondSoftBudgetAndClosedScreenCanBeTrimmed()
        {
            CreateRoot(CreateScreen(First), CreateScreen(Second));
            SetField(_catalog, "_maxResidentScreens", 1);
            SetField(_catalog, "_memoryBudgetMB", 10f);
            _root.Open(First);
            _root.Open(Second);
            yield return Until(() => IsPending(First) && IsPending(Second), "Foreground loads did not start.");
            Complete(First);
            Complete(Second);
            yield return Until(() => _root.TryGetScreen(First, out _) && _root.TryGetScreen(Second, out _),
                "Open screens were rejected because of the soft cache budget.");

            _root.TrimCache();
            yield return null;
            Assert.That(_root.ResidentScreenCount, Is.EqualTo(2));
            Assert.That(_root.EstimatedResidentMemoryMB, Is.EqualTo(20f).Within(0.01f));
            Assert.That(ReleaseCount(First) + ReleaseCount(Second), Is.Zero);

            _root.Close(First);
            yield return Until(() => !_root.TryGetScreen(First, out _), "Closed screen remained registered.");
            yield return null;
            _root.TrimCache();
            yield return Until(() => ReleaseCount(First) == 1, "Closed prefab was not released.");
            Assert.That(_root.TryGetScreen(Second, out _), Is.True);
            Assert.That(_root.ResidentScreenCount, Is.EqualTo(1));
            Assert.That(_root.EstimatedResidentMemoryMB, Is.EqualTo(10f).Within(0.01f));

            Object.Destroy(_root.gameObject);
            yield return Until(() => ReleaseCount(Second) == 1, "Root destruction leaked an open screen's prefab.");
        }

        [UnityTest]
        public IEnumerator TrimmingPendingPrefetchKeepsItsSlotUntilCompletion()
        {
            CreateRoot(CreateScreen(First, true, 10), CreateScreen(Second), CreateScreen(Third));
            var opened = 0;
            _root.ScreenOpened += (_, __) => opened++;
            yield return Until(() => IsPending(First), "Prefetch did not start.");

            _root.TrimCache();
            Assert.That(_root.ResidentScreenCount, Is.EqualTo(1), "Trim forgot the in-flight reservation.");
            Assert.That(_root.EstimatedResidentMemoryMB, Is.EqualTo(10f).Within(0.01f));
            _root.Open(Second);
            _root.Open(Third);
            yield return Until(() => IsPending(Second), "The reserved foreground slot was unavailable.");
            for (var i = 0; i < 3; i++) yield return null;
            Assert.That(LoadCount(Third), Is.Zero, "An abandoned prefetch still occupies one actual load slot.");
            Assert.That(_root.ResidentScreenCount, Is.EqualTo(2));
            Assert.That(_root.EstimatedResidentMemoryMB, Is.EqualTo(20f).Within(0.01f));

            Complete(First);
            yield return Until(() => ReleaseCount(First) == 1 && IsPending(Third),
                "Completed prefetch was not released or its slot was not reclaimed.");
            Assert.That(opened, Is.Zero, "Discarded prefetch activated a screen.");
            Assert.That(_root.TryGetScreen(First, out _), Is.False);
            Assert.That(_root.ResidentScreenCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator UserCanPromotePrefetchAfterTrimBeforeItCompletes()
        {
            CreateRoot(CreateScreen(First, true));
            var opened = 0;
            _root.ScreenOpened += (_, __) => opened++;
            yield return Until(() => IsPending(First), "Prefetch did not start.");
            _root.TrimCache();
            _root.Open(First);
            Complete(First);

            yield return Until(() => opened == 1, "User demand was discarded with the trimmed prefetch.");
            Assert.That(LoadCount(First), Is.EqualTo(1));
            Assert.That(ReleaseCount(First), Is.Zero, "The promoted screen lost its retained prefab.");
            Assert.That(_root.TryGetScreen(First, out _), Is.True);
        }

        [UnityTest]
        public IEnumerator DestroyingRootDuringLoadReleasesLateResultWithoutOpening()
        {
            CreateRoot(CreateScreen(First));
            var opened = 0;
            _root.ScreenOpened += (_, __) => opened++;
            _root.Open(First);
            yield return Until(() => IsPending(First), "The requested load did not start.");

            Object.Destroy(_root.gameObject);
            yield return null;
            Complete(First);
            yield return Until(() => ReleaseCount(First) == 1, "Late result retained a released owner's handle.");
            Assert.That(opened, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisablingRootReleasesClonesOnlyAfterInstancesAreDestroyed()
        {
            CreateRoot(CreateScreen(First));
            _root.Open(First);
            yield return Until(() => IsPending(First), "The requested load did not start.");
            Complete(First);
            yield return Until(() => _root.TryGetScreen(First, out _), "The loaded screen did not open.");
            _root.TryGetScreen(First, out var instance);
            var releasedAfterDestruction = false;
            _provider.Released += key =>
            {
                if (key == _keys[First]) releasedAfterDestruction = instance == null;
            };

            // Disable invokes disposal immediately; the release is queued on the ResourceManager
            // and must wait for the deferred destruction of the clone.
            _root.enabled = false;
            yield return Until(() => ReleaseCount(First) == 1, "Disabling the root leaked the prefab handle.");
            Assert.That(releasedAfterDestruction, Is.True);
        }

        [UnityTest]
        public IEnumerator FailedLoadNotifiesCallerAndCanBeRetriedAfterCooldown()
        {
            CreateRoot(CreateScreen(First));
            var failures = 0;
            var opened = 0;
            _root.LoadFailed += (id, error) =>
            {
                Assert.That(id, Is.EqualTo(First));
                Assert.That(error, Is.Not.Null);
                failures++;
            };
            _root.ScreenOpened += (_, __) => opened++;
            // This test intentionally fails its provider; production failures still use
            // the original ResourceManager handler, restored by TearDown.
            ResourceManager.ExceptionHandler = (_, __) => { };

            _root.Open(First);
            yield return Until(() => IsPending(First), "The requested load did not start.");
            _provider.Fail(_keys[First]);
            yield return Until(() => failures == 1, "LoadFailed did not notify the requester.");
            Assert.That(_root.ResidentScreenCount, Is.Zero);
            Assert.That(_root.TryGetScreen(First, out _), Is.False);

            _root.Open(First);
            yield return Until(() => failures == 2, "Retry cooldown was not reported.");
            Assert.That(LoadCount(First), Is.EqualTo(1), "Cooldown started another provider request.");
            yield return new WaitForSecondsRealtime(1.05f);
            _root.Open(First);
            yield return Until(() => IsPending(First), "Retry did not start after cooldown.");
            Complete(First);
            yield return Until(() => opened == 1, "Successful retry did not open the screen.");
            Assert.That(LoadCount(First), Is.EqualTo(2));
        }

        private AddressableScreenCatalog.Screen CreateScreen(ScreenId id, bool preload = false, int priority = 5)
        {
            var key = Guid.NewGuid().ToString("N");
            _keys.Add(id, key);
            var prefab = new GameObject("Test screen " + id, typeof(RectTransform), typeof(CanvasGroup));
            prefab.SetActive(false);
            prefab.AddComponent<ScreenFixture>();
            _prefabs.Add(prefab);
            _provider.Add(key, prefab);
            _locator.Add(key, new ResourceLocationBase(key, key, _provider.ProviderId, typeof(GameObject)));

            var screen = new AddressableScreenCatalog.Screen();
            SetField(screen, "_id", id.Value);
            SetField(screen, "_prefab", new AssetReferenceGameObject(key));
            SetField(screen, "_priority", priority);
            SetField(screen, "_estimatedMemoryMB", 10f);
            SetField(screen, "_preload", preload);
            return screen;
        }

        private void CreateRoot(params AddressableScreenCatalog.Screen[] screens)
        {
            _catalog = ScriptableObject.CreateInstance<AddressableScreenCatalog>();
            SetField(_catalog, "_screens", screens);
            SetField(_catalog, "_minimumResidenceSeconds", 0f);
            SetField(_catalog, "_idleDelaySeconds", 0f);
            SetField(_catalog, "_reevaluateSeconds", 0.1f);
            SetField(_catalog, "_failureRetrySeconds", 1f);

            var rootObject = new GameObject("Addressable UI test root", typeof(RectTransform));
            rootObject.SetActive(false);
            _root = rootObject.AddComponent<AddressableUIRoot>();
            SetField(_root, "_catalog", _catalog);
            SetField(_root, "_screenParent", rootObject.transform);
            rootObject.SetActive(true);
        }

        private bool IsPending(ScreenId id) => _provider.IsPending(_keys[id]);
        private int LoadCount(ScreenId id) => _provider.LoadCount(_keys[id]);
        private int ReleaseCount(ScreenId id) => _provider.ReleaseCount(_keys[id]);
        private void Complete(ScreenId id) => _provider.Complete(_keys[id]);

        private static IEnumerator Until(Func<bool> predicate, string failure)
        {
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, failure);
        }

        private void EnsureTrackingCallback(string fieldName, string methodName)
        {
            var type = _addressablesImplementation.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Update the fixture for this Addressables version: " + fieldName);
            Assert.That(method, Is.Not.Null, "Update the fixture for this Addressables version: " + methodName);
            if (field.GetValue(_addressablesImplementation) != null) return;
            field.SetValue(_addressablesImplementation,
                Delegate.CreateDelegate(typeof(Action<AsyncOperationHandle>), _addressablesImplementation, method));
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing test configuration field: " + name);
            field.SetValue(target, value);
        }

        private sealed class MemoryScreenProvider : ResourceProviderBase
        {
            private readonly Dictionary<string, GameObject> _assets = new Dictionary<string, GameObject>();
            private readonly Dictionary<string, ProvideHandle> _pending = new Dictionary<string, ProvideHandle>();
            private readonly Dictionary<string, int> _loads = new Dictionary<string, int>();
            private readonly Dictionary<string, int> _releases = new Dictionary<string, int>();

            public event Action<string> Released;

            public MemoryScreenProvider() => m_ProviderId = "ui-test-provider-" + Guid.NewGuid().ToString("N");
            public void Add(string key, GameObject prefab) => _assets.Add(key, prefab);
            public bool IsPending(string key) => _pending.ContainsKey(key);
            public int LoadCount(string key) => _loads.TryGetValue(key, out var count) ? count : 0;
            public int ReleaseCount(string key) => _releases.TryGetValue(key, out var count) ? count : 0;
            public override Type GetDefaultType(IResourceLocation location) => typeof(GameObject);

            public override void Provide(ProvideHandle handle)
            {
                var key = handle.Location.InternalId;
                _loads[key] = LoadCount(key) + 1;
                _pending.Add(key, handle);
            }

            public void Complete(string key)
            {
                var handle = _pending[key];
                _pending.Remove(key);
                handle.Complete(_assets[key], true, null);
            }

            public void CompleteAll()
            {
                var keys = new List<string>(_pending.Keys);
                foreach (var key in keys) Complete(key);
            }

            public void Fail(string key)
            {
                var handle = _pending[key];
                _pending.Remove(key);
                handle.Complete<GameObject>(null, false, new InvalidOperationException("Expected test load failure."));
            }

            public override void Release(IResourceLocation location, object asset)
            {
                var key = location.InternalId;
                _releases[key] = ReleaseCount(key) + 1;
                Released?.Invoke(key);
            }
        }
    }
}
