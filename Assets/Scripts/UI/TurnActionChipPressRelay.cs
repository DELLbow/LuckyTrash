using UnityEngine;
using UnityEngine.EventSystems;

namespace LuckyTrash.UI
{
    /// <summary>
    /// ボタンの押下・解放を <see cref="TurnActionChipView"/> に伝え、チップの沈み込みを見せるための中継。
    /// Button と同じ GameObject に付ける（Button がポインターイベントを受け取るため、親には伝わらない）。
    /// クリックの処理そのものには関与しない。
    /// </summary>
    public class TurnActionChipPressRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private TurnActionChipView _chip;

        public void SetChip(TurnActionChipView chip)
        {
            _chip = chip;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_chip != null)
            {
                _chip.SetPressed(true);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_chip != null)
            {
                _chip.SetPressed(false);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_chip != null)
            {
                _chip.SetPressed(false);
            }
        }
    }
}
