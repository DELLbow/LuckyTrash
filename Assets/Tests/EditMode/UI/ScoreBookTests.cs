using System.Collections.Generic;
using System.Linq;
using LuckyTrash.Cards;
using LuckyTrash.Game;
using LuckyTrash.UI.Scoring;
using NUnit.Framework;

namespace LuckyTrash.UI.Tests
{
    public class ScoreBookTests
    {
        private static ScoreBook NewBook(int playerCount, bool continueSession = false, ScoreBook book = null)
        {
            book = book ?? new ScoreBook(new ScoreRules());
            book.BeginMatch(playerCount, Enumerable.Range(0, playerCount), continueSession);
            return book;
        }

        private static TurnResult Turn(int seat, int discarded, bool finished)
        {
            var player = new Player(seat);
            var cards = Enumerable.Range(1, discarded).Select(r => new Card(Suit.Spade, r)).ToList();
            return new TurnResult(player, RouletteCategory.Number, new Card(Suit.Heart, 1), cards,
                discarded == 0, finished ? player : null, false, false);
        }

        // ---- 順位の得点 ----

        [TestCase(4, 1, 100)]
        [TestCase(4, 2, 60)]
        [TestCase(4, 3, 30)]
        [TestCase(4, 4, 0)]
        [TestCase(3, 1, 100)]
        [TestCase(3, 2, 50)]
        [TestCase(3, 3, 0)]
        [TestCase(2, 1, 100)]
        [TestCase(2, 2, 0)]
        public void RankPoints_MatchTheTable(int playerCount, int rank, int expected)
        {
            Assert.AreEqual(expected, new ScoreRules().GetRankPoints(playerCount, rank));
        }

        [Test]
        public void FinishMatch_GivesRankPointsInRankingOrder()
        {
            var book = NewBook(4);
            var summary = book.FinishMatch(new[] { 2, 0, 3, 1 });

            CollectionAssert.AreEqual(new[] { 2, 0, 3, 1 }, summary.Lines.Select(l => l.SeatIndex).ToArray());
            CollectionAssert.AreEqual(new[] { 100, 60, 30, 0 }, summary.Lines.Select(l => l.RankPoints).ToArray());
            Assert.AreEqual(100, book.GetTotal(2));
            Assert.AreEqual(0, book.GetTotal(1));
        }

        // ---- ビッグトラッシュ ----

        [Test]
        public void BigTrash_NotAwardedForTwoCards()
        {
            var book = NewBook(4);
            CollectionAssert.IsEmpty(book.RecordTurn(Turn(0, 2, false)));
        }

        [Test]
        public void BigTrash_AwardedForThreeCards()
        {
            var book = NewBook(4);
            var awards = book.RecordTurn(Turn(0, 3, false));

            Assert.AreEqual(1, awards.Count);
            Assert.AreEqual(BonusKind.BigTrash, awards[0].Kind);
            Assert.AreEqual(20, awards[0].Points);
        }

        [Test]
        public void BigTrash_AddedEveryTime()
        {
            var book = NewBook(2);
            book.RecordTurn(Turn(0, 3, false));
            book.RecordTurn(Turn(1, 0, false));
            book.RecordTurn(Turn(0, 0, false));
            book.RecordTurn(Turn(0, 4, false));

            var line = book.FinishMatch(new[] { 0, 1 }).Lines[0];
            Assert.AreEqual(2, line.Bonuses.Count(b => b.Kind == BonusKind.BigTrash));
            Assert.AreEqual(100 + 20 + 20, line.Deposit);
        }

        // ---- ミラクルトラッシュ ----

        [Test]
        public void MiracleTrash_SixCardsAndFinished_Plus100WithoutBigTrash()
        {
            var book = NewBook(4);
            var awards = book.RecordTurn(Turn(1, 6, true));

            Assert.AreEqual(1, awards.Count);
            Assert.AreEqual(BonusKind.MiracleTrash, awards[0].Kind);
            Assert.AreEqual(100, awards[0].Points);
            Assert.IsFalse(awards.Any(a => a.Kind == BonusKind.BigTrash));
        }

        [Test]
        public void MiracleTrash_NotAwardedWithoutFinishing()
        {
            var book = NewBook(4);
            var awards = book.RecordTurn(Turn(1, 6, false));

            Assert.AreEqual(BonusKind.BigTrash, awards.Single().Kind);
        }

        // ---- 連続トラッシュ ----

        [Test]
        public void Streak_AwardedOnThirdConsecutiveTrash()
        {
            var book = NewBook(4);
            CollectionAssert.IsEmpty(book.RecordTurn(Turn(0, 1, false)));
            CollectionAssert.IsEmpty(book.RecordTurn(Turn(0, 1, false)));
            var third = book.RecordTurn(Turn(0, 1, false));

            Assert.AreEqual(BonusKind.Streak, third.Single().Kind);
            Assert.AreEqual(30, third.Single().Points);
        }

