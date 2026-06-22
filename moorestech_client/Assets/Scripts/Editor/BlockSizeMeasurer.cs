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

    // ギズモ色：計測セル枠（緑）
    // Gizmo color: cell box (green)
    private static readonly Color CellBoxColor = new(0.2f, 1f, 0.3f, 1f);

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
        }
    }

    // 原点から正方向へ広がるボックスを半透明の面＋枠線で描画
    // Draw a box spanning origin toward positive axes with translucent faces and outline
    private static void DrawLocalBox(Vector3 size, Color color)
    {
        var p000 = Vector3.zero;
        var p100 = new Vector3(size.x, 0, 0);
        var p010 = new Vector3(0, size.y, 0);
        var p001 = new Vector3(0, 0, size.z);
        var p110 = new Vector3(size.x, size.y, 0);
        var p101 = new Vector3(size.x, 0, size.z);
        var p011 = new Vector3(0, size.y, size.z);
        var p111 = new Vector3(size.x, size.y, size.z);

        var faceColor = new Color(color.r, color.g, color.b, 0.1f);

        // 6面を半透明で塗り、枠線を本来の色で描画
        // Fill all 6 faces translucently and outline them in the base color
        Quad(p000, p100, p101, p001); // bottom (y=0)
        Quad(p010, p110, p111, p011); // top (y=max)
        Quad(p000, p100, p110, p010); // front (z=0)
        Quad(p001, p101, p111, p011); // back (z=max)
        Quad(p000, p010, p011, p001); // left (x=0)
        Quad(p100, p110, p111, p101); // right (x=max)

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Handles.DrawSolidRectangleWithOutline(new[] { a, b, c, d }, faceColor, color);
        }
    }

    // ルート配下のメッシュから正方向のローカル最大値を計測（0以下は無視）
    // Measure positive local max from child meshes (ignore non-positive region)
    private static Vector3 MeasureLocalMax(GameObject root)
    {
        var worldToRoot = root.transform.worldToLocalMatrix;
        var maxLocal = Vector3.zero;
        var found = false;

        // MeshRenderer：MeshFilterのタイトなメッシュ境界を対象
        // MeshRenderer: use the tight mesh bounds from MeshFilter
        foreach (var meshFilter in root.GetComponentsInChildren<MeshFilter>(false))
        {
            var renderer = meshFilter.GetComponent<MeshRenderer>();
            if (meshFilter.sharedMesh == null || renderer == null || !renderer.enabled) continue;
            Accumulate(meshFilter.sharedMesh.bounds, meshFilter.transform.localToWorldMatrix);
        }

        // SkinnedMeshRenderer：共有メッシュ境界を対象（ParticleSystem等は除外）
        // SkinnedMeshRenderer: use shared mesh bounds (particle/line/trail excluded)
        foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (skinned.sharedMesh == null || !skinned.enabled) continue;
            Accumulate(skinned.sharedMesh.bounds, skinned.transform.localToWorldMatrix);
        }

        return found ? maxLocal : Vector3.one;

        #region Internal

        // メッシュローカル境界の8頂点を正確な行列でルートローカルへ変換し集計
        // Transform 8 mesh-local-bounds corners into root-local space and accumulate
        void Accumulate(Bounds meshBounds, Matrix4x4 meshToWorld)
        {
            var matrix = worldToRoot * meshToWorld;
            var min = meshBounds.min;
            var max = meshBounds.max;
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                maxLocal = Vector3.Max(maxLocal, matrix.MultiplyPoint3x4(corner));
            }
            found = true;
        }

        #endregion
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
