using System;
using System.Collections.Generic;
using LuckyTrash.Game;
using NUnit.Framework;

namespace LuckyTrash.Game.Tests
{
    public class RouletteSelectorTests
    {
        [Test]
        public void AllFaces_HasSixFaces_WithCorrectBreakdown()
        {
            var faces = RouletteSelector.AllFaces;

            Assert.AreEqual(6, faces.Count);

            var counts = CountByCategory(faces);

            Assert.AreEqual(3, counts[RouletteCategory.Number], "Number should occupy 3 faces.");
            Assert.AreEqual(2, counts[RouletteCategory.Suit], "Suit should occupy 2 faces.");
            Assert.AreEqual(1, counts[RouletteCategory.Color], "Color should occupy 1 face.");
        }

        [Test]
        public void Spin_LargeSampleSize_ApproximatesThreeTwoOneRatio()
        {
            const int trials = 6000;
            var selector = new RouletteSelector(new Random(2024));

            var counts = new Dictionary<RouletteCategory, int>
            {
                { RouletteCategory.Number, 0 },
                { RouletteCategory.Suit, 0 },
                { RouletteCategory.Color, 0 }
            };

            for (int i = 0; i < trials; i++)
            {
                counts[selector.Spin()]++;
            }

            // 期待値: Number 3/6, Suit 2/6, Color 1/6
            double expectedNumber = trials * 3.0 / 6.0; // 3000
            double expectedSuit = trials * 2.0 / 6.0;   // 2000
            double expectedColor = trials * 1.0 / 6.0;  // 1000

            // 許容誤差: 期待値の10%
            AssertWithinTolerance(counts[RouletteCategory.Number], expectedNumber, 0.10);
            AssertWithinTolerance(counts[RouletteCategory.Suit], expectedSuit, 0.10);
            AssertWithinTolerance(counts[RouletteCategory.Color], expectedColor, 0.10);

            Assert.AreEqual(trials, counts[RouletteCategory.Number] + counts[RouletteCategory.Suit] + counts[RouletteCategory.Color]);
        }

        [Test]
        public void Spin_SameSeed_ProducesSameSequence()
        {
            var selectorA = new RouletteSelector(new Random(999));
            var selectorB = new RouletteSelector(new Random(999));

            const int trials = 100;
            var resultsA = new RouletteCategory[trials];
            var resultsB = new RouletteCategory[trials];

            for (int i = 0; i < trials; i++)
            {
                resultsA[i] = selectorA.Spin();
                resultsB[i] = selectorB.Spin();
            }

            CollectionAssert.AreEqual(resultsA, resultsB);
        }

        [Test]
        public void Spin_DifferentSeed_CanProduceDifferentSequence()
        {
            var selectorA = new RouletteSelector(new Random(1));
            var selectorB = new RouletteSelector(new Random(2));

            const int trials = 50;
            var resultsA = new RouletteCategory[trials];
            var resultsB = new RouletteCategory[trials];

            for (int i = 0; i < trials; i++)
            {
                resultsA[i] = selectorA.Spin();
                resultsB[i] = selectorB.Spin();
            }

            CollectionAssert.AreNotEqual(resultsA, resultsB);
        }

        private static Dictionary<RouletteCategory, int> CountByCategory(IReadOnlyList<RouletteCategory> faces)
        {
            var counts = new Dictionary<RouletteCategory, int>
            {
                { RouletteCategory.Number, 0 },
                { RouletteCategory.Suit, 0 },
                { RouletteCategory.Color, 0 }
            };

            foreach (var face in faces)
            {
                counts[face]++;
            }

            return counts;
        }

        private static void AssertWithinTolerance(int actual, double expected, double relativeTolerance)
        {
            double allowedDelta = expected * relativeTolerance;
            Assert.That(actual, Is.InRange(expected - allowedDelta, expected + allowedDelta),
                $"actual={actual} expected~{expected} (±{allowedDelta})");
        }
    }
}
