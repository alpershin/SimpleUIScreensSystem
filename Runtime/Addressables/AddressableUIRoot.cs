using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SimpleUIScreensSystem.AddressableUI
{
    /// <summary>Scene-owned Addressables cache. All API calls must run on Unity's main thread.</summary>
    public sealed class AddressableUIRoot : MonoBehaviour
    {
        private enum LoadState { Unloaded, Loading, Ready }

        private sealed class Entry
        {
            public AddressableScreenCatalog.Screen Definition;
            public AsyncOperationHandle<GameObject> Handle;
            public LoadState State;
            public UIScreen Instance;
            public UnityAction ClosedListener;
            public bool Requested;
            public bool StartedInBackground;
            public bool DiscardOnCompletion;
            public long RequestOrder;
            public double LoadedAt;
            public double RetryAfter;
            public int DestroyedAtFrame = -1;
        }

        [SerializeField] private AddressableScreenCatalog _catalog;
        [SerializeField] private Transform _screenParent;

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Dictionary<ScreenId, Entry> _byId = new Dictionary<ScreenId, Entry>();
        private readonly List<Entry> _victims = new List<Entry>();
        private ScreenPopularityPolicy _popularity;
        private Transform _staging;
        private double _nextEvaluation;
        private double _lastActionAt;
        private long _requestOrder;
        private bool _initialized;
        private bool _quitting;

        /// <summary>Fires after activation. The visual opening animation may still be running.</summary>
        public event Action<ScreenId, UIScreen> ScreenOpened;
        public event Action<ScreenId, Exception> LoadFailed;

        public AddressableScreenCatalog Catalog => _catalog;

        public int ResidentScreenCount
        {
            get
            {
                var count = 0;
                foreach (var entry in _entries)
                    if (entry.State != LoadState.Unloaded) count++;
                return count;
            }
        }

        /// <summary>Catalog estimates, including in-flight reservations; not measured process RAM.</summary>
        public float EstimatedResidentMemoryMB
        {
            get
            {
                var total = 0f;
                foreach (var entry in _entries)
                    if (entry.State != LoadState.Unloaded) total += entry.Definition.EstimatedMemoryMB;
                return total;
            }
        }

        private static double Now => Time.realtimeSinceStartupAsDouble;

        private void OnEnable()
        {
            if (_catalog == null) return;
            _catalog.Validate();
            if (_popularity == null)
            {
                _popularity = new ScreenPopularityPolicy(_catalog.PopularityHalfLifeSeconds, _catalog.ActionContextSeconds);
                foreach (var definition in _catalog.Screens) _popularity.Register(definition.Id, definition.Priority);
            }
            _popularity.ResetSessionContext();
            foreach (var definition in _catalog.Screens)
            {
                var entry = new Entry { Definition = definition };
                entry.ClosedListener = () => OnScreenClosed(entry);
                _entries.Add(entry);
                _byId.Add(definition.Id, entry);
            }
            var stagingObject = new GameObject("Inactive screen staging");
            stagingObject.SetActive(false);
            stagingObject.transform.SetParent(transform, false);
            _staging = stagingObject.transform;
            _lastActionAt = Now;
            _nextEvaluation = Now;
            _initialized = true;
            Application.lowMemory += OnLowMemory;
        }

        public void Open(ScreenId screenId)
        {
            var entry = GetEntry(screenId);
            _lastActionAt = Now;
            if (entry.Requested || (entry.Instance != null && entry.Instance.IsOpen && !entry.Instance.IsClosing)) return;
            entry.Requested = true;
            entry.DiscardOnCompletion = false;
            entry.RequestOrder = ++_requestOrder;
        }

        /// <summary>Also cancels a queued/pending open. Shared Addressables I/O is not forcibly cancelled.</summary>
        public void Close(ScreenId screenId)
        {
            var entry = GetEntry(screenId);
            _lastActionAt = Now;
            entry.Requested = false;
            if (entry.Instance != null) entry.Instance.Close();
        }

        /// <summary>Use stable action names, e.g. "inventory.selected"; never item IDs or user text.</summary>
        public void ReportAction(string actionId)
        {
            RequireInitialized();
            _lastActionAt = Now;
            _popularity.RecordAction(actionId, _lastActionAt);
        }

        public bool TryGetScreen(ScreenId screenId, out UIScreen screen)
        {
            screen = null;
            if (!screenId.IsValid || !_byId.TryGetValue(screenId, out var entry)) return false;
            screen = entry.Instance;
            return screen != null;
        }

        /// <summary>Evicts idle assets and pauses prefetch for the configured minimum residence period.</summary>
        public void TrimCache()
        {
            RequireInitialized();
            foreach (var entry in _entries)
            {
                if (entry.Requested || entry.Instance != null) continue;
                entry.DiscardOnCompletion = true;
                if (entry.State == LoadState.Ready && CanEvict(entry)) Release(entry);
            }
            _nextEvaluation = Now + Math.Max(_catalog.MinimumResidenceSeconds, _catalog.ReevaluateSeconds);
        }

        private Entry GetEntry(ScreenId screenId)
        {
            RequireInitialized();
            if (!screenId.IsValid || !_byId.TryGetValue(screenId, out var entry))
                throw new ArgumentException($"Unknown UI screen '{screenId}'.", nameof(screenId));
            return entry;
        }

        private void RequireInitialized()
        {
            if (!_initialized) throw new InvalidOperationException("Enable AddressableUIRoot with a valid catalog first.");
        }

        private void Update()
        {
            if (!_initialized) return;
            var now = Now;
            PollLoads(now);
            if (!_initialized) return;
            OpenOneReadyScreen(now);
            if (!_initialized) return;
            EnforceBudget();
            StartDemandLoads(now);
            if (!_initialized || now < _nextEvaluation || now - _lastActionAt < _catalog.IdleDelaySeconds) return;
            _nextEvaluation = now + _catalog.ReevaluateSeconds;
            StartBackgroundLoads(now);
        }

        private void PollLoads(double now)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.State == LoadState.Ready && entry.DiscardOnCompletion && CanEvict(entry))
                {
                    Release(entry);
                    continue;
                }
                if (entry.State != LoadState.Loading || !entry.Handle.IsDone) continue;
                if (entry.Handle.Status != AsyncOperationStatus.Succeeded || entry.Handle.Result == null)
                {
                    Fail(entry, entry.Handle.OperationException ?? new InvalidOperationException("Addressable prefab is null."), now);
                    continue;
                }
                if (entry.Handle.Result.GetComponent<UIScreen>() == null)
                {
                    Fail(entry, new InvalidOperationException($"Prefab '{entry.Definition.Id}' needs UIScreen on its root."), now);
                    continue;
                }
                entry.State = LoadState.Ready;
                entry.LoadedAt = now;
                if (entry.DiscardOnCompletion && !entry.Requested) Release(entry);
            }
        }

        private void OpenOneReadyScreen(double now)
        {
            Entry next = null;
            foreach (var entry in _entries)
                if (entry.Requested && entry.Instance == null && entry.State == LoadState.Ready &&
                    (next == null || entry.RequestOrder < next.RequestOrder)) next = entry;
            if (next == null) return;
            if (now < next.RetryAfter)
            {
                next.Requested = false;
                NotifyFailure(next.Definition.Id, new InvalidOperationException("Screen load is in retry cooldown."));
                return;
            }
            GameObject instance = null;
            try
            {
                // Inactive staging prevents OnEnable before Init and listener attachment.
                instance = Instantiate(next.Handle.Result, _staging, false);
                instance.SetActive(false);
                instance.transform.SetParent(_screenParent != null ? _screenParent : transform, false);
                if (!instance.transform.parent.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("The screen parent must be active before opening a screen.");
                next.Instance = instance.GetComponent<UIScreen>();
                next.Instance.Init();
                if (!_initialized || !next.Requested)
                {
                    Destroy(instance);
                    next.Instance = null;
                    next.DestroyedAtFrame = Time.frameCount;
                    return;
                }
                next.Requested = false;
                next.Instance.OnClosed.AddListener(next.ClosedListener);
                next.Instance.Open();
                if (!_initialized || next.Instance == null || !next.Instance.IsOpen) return;
                _popularity.RecordOpen(next.Definition.Id, now);
            }
            catch (Exception error)
            {
                next.Requested = false;
                if (next.Instance != null) next.Instance.OnClosed.RemoveListener(next.ClosedListener);
                if (instance != null) Destroy(instance);
                next.Instance = null;
                next.DestroyedAtFrame = Time.frameCount;
                next.RetryAfter = now + _catalog.FailureRetrySeconds;
                NotifyFailure(next.Definition.Id, error);
                return;
            }
            try { ScreenOpened?.Invoke(next.Definition.Id, next.Instance); }
            catch (Exception error) { Debug.LogException(error, this); }
        }

        private void OnScreenClosed(Entry entry)
        {
            if (!_initialized || !isActiveAndEnabled) return;
            if (entry.Instance != null)
            {
                entry.Instance.OnClosed.RemoveListener(entry.ClosedListener);
                Destroy(entry.Instance.gameObject);
                entry.Instance = null;
                entry.DestroyedAtFrame = Time.frameCount;
            }
        }

        private void StartDemandLoads(double now)
        {
            // Subscribers may enqueue again from LoadFailed; bound work to this tick's catalog size.
            var attemptsLeft = _entries.Count;
            while (_initialized && attemptsLeft-- > 0 && CountLoads() < _catalog.MaxConcurrentLoads)
            {
                Entry next = null;
                foreach (var entry in _entries)
                    if (entry.Requested && entry.State == LoadState.Unloaded &&
                        (next == null || entry.RequestOrder < next.RequestOrder)) next = entry;
                if (next == null) return;
                if (now < next.RetryAfter)
                {
                    next.Requested = false;
                    NotifyFailure(next.Definition.Id, new InvalidOperationException("Screen load is in retry cooldown."));
                    continue;
                }
                // User demand may exceed the soft budget. Idle assets are reclaimed on the next tick.
                StartLoad(next);
            }
        }

        private void StartBackgroundLoads(double now)
        {
            foreach (var entry in _entries)
                if (entry.Requested) return;
            while (CountLoads() < _catalog.MaxConcurrentLoads - 1 &&
                   CountBackgroundLoads() < _catalog.MaxBackgroundLoads)
            {
                Entry next = null;
                var bestScore = double.NegativeInfinity;
                foreach (var entry in _entries)
                {
                    if (entry.State != LoadState.Unloaded || !entry.Definition.Preload || now < entry.RetryAfter ||
                        entry.Definition.EstimatedMemoryMB > _catalog.MemoryBudgetMB) continue;
                    var score = _popularity.GetScore(entry.Definition.Id, now);
                    if (score <= bestScore || !PlanRoom(entry, score, now)) continue;
                    next = entry;
                    bestScore = score;
                }
                if (next == null || !PlanRoom(next, bestScore, now)) return;
                foreach (var victim in _victims) Release(victim);
                StartLoad(next);
                next.StartedInBackground = true;
            }
        }

        private bool PlanRoom(Entry candidate, double score, double now)
        {
            _victims.Clear();
            if (_catalog.MaxResidentScreens == 0 || _catalog.MemoryBudgetMB <= 0) return false;
            var count = ResidentScreenCount + 1;
            var memory = EstimatedResidentMemoryMB + candidate.Definition.EstimatedMemoryMB;
            while (count > _catalog.MaxResidentScreens || memory > _catalog.MemoryBudgetMB)
            {
                Entry lowest = null;
                var lowestScore = double.PositiveInfinity;
                foreach (var entry in _entries)
                {
                    if (!CanEvict(entry) || entry.State != LoadState.Ready || _victims.Contains(entry) ||
                        now - entry.LoadedAt < _catalog.MinimumResidenceSeconds) continue;
                    var value = _popularity.GetScore(entry.Definition.Id, now);
                    if (value + _catalog.ReplacementScoreMargin >= score || value >= lowestScore) continue;
                    lowest = entry;
                    lowestScore = value;
                }
                if (lowest == null) return false;
                _victims.Add(lowest);
                count--;
                memory -= lowest.Definition.EstimatedMemoryMB;
            }
            return true;
        }

        private void EnforceBudget()
        {
            while (ResidentScreenCount > _catalog.MaxResidentScreens || EstimatedResidentMemoryMB > _catalog.MemoryBudgetMB)
            {
                Entry lowest = null;
                var lowestScore = double.PositiveInfinity;
                foreach (var entry in _entries)
                {
                    if (!CanEvict(entry) || entry.State != LoadState.Ready) continue;
                    var score = _popularity.GetScore(entry.Definition.Id, Now);
                    if (score >= lowestScore) continue;
                    lowest = entry;
                    lowestScore = score;
                }
                if (lowest == null) return;
                Release(lowest);
            }
        }

        private static bool CanEvict(Entry entry) => entry.State != LoadState.Unloaded &&
            !entry.Requested && entry.Instance == null && Time.frameCount > entry.DestroyedAtFrame;

        private int CountLoads()
        {
            var count = 0;
            foreach (var entry in _entries) if (entry.State == LoadState.Loading) count++;
            return count;
        }

        private int CountBackgroundLoads()
        {
            var count = 0;
            foreach (var entry in _entries)
                if (entry.State == LoadState.Loading && entry.StartedInBackground) count++;
            return count;
        }

        private void StartLoad(Entry entry)
        {
            try
            {
                entry.Handle = Addressables.LoadAssetAsync<GameObject>(entry.Definition.Prefab.RuntimeKey);
                entry.State = LoadState.Loading;
                entry.StartedInBackground = false;
                entry.DiscardOnCompletion = false;
            }
            catch (Exception error) { Fail(entry, error, Now); }
        }

        private void Fail(Entry entry, Exception error, double now)
        {
            entry.Requested = false;
            Release(entry);
            entry.RetryAfter = now + _catalog.FailureRetrySeconds;
            NotifyFailure(entry.Definition.Id, error);
        }

        private void NotifyFailure(ScreenId id, Exception error)
        {
            if (LoadFailed == null) Debug.LogWarning($"UI screen '{id}' failed: {error.Message}", this);
            try { LoadFailed?.Invoke(id, error); }
            catch (Exception listenerError) { Debug.LogException(listenerError, this); }
        }

        private static void Release(Entry entry)
        {
            if (entry.Handle.IsValid()) Addressables.Release(entry.Handle);
            entry.Handle = default;
            entry.State = LoadState.Unloaded;
        }

        private void OnLowMemory() => TrimCache();
        private void OnApplicationQuit() => _quitting = true;
        private void OnDisable() => DisposeEntries();
        private void OnDestroy() => DisposeEntries();

        private void DisposeEntries()
        {
            if (!_initialized) return;
            _initialized = false;
            Application.lowMemory -= OnLowMemory;
            var handles = new List<AsyncOperationHandle<GameObject>>(_entries.Count);
            foreach (var entry in _entries)
            {
                if (entry.Instance != null)
                {
                    entry.Instance.OnClosed.RemoveListener(entry.ClosedListener);
                    Destroy(entry.Instance.gameObject);
                }
                if (entry.Handle.IsValid()) handles.Add(entry.Handle);
                entry.Handle = default;
            }
            _entries.Clear();
            _byId.Clear();
            _victims.Clear();
            if (_staging != null) Destroy(_staging.gameObject);
            // Destroy is deferred. ResourceManager owns the releases until the next frame.
            if (handles.Count == 0) return;
            if (_quitting || !Application.isPlaying)
            {
                foreach (var handle in handles) Addressables.Release(handle);
            }
            else new DeferredScreenRelease(handles);
        }
    }
}
