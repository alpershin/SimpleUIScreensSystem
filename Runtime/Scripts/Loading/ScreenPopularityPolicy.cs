using System;
using System.Collections.Generic;

namespace SimpleUIScreensSystem
{
    /// <summary>
    /// Ranks registered screens using designer priority and recent successful navigation.
    /// Feed events from one thread using a monotonic, unscaled clock. Statistics stay in memory.
    /// </summary>
    public sealed class ScreenPopularityPolicy
    {
        private const int MaxActionContexts = 32;
        private const double PriorityWeight = 0.1;
        private const double TransitionWeight = 2;
        private const double ActionWeight = 3;
        private const double ConfidencePrior = 3;

        private readonly double _halfLifeSeconds;
        private readonly double _contextLifetimeSeconds;
        private readonly Dictionary<ScreenId, ScreenStatistics> _screens =
            new Dictionary<ScreenId, ScreenStatistics>();
        private readonly Dictionary<string, ActionStatistics> _actions =
            new Dictionary<string, ActionStatistics>(StringComparer.Ordinal);
        private readonly LinkedList<string> _actionRecency = new LinkedList<string>();

        private ScreenStatistics _previousScreen;
        private ActionStatistics _lastAction;
        private double _lastActionTime;
        private double _latestEventTime = double.NegativeInfinity;

        public ScreenPopularityPolicy(double halfLifeSeconds = 300, double contextLifetimeSeconds = 30)
        {
            ValidatePositiveFinite(halfLifeSeconds, nameof(halfLifeSeconds));
            ValidatePositiveFinite(contextLifetimeSeconds, nameof(contextLifetimeSeconds));
            _halfLifeSeconds = halfLifeSeconds;
            _contextLifetimeSeconds = contextLifetimeSeconds;
        }

        /// <summary>Registers a stable ID or updates its priority without discarding learned data.</summary>
        public void Register(ScreenId screenId, int priority)
        {
            ValidateId(screenId, nameof(screenId));
            if (priority < 0 || priority > 10)
                throw new ArgumentOutOfRangeException(nameof(priority), "Priority must be between 0 and 10.");

            if (!_screens.TryGetValue(screenId, out var screen))
            {
                screen = new ScreenStatistics();
                _screens.Add(screenId, screen);
            }

            screen.Priority = priority;
        }

        /// <summary>
        /// Records one successful opening. Failed loads, preloads and duplicate open requests
        /// must not call this method. The most recent action predicts only the next opening.
        /// </summary>
        public void RecordOpen(ScreenId screenId, double now)
        {
            var screen = GetScreen(screenId);
            now = RecordTime(now);
            screen.Opens.Increment(now, _halfLifeSeconds);

            if (_previousScreen != null && _previousScreen != screen)
                _previousScreen.NextScreens.Record(screenId, now, _halfLifeSeconds);

            if (HasRecentAction(now))
                _lastAction.NextScreens.Record(screenId, now, _halfLifeSeconds);

            _previousScreen = screen;
            _lastAction = null;
        }

        /// <summary>
        /// Records a semantic action, for example "shop_tab_selected". Only the 32 most recently
        /// used distinct action IDs retain statistics; avoid IDs containing dynamic user data.
        /// </summary>
        public void RecordAction(string actionId, double now)
        {
            ValidateId(actionId, nameof(actionId));
            now = RecordTime(now);

            if (_actions.TryGetValue(actionId, out var action))
            {
                _actionRecency.Remove(action.RecencyNode);
                _actionRecency.AddFirst(action.RecencyNode);
            }
            else
            {
                if (_actions.Count == MaxActionContexts)
                {
                    _actions.Remove(_actionRecency.Last.Value);
                    _actionRecency.RemoveLast();
                }

                action = new ActionStatistics(_actionRecency.AddFirst(actionId));
                _actions.Add(actionId, action);
            }

            _lastAction = action;
            _lastActionTime = now;
        }

