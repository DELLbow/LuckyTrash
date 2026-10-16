using System;
using NUnit.Framework;

namespace LuckyTrash.UI.Tests
{
    public class DeckCountDrumTests
    {
        // 1枚減るときは、変わる桁だけが減る向きに1コマ回る。
        [TestCase(49, 48, new[] { 0, -1 })]
        [TestCase(40, 39, new[] { -1, -1 })]
        [TestCase(10, 9, new[] { -1, -1 })]
        [TestCase(1, 0, new[] { 0, -1 })]
        public void GetSteps_DecreaseByOne_RollsOnlyChangedDigitsOneStepDown(int from, int to, int[] expected)
        {
            CollectionAssert.AreEqual(expected, DeckCountDrum.GetSteps(from, to, 2));
        }

        // 増えるとき（作り直し）は、変わる桁が増える向きに回る。
        [TestCase(9, 35, new[] { 3, 6 })]
        [TestCase(0, 35, new[] { 3, 5 })]
        [TestCase(0, 40, new[] { 4, 0 })]
        public void GetSteps_Increase_RollsChangedDigitsForward(int from, int to, int[] expected)
        {
            CollectionAssert.AreEqual(expected, DeckCountDrum.GetSteps(from, to, 2));
        }

        [Test]
        public void GetSteps_IncreaseWithExtraTurns_AddsFullTurnsOnlyToChangedDigits()
        {
            CollectionAssert.AreEqual(new[] { 13, 16 }, DeckCountDrum.GetSteps(9, 35, 2, extraTurnsOnIncrease: 1));
            CollectionAssert.AreEqual(new[] { 14, 0 }, DeckCountDrum.GetSteps(0, 40, 2, extraTurnsOnIncrease: 1));
        }

        [Test]
        public void GetSteps_SameValue_RollsNothing()
        {
            CollectionAssert.AreEqual(new[] { 0, 0 }, DeckCountDrum.GetSteps(25, 25, 2));
        }

        [Test]
        public void GetSteps_LargerDecrease_UsesShortestStepsDownward()
        {
            // 52 → 47: 十の位 5→4 を1コマ、一の位 2→7 を減る向きに5コマ。
            CollectionAssert.AreEqual(new[] { -1, -5 }, DeckCountDrum.GetSteps(52, 47, 2));
        }

        [Test]
        public void GetSteps_NegativeCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DeckCountDrum.GetSteps(-1, 3, 2));
        }
    }
}
