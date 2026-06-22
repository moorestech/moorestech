using System.Text;
using UnityEditor;
using UnityEngine;

public class BlockSizeMeasurer : EditorWindow
{
    // 計測対象のルートGameObject
    // Root GameObject to measure
    [SerializeField] private GameObject _targetRoot;

    private Vector3Int _measuredSize = Vector3Int.one;
    private bool _hasResult;

    [MenuItem("moorestech/Util/Block Size Measurer")]
    private static void ShowWindow()
    {
        var window = GetWindow<BlockSizeMeasurer>();
        window.titleContent = new GUIContent("Block Size Measurer");
        window.Show();
    }

    private void OnGUI()
    {
        // 対象GameObjectの選択フィールド
        // Target GameObject selection field
        _targetRoot = (GameObject)EditorGUILayout.ObjectField("Target Root", _targetRoot, typeof(GameObject), true);

        EditorGUILayout.Space();

        // サイズを計測するボタン（対象未指定時は無効）
        // Measure button (disabled when target is unset)
        using (new EditorGUI.DisabledScope(_targetRoot == null))
        {
            if (GUILayout.Button("Measure Size"))
            {
                _measuredSize = MeasureBlockSize(_targetRoot);
                _hasResult = true;
            }
        }

        if (!_hasResult) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Block Size", _measuredSize.ToString());

        // 計測結果を貼り付け用JSONとしてクリップボードへコピー
        // Copy measured result to clipboard as paste-ready JSON
        if (GUILayout.Button("Copy JSON"))
        {
            GUIUtility.systemCopyBuffer = BuildVector3IntJson(_measuredSize);
            ShowNotification(new GUIContent("Copied!"));
        }
    }

    // ルート配下のRendererからローカル空間のサイズをセル数で計測
    // Measure cell size from child renderers in root-local space
    private static Vector3Int MeasureBlockSize(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(false);

        // レンダラーが無ければ最小サイズへフォールバック
        // Fall back to minimum size when no renderer exists
        if (renderers.Length == 0) return Vector3Int.one;

        var rootTransform = root.transform;
        var maxLocal = Vector3.zero;

        foreach (var meshRenderer in renderers)
        {
            // ワールド境界の8頂点をルートローカルへ変換し軸ごと最大値を集計
            // Convert 8 world-bounds corners into root-local space and accumulate per-axis max
            var bounds = meshRenderer.bounds;
            var min = bounds.min;
            var max = bounds.max;
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                var local = rootTransform.InverseTransformPoint(corner);
                maxLocal = Vector3.Max(maxLocal, local);
            }
        }

        // 0以下の領域は無視（maxLocal初期値0）し、Ceil・最小1でセル数化
        // Ignore non-positive region (maxLocal starts at 0), convert with Ceil and min 1
        return new Vector3Int(
            ToCellCount(maxLocal.x),
            ToCellCount(maxLocal.y),
            ToCellCount(maxLocal.z));
    }

    private static int ToCellCount(float size)
    {
        return Mathf.Max(1, Mathf.CeilToInt(size));
    }

    // mooreseditorの貼り付け形式（value＋vector3Intスキーマ）を生成
    // Build mooreseditor paste format (value plus vector3Int schema)
    private static string BuildVector3IntJson(Vector3Int size)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"value\": [");
        sb.AppendLine($"    {size.x},");
        sb.AppendLine($"    {size.y},");
        sb.AppendLine($"    {size.z}");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"schema\": {");
        sb.AppendLine("    \"type\": {");
        sb.AppendLine("      \"type\": \"vector3Int\",");
        sb.AppendLine("      \"default\": [");
        sb.AppendLine("        1,");
        sb.AppendLine("        1,");
        sb.AppendLine("        1");
        sb.AppendLine("      ]");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.Append("}");
        return sb.ToString();
    }
}