        /// <summary>Returns a nonnegative ranking score without changing statistics or allocating.</summary>
        public double GetScore(ScreenId screenId, double now)
        {
            var screen = GetScreen(screenId);
            ValidateTime(now);
            now = Math.Max(now, _latestEventTime);

            // Every count decays by 2^(-elapsed / halfLife). Priority supplies a cold-start baseline;
            // log frequency prevents old habits from dominating indefinitely. Context contributes
            // weight * target / (total + 3), equivalent to conditional probability multiplied by
            // total / (total + 3) confidence. Decay therefore reduces both popularity and confidence.
            var score = PriorityWeight * screen.Priority + Math.Log(1 + screen.Opens.At(now, _halfLifeSeconds));
            if (_previousScreen != null)
                score += TransitionWeight * _previousScreen.NextScreens.GetConfidence(screenId, now, _halfLifeSeconds);
            if (HasRecentAction(now))
                score += ActionWeight * _lastAction.NextScreens.GetConfidence(screenId, now, _halfLifeSeconds);
            return score;
        }

        /// <summary>Clears navigation/action context while preserving learned popularity and transitions.</summary>
        public void ResetSessionContext()
        {
            _previousScreen = null;
            _lastAction = null;
        }

        private bool HasRecentAction(double now)
        {
            return _lastAction != null && now - _lastActionTime <= _contextLifetimeSeconds;
        }

        private ScreenStatistics GetScreen(ScreenId screenId)
        {
            ValidateId(screenId, nameof(screenId));
            if (!_screens.TryGetValue(screenId, out var screen))
                throw new KeyNotFoundException($"Screen '{screenId}' is not registered.");
            return screen;
        }

        private double RecordTime(double now)
        {
            ValidateTime(now);
            // Clamp late events so a backwards clock cannot increase decayed counts artificially.
            _latestEventTime = Math.Max(now, _latestEventTime);
            return _latestEventTime;
        }

        private static void ValidateId(string id, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A nonempty stable ID is required.", parameterName);
        }

        private static void ValidateId(ScreenId id, string parameterName)
        {
            if (!id.IsValid)
                throw new ArgumentException("A valid screen ID is required.", parameterName);
        }

        private static void ValidateTime(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now))
                throw new ArgumentOutOfRangeException(nameof(now), "Time must be finite.");
        }

        private static void ValidatePositiveFinite(double value, string parameterName)
        {
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName, "The duration must be finite and positive.");
        }

        private sealed class ScreenStatistics
        {
            public int Priority;
            public readonly DecayingCount Opens = new DecayingCount();
            public readonly TransitionStatistics NextScreens = new TransitionStatistics();
        }

        private sealed class ActionStatistics
        {
            public readonly LinkedListNode<string> RecencyNode;
            public readonly TransitionStatistics NextScreens = new TransitionStatistics();

            public ActionStatistics(LinkedListNode<string> recencyNode)
            {
                RecencyNode = recencyNode;
            }
        }

        private sealed class TransitionStatistics
        {
            private readonly Dictionary<ScreenId, DecayingCount> _targets =
                new Dictionary<ScreenId, DecayingCount>();
            private readonly DecayingCount _total = new DecayingCount();

            public void Record(ScreenId screenId, double now, double halfLifeSeconds)
            {
                if (!_targets.TryGetValue(screenId, out var count))
                {
                    count = new DecayingCount();
                    _targets.Add(screenId, count);
                }

                count.Increment(now, halfLifeSeconds);
                _total.Increment(now, halfLifeSeconds);
            }

            public double GetConfidence(ScreenId screenId, double now, double halfLifeSeconds)
            {
                if (!_targets.TryGetValue(screenId, out var count)) return 0;
                return count.At(now, halfLifeSeconds) / (_total.At(now, halfLifeSeconds) + ConfidencePrior);
            }
        }

        private sealed class DecayingCount
        {
            private double _value;
            private double _time;

            public double At(double now, double halfLifeSeconds)
            {
                if (_value == 0 || now <= _time) return _value;
                return _value * Math.Exp(-Math.Log(2) * ((now - _time) / halfLifeSeconds));
            }

            public void Increment(double now, double halfLifeSeconds)
            {
                _value = At(now, halfLifeSeconds) + 1;
                _time = now;
            }
        }
    }
}
