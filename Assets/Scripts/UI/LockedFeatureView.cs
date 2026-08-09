using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 「近日公開」な機能を表す非活性ボタンの共通スタイルを適用するコンポーネント。
    /// アタッチした Button を interactable=false にし、対象アイコン/ラベルの彩度を落とし、
    /// 「近日公開」バッジを表示する。TitleScene下部メニュー（ランキング/友達/戦績/ショップ）や
    /// プロフィールパネルのCM視聴ギフトボタン、モード選択カード（全国対戦/友達と対戦）など、
    /// 非活性ボタン全般で使い回す。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class LockedFeatureView : MonoBehaviour
    {
        [Tooltip("「近日公開」ラベル/バッジを表示するGameObject。")]
        [SerializeField] private GameObject _comingSoonBadge;

        [Tooltip("彩度を落とす対象（アイコン画像、ラベルテキスト等）。")]
        [SerializeField] private Graphic[] _grayOutTargets;

        [SerializeField] private Color _grayOutColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        private void Awake()
        {
            ApplyLockedStyle();
        }

        /// <summary>
        /// 非活性（近日公開）スタイルを適用する。
        /// interactable=false / 彩度を落とした配色 / 「近日公開」バッジ表示、の3点セット。
        /// </summary>
        public void ApplyLockedStyle()
        {
            var button = GetComponent<Button>();
            if (button != null)
            {
                button.interactable = false;
            }

            if (_comingSoonBadge != null)
            {
                _comingSoonBadge.SetActive(true);
            }

            if (_grayOutTargets != null)
            {
                foreach (var graphic in _grayOutTargets)
                {
                    if (graphic != null)
                    {
                        graphic.color = _grayOutColor;
                    }
                }
            }
        }
    }
}
