using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI
{
    /// <summary>
    /// あがりの金の紙幣が中央で止まった瞬間に、その後ろから小さな紙幣を四方へはじけさせる演出。
    /// 小さな紙幣は手番の紙幣のデザインを簡略化したもの（淡いグリーンの紙・細い枠・楕円）で、
    /// くるくる回りながら放物線を描いて飛び、ひらひら揺れながら落ちて薄くなって消える。
    /// Screen Space Overlay の Canvas 上で再生し、盤面（3D）には置かない。
    /// 小さな紙幣は最初に必要な枚数だけ作っておき、使い回す（毎回の生成・破棄をしない）。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MoneyBurstView : MonoBehaviour
    {
        private class Bill
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Angle;
            public float Spin;
            public float FlipPhase;
            public float FlipSpeed;
            public float SwayPhase;
            public float Age;
            public float Life;
            public bool Active;
        }

        [Header("素材・色")]
        [Tooltip("角丸の板（9スライス）。Assets/Textures/RoundedPanel.png。")]
        [SerializeField] private Sprite _roundedSprite;
        [Tooltip("円（UI/Skin/Knob）。小さな紙幣の中央の楕円に使う。")]
        [SerializeField] private Sprite _circleSprite;
        [SerializeField] private Color _paperColor = new Color(0.86f, 0.93f, 0.83f);
        [SerializeField] private Color _inkColor = new Color(0.10f, 0.27f, 0.18f);

        [Header("枚数")]
        [Tooltip("自分があがったときの枚数。")]
        [SerializeField] private int _humanCount = 20;
        [Tooltip("CPUがあがったときの枚数。")]
        [SerializeField] private int _cpuCount = 10;
        [Tooltip("1位のときに追加する枚数。")]
        [SerializeField] private int _firstPlaceBonus = 8;

        [Header("飛び方")]
        [Tooltip("小さな紙幣の大きさ。")]
        [SerializeField] private Vector2 _billSize = new Vector2(64f, 28f);
        [Tooltip("はじける初速の範囲（Canvas単位/秒）。")]
        [SerializeField] private Vector2 _speedRange = new Vector2(380f, 760f);
        [Tooltip("自分があがったときの、飛ぶ範囲（初速）の倍率。")]
        [SerializeField] private float _humanSpreadScale = 1.3f;
        [Tooltip("下向きの重力（Canvas単位/秒²）。")]
        [SerializeField] private float _gravity = 1100f;
        [Tooltip("空気抵抗（大きいほど早く減速して、ひらひら落ちる）。")]
        [SerializeField] private float _drag = 1.6f;
        [Tooltip("落ちるときの左右の揺れ幅（Canvas単位/秒）。")]
        [SerializeField] private float _swaySpeed = 90f;
        [Tooltip("回転の速さの範囲（度/秒）。")]
        [SerializeField] private Vector2 _spinRange = new Vector2(180f, 540f);
        [Tooltip("消えるまでの時間の範囲（秒）。最後の4割で薄くなって消える。")]
        [SerializeField] private Vector2 _lifeRange = new Vector2(1.3f, 1.8f);
        [Tooltip("あらかじめ作っておく小さな紙幣の数（同時に出せる最大枚数）。")]
        [SerializeField] private int _poolSize = 32;

        private readonly List<Bill> _bills = new List<Bill>();
        private float _remaining;

        /// <summary>はじけた紙幣がすべて消えるまでの残り時間（秒）。0なら再生していない。</summary>
        public float RemainingSeconds => _remaining;

        private void Awake()
        {
            for (int i = 0; i < Mathf.Max(1, _poolSize); i++)
            {
                _bills.Add(CreateBill(i));
            }
        }

        /// <summary>
        /// worldCenter（Canvas上の点のワールド座標）を中心に、大きさ emitSize の紙幣の縁のあたりから
        /// 小さな紙幣を外向きにはじけさせる（中心から出すと手前の紙幣に隠れてしまうため）。
        /// 戻り値は全部消えるまでの時間（秒）。
        /// </summary>
        /// <param name="countScale">はじける枚数の倍率（ゲーム終了時の1位の発表で多めにするとき用。既定は 1）。</param>
        public float Play(Vector3 worldCenter, Vector2 emitSize, bool isHuman, bool firstPlace, float countScale = 1f)
        {
            var root = (RectTransform)transform;
            Vector2 center = root.InverseTransformPoint(worldCenter);
            int count = Mathf.RoundToInt(((isHuman ? _humanCount : _cpuCount) + (firstPlace ? _firstPlaceBonus : 0)) * Mathf.Max(0f, countScale));
            // 用意した数より多く出すときは、足りない分を作り足す（次からも使い回す）。
            while (_bills.Count < count)
            {
                _bills.Add(CreateBill(_bills.Count));
            }
            float spread = isHuman ? _humanSpreadScale : 1f;
            float longest = 0f;

            int started = 0;
            foreach (var bill in _bills)
            {
                if (started >= count)
                {
                    break;
                }
                if (bill.Active)
                {
                    continue;
                }

                // 上向きを中心に、四方へ散らす（真下へは飛ばさない）。紙幣の縁のあたり（楕円上）から外向きに出す。
                float angle = Random.Range(-160f, 160f) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                float speed = Random.Range(_speedRange.x, _speedRange.y) * spread;
                bill.Position = center + new Vector2(direction.x * emitSize.x * 0.42f, direction.y * emitSize.y * 0.42f);
                bill.Velocity = direction * speed;
                bill.Angle = Random.Range(0f, 360f);
                bill.Spin = Random.Range(_spinRange.x, _spinRange.y) * (Random.value < 0.5f ? -1f : 1f);
                bill.FlipPhase = Random.Range(0f, Mathf.PI * 2f);
                bill.FlipSpeed = Random.Range(6f, 12f);
                bill.SwayPhase = Random.Range(0f, Mathf.PI * 2f);
                bill.Age = 0f;
                bill.Life = Random.Range(_lifeRange.x, _lifeRange.y);
                bill.Active = true;
                bill.Rect.gameObject.SetActive(true);
                longest = Mathf.Max(longest, bill.Life);
                started++;
            }

            _remaining = Mathf.Max(_remaining, longest);
            return longest;
        }

        /// <summary>舞っている紙幣をすべてすぐに消す（演出を飛ばしたとき用）。</summary>
        public void Clear()
        {
            foreach (var bill in _bills)
            {
                bill.Active = false;
                bill.Rect.gameObject.SetActive(false);
            }
            _remaining = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _remaining = Mathf.Max(0f, _remaining - dt);

            foreach (var bill in _bills)
            {
                if (!bill.Active)
                {
                    continue;
                }

                bill.Age += dt;
                if (bill.Age >= bill.Life)
                {
                    bill.Active = false;
                    bill.Rect.gameObject.SetActive(false);
                    continue;
                }

                // 放物線（重力）＋空気抵抗。落ち始めたら左右にひらひら揺れる。
                bill.Velocity += Vector2.down * _gravity * dt;
                bill.Velocity *= Mathf.Exp(-_drag * dt);
                float sway = bill.Velocity.y < 0f ? Mathf.Sin(bill.Age * 5f + bill.SwayPhase) * _swaySpeed : 0f;
                bill.Position += (bill.Velocity + new Vector2(sway, 0f)) * dt;
                bill.Angle += bill.Spin * dt;

                bill.Rect.anchoredPosition = bill.Position;
                bill.Rect.localRotation = Quaternion.Euler(0f, 0f, bill.Angle);
                // 紙が裏返るように、横幅を周期的に縮める。
                float flip = Mathf.Cos(bill.Age * bill.FlipSpeed + bill.FlipPhase);
                bill.Rect.localScale = new Vector3(Mathf.Lerp(0.25f, 1f, Mathf.Abs(flip)), 1f, 1f);

                float t = bill.Age / bill.Life;
                bill.Group.alpha = t < 0.6f ? 1f : Mathf.Clamp01((1f - t) / 0.4f);
            }
        }

        /// <summary>手番の紙幣を小さく簡略化した1枚（紙・細い枠・中央の楕円）を作る。</summary>
        private Bill CreateBill(int index)
        {
            var go = new GameObject($"Bill_{index}", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = _billSize;
            var group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var paper = CreateImage("Paper", rect, _roundedSprite, _paperColor, Vector2.zero, _billSize);
            paper.pixelsPerUnitMultiplier = 6f;
            var edge = paper.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.8f);
            edge.effectDistance = new Vector2(1f, -1f);

            // 内側の細い枠（上下の線）と中央の楕円。
            var line = new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.55f);
            CreateImage("TopLine", rect, null, line, new Vector2(0f, _billSize.y / 2f - 4f), new Vector2(_billSize.x - 8f, 1f));
            CreateImage("BottomLine", rect, null, line, new Vector2(0f, -_billSize.y / 2f + 4f), new Vector2(_billSize.x - 8f, 1f));
            CreateImage("Oval", rect, _circleSprite, new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.7f), Vector2.zero, new Vector2(_billSize.y * 0.62f, _billSize.y * 0.78f));
            CreateImage("OvalField", rect, _circleSprite, Color.Lerp(_paperColor, Color.white, 0.4f), Vector2.zero, new Vector2(_billSize.y * 0.5f, _billSize.y * 0.66f));

            go.SetActive(false);
            return new Bill { Rect = rect, Group = group };
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                image.type = Image.Type.Sliced;
            }
            var r = image.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size;
            r.anchoredPosition = position;
            return image;
        }
    }
}
