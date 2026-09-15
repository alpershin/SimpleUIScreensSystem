using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace SimpleUIScreensSystem.Tests
{
    public sealed class ScreenPopularityPolicyTests
    {
        private static readonly ScreenId A = new ScreenId("a");
        private static readonly ScreenId B = new ScreenId("b");
        private static readonly ScreenId C = new ScreenId("c");
        private static readonly ScreenId Important = new ScreenId("important");
        private static readonly ScreenId Unknown = new ScreenId("unknown");

        [Test]
        public void ColdStartRanksByDesignerPriority()
        {
            var policy = CreatePolicy();
            policy.Register(Important, 10);
            Assert.That(policy.GetScore(Important, 0), Is.GreaterThan(policy.GetScore(A, 0)));
            Assert.That(policy.GetScore(A, 0), Is.EqualTo(policy.GetScore(B, 0)));
        }

        [Test]
        public void FrequentActualOpeningsCanOutrankColdStartPriority()
        {
            var policy = CreatePolicy();
            policy.Register(Important, 10);
            for (var i = 0; i < 10; i++) policy.RecordOpen(A, i);
            Assert.That(policy.GetScore(A, 10), Is.GreaterThan(policy.GetScore(Important, 10)));
        }

        [Test]
        public void OldPopularityDecaysAndRecentUsageChangesTheRanking()
        {
            var policy = CreatePolicy();
            for (var i = 0; i < 8; i++) policy.RecordOpen(A, 0);
            var initial = policy.GetScore(A, 0);
            Assert.That(policy.GetScore(A, 10), Is.LessThan(initial));
            policy.RecordOpen(B, 100);
            policy.ResetSessionContext();
            Assert.That(policy.GetScore(B, 100), Is.GreaterThan(policy.GetScore(A, 100)));
        }

        [Test]
        public void CurrentScreenPredictsItsLearnedSuccessor()
        {
            var policy = CreatePolicy();
            for (var i = 0; i < 6; i++)
            {
                policy.ResetSessionContext();
                policy.RecordOpen(A, 0);
                policy.RecordOpen(B, 0);
                policy.ResetSessionContext();
                policy.RecordOpen(C, 0);
            }

            policy.ResetSessionContext();
            Assert.That(policy.GetScore(B, 0), Is.EqualTo(policy.GetScore(C, 0)));
            policy.RecordOpen(A, 0);
            Assert.That(policy.GetScore(B, 0), Is.GreaterThan(policy.GetScore(C, 0)));
        }

        [Test]
        public void LearnedActionChangesPredictionUntilItExpires()
        {
            var policy = CreatePolicy();
            TrainAction(policy, "shop", B);
            for (var i = 0; i < 4; i++) policy.RecordOpen(C, 0);
            policy.ResetSessionContext();
            Assert.That(policy.GetScore(B, 0), Is.EqualTo(policy.GetScore(C, 0)));

            policy.RecordAction("shop", 0);
            Assert.That(policy.GetScore(B, 0), Is.GreaterThan(policy.GetScore(C, 0)));
            Assert.That(policy.GetScore(B, 31), Is.EqualTo(policy.GetScore(C, 31)));
        }

        [Test]
        public void ExpiredActionIsNotLearnedFromAnUnrelatedLaterOpening()
        {
            var policy = CreatePolicy();
            policy.RecordAction("shop", 0);
            policy.RecordOpen(B, 31);
            policy.ResetSessionContext();
            var baseline = policy.GetScore(B, 31);
            policy.RecordAction("shop", 31);
            Assert.That(policy.GetScore(B, 31), Is.EqualTo(baseline));
        }

        [Test]
        public void AnActionIsConsumedByOnlyTheNextSuccessfulOpening()
        {
            var policy = CreatePolicy();
            TrainAction(policy, "shop", B);
            policy.RecordAction("shop", 0);
            policy.RecordOpen(B, 0);
            policy.RecordOpen(C, 0);
            policy.ResetSessionContext();
            var baseline = policy.GetScore(C, 0);
            policy.RecordAction("shop", 0);
            Assert.That(policy.GetScore(C, 0), Is.EqualTo(baseline));
        }

        [Test]
        public void ActionStatisticsEvictTheLeastRecentlyUsedDistinctAction()
        {
            var policy = CreatePolicy();
            TrainAction(policy, "old", B);
            for (var i = 0; i < 31; i++) policy.RecordAction("action_" + i, 0);
            policy.RecordAction("old", 0);
            policy.RecordAction("new", 0);
            policy.RecordAction("old", 0);
            var retained = policy.GetScore(B, 0);
            policy.ResetSessionContext();
            Assert.That(retained, Is.GreaterThan(policy.GetScore(B, 0)));

            for (var i = 0; i < 32; i++) policy.RecordAction("replacement_" + i, 0);
            policy.ResetSessionContext();
            var baseline = policy.GetScore(B, 0);
            policy.RecordAction("old", 0);
            Assert.That(policy.GetScore(B, 0), Is.EqualTo(baseline));
        }

        [Test]
        public void ReadingFutureScoresDoesNotMutatePresentScores()
        {
            var policy = CreatePolicy();
            policy.RecordOpen(A, 0);
            var present = policy.GetScore(A, 0);
            policy.GetScore(A, 1000);
            Assert.That(policy.GetScore(A, 0), Is.EqualTo(present));
        }

        [Test]
        public void BackwardsEventTimestampsCannotAmplifyExistingHistory()
        {
            var policy = CreatePolicy();
            policy.RecordOpen(A, 100);
            policy.RecordOpen(A, 0);
            Assert.That(policy.GetScore(A, 100), Is.EqualTo(Math.Log(3)).Within(1e-12));
            Assert.That(policy.GetScore(A, 0), Is.EqualTo(policy.GetScore(A, 100)));
        }

        [Test]
        public void RegistrationUpdatesPriorityWithoutErasingPopularity()
        {
            var policy = CreatePolicy();
            policy.RecordOpen(A, 0);
            var before = policy.GetScore(A, 0);
            policy.Register(A, 10);
            Assert.That(policy.GetScore(A, 0), Is.EqualTo(before + 1).Within(1e-12));
        }

        [Test]
        public void ResetRemovesContextButRetainsLearnedCounts()
        {
            var policy = CreatePolicy();
            TrainAction(policy, "shop", B);
            policy.RecordAction("shop", 0);
            var contextual = policy.GetScore(B, 0);
            policy.ResetSessionContext();
            var baseline = policy.GetScore(B, 0);
            Assert.That(baseline, Is.GreaterThan(0).And.LessThan(contextual));
            policy.RecordAction("shop", 0);
            Assert.That(policy.GetScore(B, 0), Is.EqualTo(contextual));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void RejectsInvalidDurations(double duration)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenPopularityPolicy(duration));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenPopularityPolicy(10, duration));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void RejectsEmptyActionIds(string id)
        {
            var policy = CreatePolicy();
            Assert.Throws<ArgumentException>(() => policy.RecordAction(id, 0));
        }

        [Test]
        public void RejectsDefaultScreenId()
        {
            var policy = CreatePolicy();
            Assert.Throws<ArgumentException>(() => policy.Register(default, 0));
            Assert.Throws<ArgumentException>(() => policy.RecordOpen(default, 0));
            Assert.Throws<ArgumentException>(() => policy.GetScore(default, 0));
        }

        [Test]
        public void EquivalentKeysShareLearnedHistory()
        {
            var policy = CreatePolicy();
            policy.RecordOpen(new ScreenId(A.Value), 0);
            policy.Register(new ScreenId(A.Value), 10);
            Assert.That(policy.GetScore(A, 0), Is.EqualTo(Math.Log(2) + 1).Within(1e-12));
        }

        [Test]
        public void ScreenKeysRemainCaseSensitive()
        {
            var policy = CreatePolicy();
            var upperCase = new ScreenId("A");
            policy.Register(upperCase, 0);
            policy.RecordOpen(A, 0);
            Assert.That(policy.GetScore(A, 0), Is.GreaterThan(0));
            Assert.That(policy.GetScore(upperCase, 0), Is.Zero);
        }

        [Test]
        public void RejectsUnknownScreensAndOutOfRangePriorities()
        {
            var policy = CreatePolicy();
            Assert.Throws<KeyNotFoundException>(() => policy.RecordOpen(Unknown, 0));
            Assert.Throws<KeyNotFoundException>(() => policy.GetScore(Unknown, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => policy.Register(A, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => policy.Register(A, 11));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void RejectsNonfiniteTimestamps(double time)
        {
            var policy = CreatePolicy();
            Assert.Throws<ArgumentOutOfRangeException>(() => policy.RecordOpen(A, time));
            Assert.Throws<ArgumentOutOfRangeException>(() => policy.RecordAction("shop", time));
            Assert.Throws<ArgumentOutOfRangeException>(() => policy.GetScore(A, time));
        }

        [Test]
        public void VeryShortHalfLifeStillProducesFiniteScores()
        {
            var policy = new ScreenPopularityPolicy(double.Epsilon);
            policy.Register(A, 0);
            policy.RecordOpen(A, 0);
            Assert.That(policy.GetScore(A, 0), Is.EqualTo(Math.Log(2)));
            Assert.That(policy.GetScore(A, 1), Is.EqualTo(0));
        }

        private static ScreenPopularityPolicy CreatePolicy()
        {
            var policy = new ScreenPopularityPolicy(10, 30);
            policy.Register(A, 0);
            policy.Register(B, 0);
            policy.Register(C, 0);
            return policy;
        }

        private static void TrainAction(ScreenPopularityPolicy policy, string action, ScreenId screen)
        {
            for (var i = 0; i < 4; i++)
            {
                policy.RecordAction(action, 0);
                policy.RecordOpen(screen, 0);
            }
            policy.ResetSessionContext();
        }
    }
}
