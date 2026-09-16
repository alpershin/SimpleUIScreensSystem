using NUnit.Framework;
using UnityEngine;

namespace SimpleUIScreensSystem.Tests
{
    public sealed class ScreenTransitionProfileTests
    {
        [Test]
        public void DefaultProfileIsSharedAndHasSaneValues()
        {
            var first = ScreenTransitionProfile.Default;
            Assert.That(first, Is.SameAs(ScreenTransitionProfile.Default));
            Assert.That(first.FadeDuration, Is.GreaterThan(0f));
            Assert.That(first.ModalHiddenScale, Is.InRange(0f, 1f));
        }

        [Test]
        public void CurvesAreClampedToUnitRangeAndEndpointsAreExact()
        {
            var profile = ScriptableObject.CreateInstance<ScreenTransitionProfile>();
            try
            {
                Assert.That(profile.EvaluateFade(0f), Is.EqualTo(0f).Within(1e-5f));
                Assert.That(profile.EvaluateFade(1f), Is.EqualTo(1f).Within(1e-5f));
                Assert.That(profile.EvaluateScale(0.5f), Is.InRange(0f, 1f));
                Assert.That(profile.EvaluateFade(2f), Is.LessThanOrEqualTo(1f));
                Assert.That(profile.EvaluateFade(-1f), Is.GreaterThanOrEqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