        [Test]
        public void Streak_ResetByNoTrash()
        {
            var book = NewBook(4);
            book.RecordTurn(Turn(0, 1, false));
            book.RecordTurn(Turn(0, 1, false));
            book.RecordTurn(Turn(0, 0, false));
            CollectionAssert.IsEmpty(book.RecordTurn(Turn(0, 1, false)));
            CollectionAssert.IsEmpty(book.RecordTurn(Turn(0, 1, false)));
            Assert.AreEqual(BonusKind.Streak, book.RecordTurn(Turn(0, 1, false)).Single().Kind);
        }

        [Test]
        public void Streak_CountRestartsAfterAward()
        {
            var book = NewBook(4);
            for (int i = 0; i < 3; i++)
            {
                book.RecordTurn(Turn(0, 1, false));
            }
            CollectionAssert.IsEmpty(book.RecordTurn(Turn(0, 1, false)));
            CollectionAssert.IsEmpty(book.RecordTurn(Turn(0, 1, false)));
            Assert.AreEqual(BonusKind.Streak, book.RecordTurn(Turn(0, 1, false)).Single().Kind);
        }

        [Test]
        public void Streak_CountedPerSeat()
        {
            var book = NewBook(2);
            book.RecordTurn(Turn(0, 1, false));
            book.RecordTurn(Turn(1, 0, false));
            book.RecordTurn(Turn(0, 1, false));
            book.RecordTurn(Turn(1, 1, false));
            Assert.AreEqual(BonusKind.Streak, book.RecordTurn(Turn(0, 1, false)).Single().Kind);
        }

        [Test]
        public void Streak_AndBigTrash_CanBothBeAwardedInOneTurn()
        {
            var book = NewBook(4);
            book.RecordTurn(Turn(0, 1, false));
            book.RecordTurn(Turn(0, 1, false));
            var awards = book.RecordTurn(Turn(0, 3, false));

            CollectionAssert.AreEqual(new[] { BonusKind.BigTrash, BonusKind.Streak }, awards.Select(a => a.Kind).ToArray());
        }

        // ---- 通算の加算とリセット ----

        [Test]
        public void ContinueSession_AccumulatesTotalsAndCountsMatches()
        {
            var book = NewBook(4);
            book.FinishMatch(new[] { 0, 1, 2, 3 });
            NewBook(4, continueSession: true, book: book);
            var summary = book.FinishMatch(new[] { 1, 0, 2, 3 });

            Assert.AreEqual(2, summary.MatchNumber);
            Assert.AreEqual(160, book.GetTotal(0));
            Assert.AreEqual(160, book.GetTotal(1));
            var line = summary.Lines.First(l => l.SeatIndex == 0);
            Assert.AreEqual(100, line.PreviousBalance);
            Assert.AreEqual(160, line.Balance);
        }

        [Test]
        public void PlayerCountChange_ResetsTotals()
        {
            var book = NewBook(4);
            book.FinishMatch(new[] { 0, 1, 2, 3 });
            NewBook(3, continueSession: true, book: book);

            Assert.AreEqual(1, book.MatchNumber);
            Assert.AreEqual(0, book.GetTotal(0));
        }

        [Test]
        public void NewSessionFromTitle_ResetsTotals()
        {
            var book = NewBook(4);
            book.FinishMatch(new[] { 0, 1, 2, 3 });
            NewBook(4, continueSession: false, book: book);

            Assert.AreEqual(1, book.MatchNumber);
            Assert.AreEqual(0, book.GetTotal(0));
        }

        [Test]
        public void Bonuses_DoNotCarryOverToTheNextMatch()
        {
            var book = NewBook(2);
            book.RecordTurn(Turn(0, 3, false));
            book.FinishMatch(new[] { 0, 1 });
            NewBook(2, continueSession: true, book: book);
            var line = book.FinishMatch(new[] { 1, 0 }).Lines.First(l => l.SeatIndex == 0);

            CollectionAssert.IsEmpty(line.Bonuses);
            Assert.AreEqual(120, line.Balance);
        }

        [Test]
        public void LeaderChanged_WhenSoleLeaderIsOvertaken()
        {
            var book = NewBook(2);
            var first = book.FinishMatch(new[] { 0, 1 });
            Assert.IsFalse(first.LeaderChanged, "第1戦は前回の1位がいないので逆転にならない");
            CollectionAssert.AreEqual(new[] { 0 }, first.LeaderSeats);

            NewBook(2, continueSession: true, book: book);
            book.RecordTurn(Turn(1, 6, true));
            var second = book.FinishMatch(new[] { 1, 0 });

            Assert.IsTrue(second.LeaderChanged);
            CollectionAssert.AreEqual(new[] { 1 }, second.LeaderSeats);
        }

        [Test]
        public void LeaderChanged_FalseWhenTied()
        {
            var book = NewBook(2);
            book.FinishMatch(new[] { 0, 1 });
            NewBook(2, continueSession: true, book: book);
            var summary = book.FinishMatch(new[] { 1, 0 });

            Assert.IsFalse(summary.LeaderChanged);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, summary.LeaderSeats);
        }
    }
}
