using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// テーブルのフェルト面の高さと、その上に置く小物（山札・基準カードスロット・4座席の手札）を
    /// フェルトからどれだけ浮かせるかを1か所で持つ設定アセット。<see cref="TableSurfaceSnap"/> が参照する。
    /// 値を変えると、開いているシーンの <see cref="TableSurfaceSnap"/> すべての高さがエディタ上でそろって更新される。
    /// </summary>
    [CreateAssetMenu(fileName = "TableSurfaceSettings", menuName = "LuckyTrash/Table Surface Settings")]
    public class TableSurfaceSettings : ScriptableObject
    {
        [Tooltip("フェルト面のワールドY座標（テーブル縁のレール上面Y≈1.819ではない）。")]
        [SerializeField] private float _tableSurfaceY = 1.7314f;

        [Tooltip("フェルト面からどれだけ浮かせるか（ワールド単位）。0だと表面が重なってちらつくため、0.002〜0.005程度にする。")]
        [Range(0f, 0.02f)] [SerializeField] private float _heightAboveTable = 0.003f;

        public float TableSurfaceY => _tableSurfaceY;
        public float HeightAboveTable => _heightAboveTable;

        /// <summary>小物を置く高さ（フェルト面＋浮かせ量）。</summary>
        public float PlacementY => _tableSurfaceY + _heightAboveTable;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // OnValidate中にTransformを書き換えると警告が出るため、次のエディタ更新で反映する。
            TableSurfaceSnap.RunOnNextEditorUpdate(() =>
            {
                if (this == null)
                {
                    return;
                }

                foreach (var snap in FindObjectsByType<TableSurfaceSnap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    snap.ApplyIfUsing(this);
                }
            });
        }
#endif
    }
}
