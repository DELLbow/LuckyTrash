using System.Collections.Generic;
using UnityEngine;

namespace LuckyTrash.Controllers
{
    /// <summary>
    /// シーンをまたいで保持する必要がある、直近のゲーム結果に関する最小限の情報を管理するシングルトン
    /// （<see cref="DontDestroyOnLoad(Object)"/>）。
    /// 現時点では「次回ゲーム開始時に、前回と同じ人数・同じ座席構成であれば前回の最下位だった座席から
    /// 開始する」という判定にのみ使用する。
    /// ResultScene への順位データ受け渡し等は今回のスコープ外だが、将来同じシングルトンを
    /// 拡張していく前提で、記録する情報は最小限に絞ってある。
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
        /// 次回の開始プレイヤー決定に使う最小限の情報を記録する。
        /// </summary>
        /// <param name="playerCount">今回のプレイ人数。</param>
        /// <param name="seatIndices">今回使用した座席インデックスの集合。</param>
        /// <param name="lastPlaceSeatIndex">最後まで手札を持っていた（自動的に最下位が確定した）プレイヤーの座席インデックス。</param>
        public void RecordGameResult(int playerCount, IEnumerable<int> seatIndices, int lastPlaceSeatIndex)
        {
            _previousPlayerCount = playerCount;
            _previousSeatIndices = new HashSet<int>(seatIndices);
            _previousLastPlaceSeatIndex = lastPlaceSeatIndex;
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

            if (!_hasPreviousGameRecord || playerCount != _previousPlayerCount)
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
    }
}
