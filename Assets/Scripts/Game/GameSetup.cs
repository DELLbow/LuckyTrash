using System;
using System.Collections.Generic;
using LuckyTrash.Cards;

namespace LuckyTrash.Game
{
    /// <summary>
    /// GameSetup.SetUp の結果。生成された Player 一覧と、この後のターン進行で使うめくり札用デッキを保持する。
    /// </summary>
    public sealed class GameSetupResult
    {
        /// <summary>座席順（下辺→右辺→上辺→左辺）に並んだ Player の一覧。</summary>
        public IReadOnlyList<Player> Players { get; }

        /// <summary>シャッフル済みのめくり札用デッキ（手札配布用デッキとは独立した別インスタンス）。</summary>
        public Deck FlipDeck { get; }

        public GameSetupResult(IReadOnlyList<Player> players, Deck flipDeck)
        {
            Players = players ?? throw new ArgumentNullException(nameof(players));
            FlipDeck = flipDeck ?? throw new ArgumentNullException(nameof(flipDeck));
        }
    }

    /// <summary>
    /// ゲーム開始時のセットアップ（Player生成・手札配布・めくり札用デッキ準備）を行う。
    /// </summary>
    public static class GameSetup
    {
        public const int MinPlayerCount = 2;
        public const int MaxPlayerCount = 4;
        public const int InitialHandSize = 6;

        /// <summary>
        /// 指定人数分の Player を座席順に生成し、手札配布用デッキ（52枚）をシャッフルして
        /// 各 Player に6枚ずつ配布する（配布後の残りは使わず破棄する）。
        /// あわせて、手札配布用デッキとは独立しためくり札用デッキ（52枚）を別途シャッフルして用意する。
        /// </summary>
        /// <param name="playerCount">プレイ人数（2〜4人）。範囲外の場合は例外を投げる。</param>
        /// <param name="random">
        /// シャッフルに使用する乱数生成器。省略時は既定の System.Random を使用する。
        /// テストで結果を固定したい場合はシード付きの Random を渡す。
        /// </param>
        /// <returns>生成された Player のリストと、めくり札用デッキ。</returns>
        public static GameSetupResult SetUp(int playerCount, Random random = null)
        {
            if (playerCount < MinPlayerCount || playerCount > MaxPlayerCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerCount), playerCount,
                    $"Player count must be between {MinPlayerCount} and {MaxPlayerCount}.");
            }

            var rng = random ?? new Random();

            // 座席順（下辺→右辺→上辺→左辺、SeatIndex 0始まり）に Player を生成する。
            var players = new List<Player>(playerCount);
            for (int seat = 0; seat < playerCount; seat++)
            {
                players.Add(new Player(seat));
            }

            // 手札配布用デッキ: シャッフルして各 Player に6枚ずつ配る。
            // 配布後の残り（余り）はこの後使わない＝スコープを抜けて破棄される。
            var handDeck = Deck.CreateStandard52(rng);
            handDeck.Shuffle();

            foreach (var player in players)
            {
                for (int i = 0; i < InitialHandSize; i++)
                {
                    player.AddToHand(handDeck.Draw());
                }
            }

            // めくり札用デッキ: 手札配布用デッキとは独立した別インスタンスとして生成・シャッフルする。
            var flipDeck = Deck.CreateStandard52(rng);
            flipDeck.Shuffle();

            return new GameSetupResult(players, flipDeck);
        }
    }
}
