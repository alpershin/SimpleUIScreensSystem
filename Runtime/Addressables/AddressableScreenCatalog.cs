using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace SimpleUIScreensSystem.AddressableUI
{
    [CreateAssetMenu(menuName = "Simple UI/Addressable Screen Catalog")]
    public sealed class AddressableScreenCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Screen
        {
            [SerializeField, HideInInspector] private string _id;
            [SerializeField] private string _codeName;
            [SerializeField] private AssetReferenceGameObject _prefab;
            [SerializeField, Range(0, 10)] private int _priority = 5;
            [SerializeField, Min(0.1f)] private float _estimatedMemoryMB = 16;
            [SerializeField] private bool _preload = true;

            public ScreenId Id => ScreenId.FromSerialized(_id);
            public string CodeName => _codeName;
            public AssetReferenceGameObject Prefab => _prefab;
            public int Priority => _priority;
            public float EstimatedMemoryMB => _estimatedMemoryMB;
            public bool Preload => _preload;

#if UNITY_EDITOR
            internal void EnsureId()
            {
                if (string.IsNullOrWhiteSpace(_id)) _id = Guid.NewGuid().ToString("N");
            }
#endif
        }

        [SerializeField] private Screen[] _screens = Array.Empty<Screen>();
        [SerializeField] private string _generatedNamespace = "Game.UI";
        [SerializeField] private string _generatedClassName = "Screens";
        [SerializeField, Min(0)] private int _maxResidentScreens = 4;
        [SerializeField, Min(0)] private float _memoryBudgetMB = 96;
        [SerializeField, Min(2)] private int _maxConcurrentLoads = 2;
        [SerializeField, Min(1)] private int _maxBackgroundLoads = 1;
        [SerializeField, Min(0.1f)] private float _reevaluateSeconds = 2;
        [SerializeField, Min(0)] private float _idleDelaySeconds = 0.5f;
        [SerializeField, Min(0)] private float _minimumResidenceSeconds = 15;
        [SerializeField, Min(0)] private float _replacementScoreMargin = 0.25f;
        [SerializeField, Min(1)] private float _popularityHalfLifeSeconds = 300;
        [SerializeField, Min(0.1f)] private float _actionContextSeconds = 30;
        [SerializeField, Min(1)] private float _failureRetrySeconds = 30;

        public IReadOnlyList<Screen> Screens => _screens;
        public string GeneratedNamespace => _generatedNamespace;
        public string GeneratedClassName => _generatedClassName;
        public int MaxResidentScreens => _maxResidentScreens;
        public float MemoryBudgetMB => _memoryBudgetMB;
        public int MaxConcurrentLoads => _maxConcurrentLoads;
        public int MaxBackgroundLoads => _maxBackgroundLoads;
        public float ReevaluateSeconds => _reevaluateSeconds;
        public float IdleDelaySeconds => _idleDelaySeconds;
        public float MinimumResidenceSeconds => _minimumResidenceSeconds;
        public float ReplacementScoreMargin => _replacementScoreMargin;
        public float PopularityHalfLifeSeconds => _popularityHalfLifeSeconds;
        public float ActionContextSeconds => _actionContextSeconds;
        public float FailureRetrySeconds => _failureRetrySeconds;

        public void Validate()
        {
            if (_screens == null) throw new InvalidOperationException("Screen catalog is missing its entries.");
            var ids = new HashSet<ScreenId>();
            var assets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var screen in _screens)
            {
                if (screen == null || !screen.Id.IsValid || !ids.Add(screen.Id))
                    throw new InvalidOperationException("Screen IDs must be nonempty and unique.");
                if (screen.Prefab == null || !screen.Prefab.RuntimeKeyIsValid() || !assets.Add(screen.Prefab.AssetGUID))
                    throw new InvalidOperationException($"Screen '{screen.Id}' needs a unique Addressable prefab.");
                if (screen.Priority < 0 || screen.Priority > 10)
                    throw new InvalidOperationException($"Screen '{screen.Id}' priority must be between 0 and 10.");
                RequireNumber(screen.EstimatedMemoryMB, 0.1f, screen.Id + " estimated memory");
            }

            if (_maxResidentScreens < 0 || _maxConcurrentLoads < 2 || _maxBackgroundLoads < 1 ||
                _maxBackgroundLoads >= _maxConcurrentLoads)
                throw new InvalidOperationException("Keep at least one load slot reserved for user requests.");
            RequireNumber(_memoryBudgetMB, 0, nameof(_memoryBudgetMB));
            RequireNumber(_reevaluateSeconds, 0.1f, nameof(_reevaluateSeconds));
            RequireNumber(_idleDelaySeconds, 0, nameof(_idleDelaySeconds));
            RequireNumber(_minimumResidenceSeconds, 0, nameof(_minimumResidenceSeconds));
            RequireNumber(_replacementScoreMargin, 0, nameof(_replacementScoreMargin));
            RequireNumber(_popularityHalfLifeSeconds, 1, nameof(_popularityHalfLifeSeconds));
            RequireNumber(_actionContextSeconds, 0.1f, nameof(_actionContextSeconds));
            RequireNumber(_failureRetrySeconds, 1, nameof(_failureRetrySeconds));
        }

#if UNITY_EDITOR
        private void OnValidate() => EnsureScreenIds();

        /// <summary>Fills only missing IDs. Existing IDs survive renames and prefab replacement.</summary>
        public void EnsureScreenIds()
        {
            if (_screens == null) return;
            foreach (var screen in _screens) screen?.EnsureId();
        }
#endif

        private static void RequireNumber(float value, float minimum, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum)
                throw new InvalidOperationException($"Invalid {name}: {value}.");
        }
    }
}
