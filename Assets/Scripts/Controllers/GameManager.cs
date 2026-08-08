using System.Collections.Generic;
using UnityEngine;

namespace LuckyTrash.Controllers
{
    /// <summary>
    /// シーンをまたいで保持する必要がある、直近のゲーム結果に関する最小限の情報を管理するシングルトン
    /// （<see cref="DontDestroyOnLoad(Object)"/>）。
    /// 用途は以下の2つ:
    /// 1. 「次回ゲーム開始時に、前回と同じ人数・同じ座席構成であれば前回の最下位だった座席から
    ///    開始する」という判定（<see cref="TryGetStartingSeat"/>）。
    /// 2. ResultScene での最終順位表示（<see cref="LastFinalRankingSeatIndices"/>）と、
    ///    ResultScene の「もう一度プレイ」から GameScene に戻った際に人数選択をスキップして
    ///    自動的にゲームを開始するためのクイック再開予約（<see cref="RequestQuickRestart"/> /
    ///    <see cref="TryConsumeQuickRestart"/>）。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private static GameManager _instance;

        /// <summary>
        /// シングルトンインスタンス。存在しなければ自動的に生成する（シーンへの手動配置は不要）。
        /// </summary>
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindFirstObjectByType<GameManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject(nameof(GameManager));
                        _instance = go.AddComponent<GameManager>();
                    }
                }

                return _instance;
            }
        }

        private bool _hasPreviousGameRecord;
        private int _previousPlayerCount;
        private HashSet<int> _previousSeatIndices;
        private int _previousLastPlaceSeatIndex;
        private List<int> _lastFinalRankingSeatIndices;

        private bool _quickRestartPending;
        private int _quickRestartPlayerCount;

        /// <summary>
        /// 直近ゲームの最終順位（座席インデックスのリスト、1位から順）。まだ記録が無ければ null。
        /// </summary>
        public IReadOnlyList<int> LastFinalRankingSeatIndices => _lastFinalRankingSeatIndices;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// ゲーム終了時（残り1人になり自動的に最下位が確定したタイミング）に、
        /// 次回の開始プレイヤー決定・結果表示に使う情報を記録する。
        /// </summary>
        /// <param name="playerCount">今回のプレイ人数。</param>
        /// <param name="seatIndices">今回使用した座席インデックスの集合。</param>
        /// <param name="lastPlaceSeatIndex">最後まで手札を持っていた（自動的に最下位が確定した）プレイヤーの座席インデックス。</param>
        /// <param name="finalRankingSeatIndices">最終順位（座席インデックスのリスト、1位から順）。</param>
        public void RecordGameResult(
            int playerCount,
            IEnumerable<int> seatIndices,
            int lastPlaceSeatIndex,
            IReadOnlyList<int> finalRankingSeatIndices)
        {
            _previousPlayerCount = playerCount;
            _previousSeatIndices = new HashSet<int>(seatIndices);
            _previousLastPlaceSeatIndex = lastPlaceSeatIndex;
            _lastFinalRankingSeatIndices = finalRankingSeatIndices != null
                ? new List<int>(finalRankingSeatIndices)
                : null;
            _hasPreviousGameRecord = true;
        }

        /// <summary>
        /// 今回の人数・座席構成が、記録済みの前回の結果と完全一致する場合のみ、
        /// 前回の最下位座席を <paramref name="startingSeatIndex"/> に返して true を返す。
        /// 記録が無い場合（初回）、人数が異なる場合、座席構成（メンバー）が異なる場合は false を返す。
        /// </summary>
        public bool TryGetStartingSeat(int playerCount, IEnumerable<int> seatIndices, out int startingSeatIndex)
        {
            startingSeatIndex = -1;

            if (!_hasPreviousGameRecord || _previousSeatIndices == null || playerCount != _previousPlayerCount)
            {
                return false;
            }

            var currentSeatIndices = new HashSet<int>(seatIndices);
            if (!currentSeatIndices.SetEquals(_previousSeatIndices))
            {
                return false;
            }

            startingSeatIndex = _previousLastPlaceSeatIndex;
            return true;
        }

        /// <summary>
        /// 次に GameScene がロードされた際、人数選択をスキップして前回と同じ人数でゲームを
        /// 自動的に開始するよう予約する。直近のゲーム結果が記録されていなければ何もしない。
        /// </summary>
        public void RequestQuickRestart()
        {
            if (!_hasPreviousGameRecord)
            {
                return;
            }

            _quickRestartPending = true;
            _quickRestartPlayerCount = _previousPlayerCount;
        }

        /// <summary>
        /// クイック再開の予約を明示的に取り消す（人数選択からやり直す場合に呼ぶ）。
        /// </summary>
        public void ClearQuickRestart()
        {
            _quickRestartPending = false;
        }

        /// <summary>
        /// クイック再開の予約があれば人数を取得したうえで、その予約を消費（クリア）して true を返す。
        /// 予約が無ければ false を返す。GameController の起動時に一度だけ呼ばれる想定。
        /// </summary>
        public bool TryConsumeQuickRestart(out int playerCount)
        {
            if (_quickRestartPending)
            {
                playerCount = _quickRestartPlayerCount;
                _quickRestartPending = false;
                return true;
            }

            playerCount = 0;
            return false;
        }
    }
}
