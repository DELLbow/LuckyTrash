using System;
using System.Collections.Generic;
using System.Linq;
using LuckyTrash.Game;

namespace LuckyTrash.UI.Scoring
{
    /// <summary>ボーナスの種類。</summary>
    public enum BonusKind
    {
        BigTrash,
        MiracleTrash,
        Streak,
    }

    /// <summary>付いたボーナス1つ（種類・表示名・得点）。</summary>
    public readonly struct BonusAward
    {
        public BonusKind Kind { get; }
        public string Name { get; }
        public int Points { get; }

        public BonusAward(BonusKind kind, string name, int points)
        {
            Kind = kind;
            Name = name;
            Points = points;
        }
    }

    /// <summary>1回の勝負の、プレイヤー1人分の得点の明細。</summary>
    public sealed class PlayerScoreLine
    {
        public int SeatIndex { get; }
        public int Rank { get; }
        public int RankPoints { get; }
        public IReadOnlyList<BonusAward> Bonuses { get; }
        /// <summary>今回の合計（順位の得点＋ボーナス）。</summary>
        public int Deposit { get; }
        /// <summary>今回の勝負の前の通算。</summary>
        public int PreviousBalance { get; }
        /// <summary>今回の勝負の後の通算。</summary>
        public int Balance { get; }

        public PlayerScoreLine(int seatIndex, int rank, int rankPoints, IReadOnlyList<BonusAward> bonuses, int previousBalance)
        {
            SeatIndex = seatIndex;
            Rank = rank;
            RankPoints = rankPoints;
            Bonuses = bonuses;
            Deposit = rankPoints + bonuses.Sum(b => b.Points);
            PreviousBalance = previousBalance;
            Balance = previousBalance + Deposit;
        }
    }

    /// <summary>1回の勝負の得点の明細（今回の順位の順）と、通算の1位の入れ替わり。</summary>
    public sealed class MatchSummary
    {
        /// <summary>続けて遊んだ回数（第N戦）。</summary>
        public int MatchNumber { get; }
        public IReadOnlyList<PlayerScoreLine> Lines { get; }
        /// <summary>今回の勝負の後に、通算が一番高い座席（同点ならすべて）。</summary>
        public IReadOnlyList<int> LeaderSeats { get; }
        /// <summary>今回の結果で、通算の単独1位が別の人に入れ替わったか。</summary>
        public bool LeaderChanged { get; }

        public MatchSummary(int matchNumber, IReadOnlyList<PlayerScoreLine> lines, IReadOnlyList<int> leaderSeats, bool leaderChanged)
        {
            MatchNumber = matchNumber;
            Lines = lines;
            LeaderSeats = leaderSeats;
            LeaderChanged = leaderChanged;
        }
    }

    /// <summary>
    /// 得点の計算（順位の得点、ボーナスの判定、通算の加算・リセット）。Unity にもゲームの進行にも依存しない純粋なクラスで、
    /// ターンの結果（<see cref="TurnResult"/>）と最終順位を読み取るだけで判定する。
    /// 通算は「もう一度プレイ」で続けて遊ぶ間だけ持ち越し、人数が変わったときや、続けずに新しく始めたときにリセットする。
    /// プレイヤーは座席の番号で識別する（同じ人数で続けて遊ぶ限り、座席と人は同じ）。
    /// </summary>
    public sealed class ScoreBook
    {
        private readonly Dictionary<int, int> _totals = new Dictionary<int, int>();
        private readonly Dictionary<int, List<BonusAward>> _matchBonuses = new Dictionary<int, List<BonusAward>>();
        private readonly Dictionary<int, int> _streaks = new Dictionary<int, int>();

        public ScoreBook(ScoreRules rules)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        /// <summary>得点のルール（勝負の始めに差し替えてよい）。</summary>
        public ScoreRules Rules { get; set; }

        /// <summary>いま続けて遊んでいる人数（まだ始まっていなければ 0）。</summary>
        public int PlayerCount { get; private set; }

        /// <summary>続けて遊んだ回数（第N戦。まだ始まっていなければ 0）。</summary>
        public int MatchNumber { get; private set; }

        /// <summary>座席ごとの通算の得点。</summary>
        public IReadOnlyDictionary<int, int> Totals => _totals;

        /// <summary>座席の通算の得点（記録がなければ 0）。</summary>
        public int GetTotal(int seatIndex) => _totals.TryGetValue(seatIndex, out int total) ? total : 0;

        /// <summary>
        /// 1回の勝負を始める。continueSession が false のとき（タイトルから新しく始めたとき）や、人数が前回と違うときは
        /// 通算をリセットして第1戦から数え直す。true で人数も同じなら、通算を持ち越して回数を1つ進める。
        /// </summary>
        public void BeginMatch(int playerCount, IEnumerable<int> seatIndices, bool continueSession)
        {
            if (seatIndices == null)
            {
                throw new ArgumentNullException(nameof(seatIndices));
            }
            if (!continueSession || playerCount != PlayerCount || MatchNumber == 0)
            {
                ResetSession();
            }

            PlayerCount = playerCount;
            MatchNumber++;
            _matchBonuses.Clear();
            _streaks.Clear();
            foreach (int seat in seatIndices)
            {
                if (!_totals.ContainsKey(seat))
                {
                    _totals[seat] = 0;
                }
                _matchBonuses[seat] = new List<BonusAward>();
                _streaks[seat] = 0;
            }
        }

