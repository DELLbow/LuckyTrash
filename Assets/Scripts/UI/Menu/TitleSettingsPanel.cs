using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI.Menu
{
    /// <summary>
    /// タイトル画面の「設定」のポップアップに、「ゲームを終了」のボタンを置く（押すと確認を出してからアプリを閉じる）。
    /// 今は他に設定がないので、パネルを少し小さくし、音の設定は今後追加する旨を小さく添えて、空っぽに見えないようにする。
    /// ボタンは既存の CLOSE ボタンを複製して作る（タイトル画面のボタンと同じ見た目）。
    /// </summary>
    public class TitleSettingsPanel : MonoBehaviour
    {
        [Tooltip("設定のパネル（CardPanel）。")]
        [SerializeField] private RectTransform _cardPanel;
        [Tooltip("既存の CLOSE ボタン（複製して「ゲームを終了」を作る。CLOSE は一番下に残す）。")]
        [SerializeField] private Button _closeButton;
        [Tooltip("「ゲームを終了しますか？」の確認（試合中のメニューと同じ部品）。")]
        [SerializeField] private ConfirmDialogView _confirm;
        [SerializeField] private TMP_FontAsset _font;

        [Header("レイアウト")]
        [SerializeField] private Vector2 _panelSize = new Vector2(640f, 440f);
        [SerializeField] private string _quitLabel = "ゲームを終了";
        [SerializeField] private string _quitConfirmMessage = "ゲームを終了しますか？";
        [SerializeField] private string _note = "サウンドなどの設定は、今後追加予定です";
        [SerializeField] private Color _noteColor = new Color(0.45f, 0.45f, 0.48f);

        private bool _built;

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            if (_built || _cardPanel == null || _closeButton == null)
            {
                return;
            }
            _built = true;
            _cardPanel.sizeDelta = _panelSize;

            var closeRect = (RectTransform)_closeButton.transform;
            var quit = Instantiate(_closeButton.gameObject, _closeButton.transform.parent);
            quit.name = "QuitButton";
            var quitRect = (RectTransform)quit.transform;
            quitRect.anchoredPosition = closeRect.anchoredPosition + new Vector2(0f, closeRect.sizeDelta.y + 70f);
            quitRect.sizeDelta = new Vector2(closeRect.sizeDelta.x + 60f, closeRect.sizeDelta.y);
            var label = quit.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = _quitLabel;
            }
            var quitButton = quit.GetComponent<Button>();
            quitButton.onClick = new Button.ButtonClickedEvent();
            quitButton.onClick.AddListener(() =>
            {
                if (_confirm != null)
                {
                    _confirm.Show(_quitConfirmMessage, "終了する", "やめる", AppQuit.Quit, null);
                }
                else
                {
                    AppQuit.Quit();
                }
            });

            var font = _font != null ? _font : label != null ? label.font : null;
            var note = MenuUiKit.CreateText("Note", _cardPanel, font, _note, 22f, _noteColor, TextAlignmentOptions.Center);
            // 「ゲームを終了」と同じ基準で位置を決め、そのすぐ上（見出しとの間）に置く。
            note.rectTransform.anchorMin = quitRect.anchorMin;
            note.rectTransform.anchorMax = quitRect.anchorMax;
            note.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            note.rectTransform.sizeDelta = new Vector2(_panelSize.x - 60f, 30f);
            note.rectTransform.anchoredPosition = new Vector2(0f, quitRect.anchoredPosition.y + (0.5f - quitRect.pivot.y) * quitRect.sizeDelta.y + quitRect.sizeDelta.y / 2f + 34f);
        }
    }
}
