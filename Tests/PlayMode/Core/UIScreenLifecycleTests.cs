using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SimpleUIScreensSystem.Tests
{
    public sealed class UIScreenLifecycleTests
    {
        private static readonly FieldInfo AnimationFlag =
            typeof(UIScreen).GetField("_withAnimation", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ModalField =
            typeof(UIScreen).GetField("_modalWindow", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ProfileField =
            typeof(UIScreen).GetField("_transition", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DurationField =
            typeof(ScreenTransitionProfile).GetField("_fadeDuration", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<string> _events = new List<string>();

        [SetUp]
        public void SetUp()
        {
            Assert.That(AnimationFlag, Is.Not.Null, "UIScreen no longer serializes '_withAnimation'; update the test.");
            Assert.That(ModalField, Is.Not.Null, "UIScreen no longer serializes '_modalWindow'; update the test.");
            Assert.That(ProfileField, Is.Not.Null, "UIScreen no longer serializes '_transition'; update the test.");
            Assert.That(DurationField, Is.Not.Null, "ScreenTransitionProfile no longer serializes '_fadeDuration'; update the test.");
            _events.Clear();
            UIMotion.ReduceMotion = false;
        }

        [TearDown]
        public void TearDown()
        {
            UIMotion.ReduceMotion = false;
            foreach (var gameObject in _objects)
                if (gameObject != null) Object.DestroyImmediate(gameObject);
            _objects.Clear();
        }

        [Test]
        public void ReduceMotionMakesAnimatedScreensInstant()
        {
            UIMotion.ReduceMotion = true;
            var screen = CreateScreen(animated: true);

            screen.Open();
            Assert.That(screen.State, Is.EqualTo(ScreenState.Open));
            Assert.That(screen.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));

            screen.Close();
            Assert.That(screen.State, Is.EqualTo(ScreenState.Hidden));
            Assert.That(_events, Is.EqualTo(new[] { "Opening", "Opened", "Closing", "Closed" }));
        }

        [UnityTest]
        public IEnumerator ProfileDurationControlsTheTransition()
        {
            var instant = ScriptableObject.CreateInstance<ScreenTransitionProfile>();
            DurationField.SetValue(instant, 0f);
            var slow = ScriptableObject.CreateInstance<ScreenTransitionProfile>();
            DurationField.SetValue(slow, 10f);
            try
            {
                var screen = CreateScreen(animated: true);
                ProfileField.SetValue(screen, instant);
                screen.Open();
                Assert.That(screen.State, Is.EqualTo(ScreenState.Open), "A zero-length fade completes synchronously.");
                Assert.That(screen.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));

                var slowScreen = CreateScreen(animated: true);
                ProfileField.SetValue(slowScreen, slow);
                slowScreen.Open();
                for (var i = 0; i < 3; i++) yield return null;
                Assert.That(slowScreen.State, Is.EqualTo(ScreenState.Opening));
                Assert.That(slowScreen.GetComponent<CanvasGroup>().alpha, Is.LessThan(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(instant);
                Object.DestroyImmediate(slow);
            }
        }

        [Test]
        public void InstantTransitionsRaiseEventsInOrderAndTrackState()
        {
            var screen = CreateScreen(animated: false);
            Assert.That(screen.State, Is.EqualTo(ScreenState.Hidden));
            Assert.That(screen.IsOpen, Is.False);

            screen.Open();
            Assert.That(screen.State, Is.EqualTo(ScreenState.Open));
            Assert.That(screen.gameObject.activeSelf, Is.True);
            Assert.That(_events, Is.EqualTo(new[] { "Opening", "Opened" }));

            screen.Open();
            Assert.That(_events.Count, Is.EqualTo(2), "Opening an open screen must be a no-op.");

            screen.Close();
            Assert.That(screen.State, Is.EqualTo(ScreenState.Hidden));
            Assert.That(screen.gameObject.activeSelf, Is.False);
            Assert.That(_events, Is.EqualTo(new[] { "Opening", "Opened", "Closing", "Closed" }));

            screen.Close();
            Assert.That(_events.Count, Is.EqualTo(4), "Closing a hidden screen must be a no-op.");
        }

        [UnityTest]
        public IEnumerator AnimatedOpenPassesThroughOpeningAndEndsFullyVisible()
        {
            var screen = CreateScreen(animated: true, withModal: true);
            var group = screen.GetComponent<CanvasGroup>();
            var modal = (Transform)ModalField.GetValue(screen);

            screen.Open();
            Assert.That(screen.State, Is.EqualTo(ScreenState.Opening));
            Assert.That(group.alpha, Is.LessThan(1f));
            Assert.That(_events, Is.EqualTo(new[] { "Opening" }));

            yield return Until(() => screen.State == ScreenState.Open, "The opening transition never finished.");
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(modal.localScale, Is.EqualTo(Vector3.one));
            Assert.That(_events, Is.EqualTo(new[] { "Opening", "Opened" }));

            screen.Close();
            Assert.That(screen.State, Is.EqualTo(ScreenState.Closing));
            Assert.That(screen.IsClosing, Is.True);
            yield return Until(() => screen.State == ScreenState.Hidden, "The closing transition never finished.");
            Assert.That(screen.gameObject.activeSelf, Is.False);
            Assert.That(group.alpha, Is.EqualTo(0f));
            Assert.That(_events, Is.EqualTo(new[] { "Opening", "Opened", "Closing", "Closed" }));
        }

        [UnityTest]
        public IEnumerator CloseDuringOpeningReversesWithoutRaisingOpened()
        {
            var screen = CreateScreen(animated: true);
            var group = screen.GetComponent<CanvasGroup>();

            screen.Open();
            yield return null;
            yield return null;
            var alphaAtInterruption = group.alpha;
            Assert.That(screen.State, Is.EqualTo(ScreenState.Opening));

            screen.Close();
            Assert.That(screen.State, Is.Not.EqualTo(ScreenState.Open));
            yield return null;
            Assert.That(group.alpha, Is.LessThanOrEqualTo(alphaAtInterruption), "Closing must continue from the interrupted alpha.");

            yield return Until(() => screen.State == ScreenState.Hidden, "The reversed transition never finished.");
            Assert.That(_events, Is.EqualTo(new[] { "Opening", "Closing", "Closed" }));
        }

        [UnityTest]
        public IEnumerator OpenDuringClosingReopensWithoutDeactivating()
        {
            var screen = CreateScreen(animated: true);
            var group = screen.GetComponent<CanvasGroup>();
            var deactivations = 0;
            screen.OnClosed.AddListener(() => deactivations++);

            screen.Open();
            yield return Until(() => screen.State == ScreenState.Open, "The opening transition never finished.");
            screen.Close();
            yield return null;
            Assert.That(screen.State, Is.EqualTo(ScreenState.Closing));

            screen.Open();
            Assert.That(screen.State, Is.EqualTo(ScreenState.Opening));
            yield return Until(() => screen.State == ScreenState.Open, "The reopening transition never finished.");
            Assert.That(screen.gameObject.activeSelf, Is.True);
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(deactivations, Is.Zero);
            Assert.That(_events, Is.EqualTo(new[] { "Opening", "Opened", "Closing", "Opening", "Opened" }));
        }

        [Test]
        public void ExternalActivationAndDeactivationKeepStateAndEventsConsistent()
        {
            var screen = CreateScreen(animated: true);
            var group = screen.GetComponent<CanvasGroup>();
            group.alpha = 0f;

            screen.gameObject.SetActive(true);
            Assert.That(screen.State, Is.EqualTo(ScreenState.Open));
            Assert.That(group.alpha, Is.EqualTo(1f), "An external activation shows the screen instantly.");
            Assert.That(_events, Is.EqualTo(new[] { "Opening", "Opened" }));

            screen.gameObject.SetActive(false);
            Assert.That(screen.State, Is.EqualTo(ScreenState.Hidden));
            Assert.That(_events, Is.EqualTo(new[] { "Opening", "Opened", "Closing", "Closed" }));
        }

        [UnityTest]
        public IEnumerator HideCutsARunningCloseShort()
        {
            var screen = CreateScreen(animated: true);
            screen.Open();
            yield return Until(() => screen.State == ScreenState.Open, "The opening transition never finished.");

            screen.Close();
            Assert.That(screen.State, Is.EqualTo(ScreenState.Closing));
            screen.Hide();
            Assert.That(screen.State, Is.EqualTo(ScreenState.Hidden));
            Assert.That(screen.gameObject.activeSelf, Is.False);
            Assert.That(_events, Is.EqualTo(new[] { "Opening", "Opened", "Closing", "Closed" }));

            screen.Open();
            screen.Hide();
            Assert.That(screen.State, Is.EqualTo(ScreenState.Hidden));
            Assert.That(_events, Is.EqualTo(new[]
            {
                "Opening", "Opened", "Closing", "Closed", "Opening", "Closing", "Closed"
            }), "Hiding during an opening transition raises Closing and Closed once and never Opened.");
        }

        [Test]
        public void ListenerClosingFromOpeningCancelsTheOpen()
        {
            var screen = CreateScreen(animated: false);
            screen.OnOpening.AddListener(screen.Close);

            screen.Open();
            Assert.That(screen.State, Is.EqualTo(ScreenState.Hidden));
            Assert.That(screen.gameObject.activeSelf, Is.False);
            Assert.That(_events, Is.EqualTo(new[] { "Opening", "Closing", "Closed" }));
        }

        [Test]
        public void HideDeactivatesAScreenThatStartedActive()
        {
            // Mirrors UIInitializer hiding screens that are active in the scene at startup.
            var gameObject = new GameObject("Startup screen");
            _objects.Add(gameObject);
            var screen = gameObject.AddComponent<UIScreen>();
            screen.Init();
            screen.Hide();
            Assert.That(gameObject.activeSelf, Is.False);
            Assert.That(screen.State, Is.EqualTo(ScreenState.Hidden));
        }

        private UIScreen CreateScreen(bool animated, bool withModal = false)
        {
            var gameObject = new GameObject("Screen", typeof(RectTransform));
            gameObject.SetActive(false);
            _objects.Add(gameObject);
            var screen = gameObject.AddComponent<UIScreen>();
            AnimationFlag.SetValue(screen, animated);
            if (withModal)
            {
                var modal = new GameObject("Modal", typeof(RectTransform));
                modal.transform.SetParent(gameObject.transform, false);
                ModalField.SetValue(screen, modal.transform);
            }

            screen.OnOpening.AddListener(() => _events.Add("Opening"));
            screen.OnOpened.AddListener(() => _events.Add("Opened"));
            screen.OnClosing.AddListener(() => _events.Add("Closing"));
            screen.OnClosed.AddListener(() => _events.Add("Closed"));
            return screen;
        }

        private static IEnumerator Until(System.Func<bool> predicate, string failure)
        {
            var deadline = Time.realtimeSinceStartup + 2f;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, failure);
        }
    }
}
