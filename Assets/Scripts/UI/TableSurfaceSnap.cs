using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// テーブル上に置く小物（山札・基準カードスロット・4座席の手札のアンカーなど）の高さを、フェルト面から
    /// 少しだけ浮かせた位置に合わせるエディタ用の補助コンポーネント。高さの値は共通の設定アセット
    /// （<see cref="TableSurfaceSettings"/>）から読み、このTransformのワールドY座標だけを書き換える
    /// （X/Zと回転には触れない）。
    /// 実行時には何もしない（シーンに保存された位置をそのまま使う）ため、
    /// <see cref="ReferenceCardDrawView"/> が起動時に記録する定位置とも食い違わない。
    /// </summary>
    public class TableSurfaceSnap : MonoBehaviour
    {
        [Tooltip("フェルト面の高さと浮かせ量を持つ共通の設定アセット。山札・基準カードスロット・4座席の手札で同じものを使う。")]
        [SerializeField] private TableSurfaceSettings _settings;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // OnValidate中にTransformを書き換えると警告が出るため、次のエディタ更新で反映する。
            RunOnNextEditorUpdate(Apply);
        }

        /// <summary>
        /// 次のエディタ更新で1回だけ action を実行する。EditorApplication.delayCall はエディタが
        /// フォーカスを失っている間は実行されないことがあるため、EditorApplication.update を使う。
        /// </summary>
        internal static void RunOnNextEditorUpdate(System.Action action)
        {
            UnityEditor.EditorApplication.CallbackFunction callback = null;
            callback = () =>
            {
                UnityEditor.EditorApplication.update -= callback;
                action();
            };
            UnityEditor.EditorApplication.update += callback;
        }

        /// <summary>指定の設定アセットを参照している場合だけ、高さを合わせ直す（設定アセット側の変更時に呼ばれる）。</summary>
        public void ApplyIfUsing(TableSurfaceSettings settings)
        {
            if (_settings == settings)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (this == null || _settings == null || Application.isPlaying)
            {
                return;
            }

            float targetY = _settings.PlacementY;
            if (Mathf.Approximately(transform.position.y, targetY))
            {
                return;
            }

            UnityEditor.Undo.RecordObject(transform, "Snap to table surface");
            var position = transform.position;
            transform.position = new Vector3(position.x, targetY, position.z);
            UnityEditor.EditorUtility.SetDirty(transform);
        }
#endif
    }
}
