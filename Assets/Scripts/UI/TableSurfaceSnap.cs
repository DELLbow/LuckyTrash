using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// テーブル上に置く小物（山札・基準カードスロットなど）の高さを、フェルト面から指定した分だけ
    /// 浮かせた位置に合わせるエディタ用の補助コンポーネント。Inspectorで値を変えると、このTransformの
    /// ワールドY座標だけを書き換える（X/Zと回転には触れない）。
    /// 実行時には何もしない（シーンに保存された位置をそのまま使う）ため、
    /// <see cref="ReferenceCardDrawView"/> が起動時に記録する定位置とも食い違わない。
    /// </summary>
    public class TableSurfaceSnap : MonoBehaviour
    {
        [Tooltip("フェルト面のワールドY座標（テーブル縁のレール上面Y≈1.819ではない）。")]
        [SerializeField] private float _tableSurfaceY = 1.7314f;

        [Tooltip("フェルト面からどれだけ浮かせるか（ワールド単位）。0だと表面が重なってちらつくため、0.002〜0.005程度にする。")]
        [Range(0f, 0.02f)] [SerializeField] private float _heightAboveTable = 0.003f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // OnValidate中にTransformを書き換えると警告が出るため、次のエディタ更新で反映する。
            UnityEditor.EditorApplication.delayCall += Apply;
        }

        private void Apply()
        {
            if (this == null || Application.isPlaying)
            {
                return;
            }

            float targetY = _tableSurfaceY + _heightAboveTable;
            if (Mathf.Approximately(transform.position.y, targetY))
            {
                return;
            }

            UnityEditor.Undo.RecordObject(transform, "Snap to table surface");
            var position = transform.position;
            transform.position = new Vector3(position.x, targetY, position.z);
        }
#endif
    }
}
