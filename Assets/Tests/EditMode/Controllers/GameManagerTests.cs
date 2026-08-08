using System.Collections.Generic;
using LuckyTrash.Controllers;
using NUnit.Framework;
using UnityEngine;

namespace LuckyTrash.Controllers.Tests
{
    public class GameManagerTests
    {
        private GameObject _go;
        private GameManager _gameManager;

        [SetUp]
        public void SetUp()
        {
            // GameManager.Instance（静的シングルトン）は使わず、テストごとに独立したインスタンスを
            // 直接生成する。DontDestroyOnLoad はエディタのシーン境界の影響を受けないため、
            // Awake が走っても問題なくテスト後に破棄できる。
            _go = new GameObject("GameManagerTestInstance");
            _gameManager = _go.AddComponent<GameManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        // ------------------------------------------------------------------
        // TryGetStartingSeat（開始プレイヤー決定用のリピート判定）
        // ------------------------------------------------------------------

        [Test]
        public void TryGetStartingSeat_NoRecordYet_ReturnsFalse()
        {
            bool result = _gameManager.TryGetStartingSeat(4, new[] { 0, 1, 2, 3 }, out int startingSeat);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryGetStartingSeat_SamePlayerCountAndSeats_ReturnsTrue_WithRecordedLastPlaceSeat()
        {
            // 「同じ人数・同じ座席構成で2回目、前回最下位だった座席から開始する」ことの検証。
            _gameManager.RecordGameResult(4, new[] { 0, 1, 2, 3 }, lastPlaceSeatIndex: 2, finalRankingSeatIndices: new[] { 1, 0, 3, 2 });

            bool result = _gameManager.TryGetStartingSeat(4, new[] { 0, 1, 2, 3 }, out int startingSeat);

            Assert.IsTrue(result);
            Assert.AreEqual(2, startingSeat);
        }

        [Test]
        public void TryGetStartingSeat_DifferentPlayerCount_ReturnsFalse()
        {
            _gameManager.RecordGameResult(4, new[] { 0, 1, 2, 3 }, lastPlaceSeatIndex: 2, finalRankingSeatIndices: new[] { 1, 0, 3, 2 });

            bool result = _gameManager.TryGetStartingSeat(3, new[] { 0, 1, 2 }, out int startingSeat);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryGetStartingSeat_DifferentSeatComposition_ReturnsFalse()
        {
            // 座席の「集合」比較ロジックを、人数の一致だけに引きずられずに検証する
            // （実際の GetSeatMapping では人数が同じなら座席集合も一意に決まるが、
            //  TryGetStartingSeat 自体は座席集合の完全一致を独立してチェックする設計になっている）。
            _gameManager.RecordGameResult(3, new[] { 0, 1, 2 }, lastPlaceSeatIndex: 1, finalRankingSeatIndices: new[] { 0, 1, 2 });

            bool result = _gameManager.TryGetStartingSeat(3, new[] { 0, 1, 3 }, out int startingSeat);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryGetStartingSeat_SeatOrderDoesNotMatter()
        {
            _gameManager.RecordGameResult(4, new[] { 0, 1, 2, 3 }, lastPlaceSeatIndex: 3, finalRankingSeatIndices: new[] { 0, 1, 2, 3 });

            bool result = _gameManager.TryGetStartingSeat(4, new[] { 3, 2, 1, 0 }, out int startingSeat);

            Assert.IsTrue(result);
            Assert.AreEqual(3, startingSeat);
        }

        [Test]
        public void TryGetStartingSeat_AfterFailedLookup_OutParamIsNegativeOne()
        {
            bool result = _gameManager.TryGetStartingSeat(2, new[] { 0, 1 }, out int startingSeat);

            Assert.IsFalse(result);
            Assert.AreEqual(-1, startingSeat);
        }

        // ------------------------------------------------------------------
        // LastFinalRankingSeatIndices（ResultScene 表示用の最終順位データ）
        // ------------------------------------------------------------------

        [Test]
        public void LastFinalRankingSeatIndices_BeforeAnyRecord_IsNull()
        {
            Assert.IsNull(_gameManager.LastFinalRankingSeatIndices);
        }

        [Test]
        public void LastFinalRankingSeatIndices_AfterRecordGameResult_MatchesRecordedOrder()
        {
            var finalRanking = new List<int> { 2, 0, 3, 1 }; // 1位=Player2, 2位=Player0, ...

            _gameManager.RecordGameResult(4, new[] { 0, 1, 2, 3 }, lastPlaceSeatIndex: 1, finalRankingSeatIndices: finalRanking);

            CollectionAssert.AreEqual(finalRanking, _gameManager.LastFinalRankingSeatIndices);
        }

        // ------------------------------------------------------------------
        // クイック再開（RequestQuickRestart / ClearQuickRestart / TryConsumeQuickRestart）
        // ------------------------------------------------------------------

        [Test]
        public void TryConsumeQuickRestart_WithoutRequest_ReturnsFalse()
        {
            bool result = _gameManager.TryConsumeQuickRestart(out int playerCount);

            Assert.IsFalse(result);
        }

        [Test]
        public void RequestQuickRestart_WithoutPreviousRecord_DoesNothing()
        {
            // 直近のゲーム結果が記録されていない状態で呼んでも予約されないこと。
            _gameManager.RequestQuickRestart();

            bool result = _gameManager.TryConsumeQuickRestart(out int playerCount);

            Assert.IsFalse(result);
        }

        [Test]
        public void RequestQuickRestart_AfterRecordGameResult_TryConsumeReturnsTrue_WithSamePlayerCount()
        {
            _gameManager.RecordGameResult(3, new[] { 0, 1, 2 }, lastPlaceSeatIndex: 2, finalRankingSeatIndices: new[] { 0, 1, 2 });

            _gameManager.RequestQuickRestart();
            bool result = _gameManager.TryConsumeQuickRestart(out int playerCount);

            Assert.IsTrue(result);
            Assert.AreEqual(3, playerCount);
        }

        [Test]
        public void TryConsumeQuickRestart_ConsumesTheRequest_SecondCallReturnsFalse()
        {
            _gameManager.RecordGameResult(2, new[] { 0, 1 }, lastPlaceSeatIndex: 0, finalRankingSeatIndices: new[] { 1, 0 });
            _gameManager.RequestQuickRestart();

            bool first = _gameManager.TryConsumeQuickRestart(out int firstPlayerCount);
            bool second = _gameManager.TryConsumeQuickRestart(out int secondPlayerCount);

            Assert.IsTrue(first);
            Assert.IsFalse(second, "一度消費した予約は再度 true を返さないこと。");
        }

        [Test]
        public void ClearQuickRestart_CancelsPendingRequest()
        {
            _gameManager.RecordGameResult(4, new[] { 0, 1, 2, 3 }, lastPlaceSeatIndex: 0, finalRankingSeatIndices: new[] { 1, 2, 3, 0 });
            _gameManager.RequestQuickRestart();

            _gameManager.ClearQuickRestart();
            bool result = _gameManager.TryConsumeQuickRestart(out int playerCount);

            Assert.IsFalse(result, "人数選択やり直し相当の ClearQuickRestart 後は予約が消えていること。");
        }
    }
}