        /// <summary>通算をすべて消し、続けて遊んだ回数も 0 に戻す（タイトルへ戻ったとき）。</summary>
        public void ResetSession()
        {
            _totals.Clear();
            _matchBonuses.Clear();
            _streaks.Clear();
            PlayerCount = 0;
            MatchNumber = 0;
        }

        /// <summary>ターンの結果からボーナスを判定して記録し、このターンに付いたボーナスを返す。</summary>
        public IReadOnlyList<BonusAward> RecordTurn(TurnResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
            int seat = result.TurnPlayer.SeatIndex;
            bool finished = result.FinishedPlayer != null && result.FinishedPlayer.SeatIndex == seat;
            return RecordTurn(seat, result.DiscardedCards.Count, finished);
        }

        /// <summary>
        /// ボーナスを判定して記録し、このターンに付いたボーナスを返す（付いた順：ミラクル／ビッグ → 連続）。
        /// </summary>
        /// <param name="seatIndex">手番の座席。</param>
        /// <param name="discardedCount">このターンに捨てた枚数（0ならノートラッシュ）。</param>
        /// <param name="finishedThisTurn">このターンにあがったか。</param>
        public IReadOnlyList<BonusAward> RecordTurn(int seatIndex, int discardedCount, bool finishedThisTurn)
        {
            var awards = new List<BonusAward>();
            if (!_matchBonuses.TryGetValue(seatIndex, out var bonuses))
            {
                bonuses = new List<BonusAward>();
                _matchBonuses[seatIndex] = bonuses;
            }
            _streaks.TryGetValue(seatIndex, out int streak);

            if (discardedCount <= 0)
            {
                // ノートラッシュの番があったら、連続の回数を数え直す。
                _streaks[seatIndex] = 0;
                return awards;
            }

            if (finishedThisTurn && discardedCount >= Rules.MiracleTrashCards)
            {
                awards.Add(new BonusAward(BonusKind.MiracleTrash, Rules.MiracleTrashName, Rules.MiracleTrashPoints));
            }
            else if (discardedCount >= Rules.BigTrashMinCards)
            {
                awards.Add(new BonusAward(BonusKind.BigTrash, Rules.BigTrashName, Rules.BigTrashPoints));
            }

            streak++;
            if (Rules.StreakTurns > 0 && streak >= Rules.StreakTurns)
            {
                awards.Add(new BonusAward(BonusKind.Streak, Rules.StreakName, Rules.StreakPoints));
                streak = 0;
            }
            _streaks[seatIndex] = streak;

            bonuses.AddRange(awards);
            return awards;
        }

        /// <summary>
        /// 勝負の終わりに、最終順位（座席の番号、1位から順）から順位の得点を付け、ボーナスと合わせて通算に加える。
        /// </summary>
        public MatchSummary FinishMatch(IReadOnlyList<int> rankingSeatIndices)
        {
            if (rankingSeatIndices == null)
            {
                throw new ArgumentNullException(nameof(rankingSeatIndices));
            }

            var previousLeaders = Leaders();
            var lines = new List<PlayerScoreLine>(rankingSeatIndices.Count);
            for (int i = 0; i < rankingSeatIndices.Count; i++)
            {
                int seat = rankingSeatIndices[i];
                int rank = i + 1;
                var bonuses = _matchBonuses.TryGetValue(seat, out var list) ? list.ToList() : new List<BonusAward>();
                var line = new PlayerScoreLine(seat, rank, Rules.GetRankPoints(PlayerCount, rank), bonuses, GetTotal(seat));
                lines.Add(line);
            }
            foreach (var line in lines)
            {
                _totals[line.SeatIndex] = line.Balance;
            }

            var leaders = Leaders();
            // 前回の単独1位と今回の単独1位が別の人なら「逆転」（どちらかが同点なら逆転とはしない）。
            bool changed = previousLeaders.Count == 1 && leaders.Count == 1 && previousLeaders[0] != leaders[0];
            return new MatchSummary(MatchNumber, lines, leaders, changed);
        }

        /// <summary>通算が一番高い座席（同点ならすべて。全員 0 なら空）。</summary>
        private List<int> Leaders()
        {
            if (_totals.Count == 0)
            {
                return new List<int>();
            }
            int max = _totals.Values.Max();
            if (max <= 0)
            {
                return new List<int>();
            }
            return _totals.Where(kv => kv.Value == max).Select(kv => kv.Key).OrderBy(s => s).ToList();
        }
    }
}
