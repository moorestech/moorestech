using System.Text;
using UnityEditor;
using UnityEngine;

public class BlockSizeMeasurer : EditorWindow
{
    // 計測対象のルートGameObject
    // Root GameObject to measure
    [SerializeField] private GameObject _targetRoot;

    private Vector3Int _measuredSize = Vector3Int.one;
    private Vector3 _measuredBounds = Vector3.one;
    private bool _hasResult;
    private bool _showGizmo = true;

    // ギズモ色：計測セル枠（緑）と実測境界（黄）
    // Gizmo colors: cell box (green) and actual bounds (yellow)
    private static readonly Color CellBoxColor = new(0.2f, 1f, 0.3f, 1f);
    private static readonly Color BoundsBoxColor = new(1f, 0.8f, 0.1f, 1f);

    [MenuItem("moorestech/Util/Block Size Measurer")]
    private static void ShowWindow()
    {
        var window = GetWindow<BlockSizeMeasurer>();
        window.titleContent = new GUIContent("Block Size Measurer");
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
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
                _measuredBounds = MeasureLocalMax(_targetRoot);
                _measuredSize = ToCellSize(_measuredBounds);
                _hasResult = true;
                _showGizmo = true;
                SceneView.RepaintAll();
            }
        }

        if (!_hasResult) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Block Size", _measuredSize.ToString());
        EditorGUILayout.LabelField("Actual Bounds", _measuredBounds.ToString("F3"));

        // ギズモ表示トグル（変更時はSceneビューを再描画）
        // Gizmo toggle (repaint Scene view on change)
        EditorGUI.BeginChangeCheck();
        _showGizmo = EditorGUILayout.Toggle("Show Gizmo", _showGizmo);
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

        // 計測結果を貼り付け用JSONとしてクリップボードへコピー
        // Copy measured result to clipboard as paste-ready JSON
        if (GUILayout.Button("Copy JSON"))
        {
            GUIUtility.systemCopyBuffer = BuildVector3IntJson(_measuredSize);
            ShowNotification(new GUIContent("Copied!"));
        }
    }

    // ルートのローカル空間で原点から正方向へ枠を描画し適正サイズを確認
    // Draw boxes from local origin toward positive axes to verify the size
    private void OnSceneGUI(SceneView sceneView)
    {
        if (!_showGizmo || !_hasResult || _targetRoot == null) return;

        using (new Handles.DrawingScope(_targetRoot.transform.localToWorldMatrix))
        {
            // 計測セルサイズの外枠
            // Outer box of the measured cell size
            DrawLocalBox(new Vector3(_measuredSize.x, _measuredSize.y, _measuredSize.z), CellBoxColor);

            // Rendererの実測境界
            // Actual renderer bounds
            DrawLocalBox(_measuredBounds, BoundsBoxColor);
        }
    }

    private static void DrawLocalBox(Vector3 size, Color color)
    {
        Handles.color = color;
        Handles.DrawWireCube(size * 0.5f, size);
    }

    // ルート配下のRendererから正方向のローカル最大値を計測（0以下は無視）
    // Measure positive local max from child renderers (ignore non-positive region)
    private static Vector3 MeasureLocalMax(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(false);
        if (renderers.Length == 0) return Vector3.one;

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

        return maxLocal;
    }

    // ローカル最大値をCeil・最小1でセル数化
    // Convert local max to cell count with Ceil and min 1
    private static Vector3Int ToCellSize(Vector3 localMax)
    {
        return new Vector3Int(ToCellCount(localMax.x), ToCellCount(localMax.y), ToCellCount(localMax.z));
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
