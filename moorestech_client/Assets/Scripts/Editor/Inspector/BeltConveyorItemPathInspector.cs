using Client.Game.InGame.BlockSystem.StateProcessor.BeltConveyor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ベルトコンベアのアイテムパスを編集するカスタムエディタ
/// Custom editor for editing belt conveyor item paths
/// </summary>
[CustomEditor(typeof(BeltConveyorItemPath))]
public class BeltConveyorItemPathInspector : Editor
{
    private int _selectedPathIndex = -1; // -1 = デフォルトパス
    private string _newStartGuid = "";
    private string _newGoalGuid = "";

    private static readonly Color DefaultPathColor = Color.cyan;
    private static readonly Color SelectedPathColor = Color.yellow;
    private static readonly Color ControlPointColor = Color.red;
    private static readonly Color ControlLineColor = new Color(1f, 0.5f, 0f, 0.7f);

    public override void OnInspectorGUI()
    {
        var pathComponent = target as BeltConveyorItemPath;

        GUILayout.BeginVertical("Belt Conveyor Item Path Editor", "window");
        GUILayout.Space(10);

        DrawDefaultPathSection(pathComponent);
        GUILayout.Space(10);
        DrawPathListSection(pathComponent);
        GUILayout.Space(10);
        DrawAddPathSection(pathComponent);

        GUILayout.EndVertical();
        GUILayout.Space(10);

        base.OnInspectorGUI();

        #region Internal

        void DrawDefaultPathSection(BeltConveyorItemPath component)
        {
            EditorGUILayout.LabelField("Default Path", EditorStyles.boldLabel);

            if (component.DefaultPath == null)
            {
                if (GUILayout.Button("Initialize Default Path"))
                {
                    Undo.RecordObject(component, "Initialize Default Path");
                    component.InitializeDefaultPath();
                    EditorUtility.SetDirty(component);
                }
            }
            else
            {
                bool isSelected = _selectedPathIndex == -1;
                GUI.backgroundColor = isSelected ? Color.yellow : Color.white;

                if (GUILayout.Button(isSelected ? "[Selected] Default Path" : "Select Default Path"))
                {
                    _selectedPathIndex = -1;
                    SceneView.RepaintAll();
                }

                GUI.backgroundColor = Color.white;
            }
        }

        void DrawPathListSection(BeltConveyorItemPath component)
        {
            EditorGUILayout.LabelField("Named Paths", EditorStyles.boldLabel);

            for (int i = 0; i < component.Paths.Count; i++)
            {
                var path = component.Paths[i];
                bool isSelected = _selectedPathIndex == i;

                EditorGUILayout.BeginHorizontal();

                // StartId -> GoalId の形式で表示
                // Display as StartId -> GoalId format
                // guidの文字数を5文字までに
                var displayName = $"{path.StartGuid.Substring(0, 5)} -> {path.GoalGuid.Substring(0, 5)}";
                GUI.backgroundColor = isSelected ? Color.yellow : Color.white;
                if (GUILayout.Button(isSelected ? $"[Selected] {displayName}" : displayName))
                {
                    _selectedPathIndex = i;
                    SceneView.RepaintAll();
                }

                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    Undo.RecordObject(component, "Remove Path");
                    component.RemovePath(i);
                    if (_selectedPathIndex == i) _selectedPathIndex = -1;
                    EditorUtility.SetDirty(component);
                    SceneView.RepaintAll();
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        void DrawAddPathSection(BeltConveyorItemPath component)
        {
            EditorGUILayout.LabelField("Add New Path", EditorStyles.boldLabel);

            // StartIdとGoalIdの入力フィールド
            // Input fields for StartId and GoalId
            _newStartGuid = EditorGUILayout.TextField("Start GUID", _newStartGuid);
            _newGoalGuid = EditorGUILayout.TextField("Goal GUID", _newGoalGuid);

            // 両方が入力されている場合のみ追加可能
            // Only allow adding when both are filled
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_newStartGuid) && string.IsNullOrEmpty(_newGoalGuid)))
            {
                if (GUILayout.Button("Add Path"))
                {
                    Undo.RecordObject(component, "Add Path");
                    component.AddPath(_newStartGuid, _newGoalGuid);
                    _selectedPathIndex = component.Paths.Count - 1;
                    _newStartGuid = "";
                    _newGoalGuid = "";
                    EditorUtility.SetDirty(component);
                    SceneView.RepaintAll();
                }
            }
        }

        #endregion
    }

    private void OnSceneGUI()
    {
        // GameObjectのデフォルトギズモを非表示
        // Hide default GameObject gizmo
        Tools.hidden = true;

        var pathComponent = target as BeltConveyorItemPath;

        DrawAllPaths(pathComponent);

        // 選択されたパスを編集可能にする
        // Make selected path editable
        if (_selectedPathIndex == -1)
        {
            if (pathComponent.DefaultPath != null)
            {
                EditPath(pathComponent.DefaultPath, pathComponent.transform);
            }
        }
        else if (_selectedPathIndex >= 0 && _selectedPathIndex < pathComponent.Paths.Count)
        {
            EditPath(pathComponent.Paths[_selectedPathIndex].BezierPath, pathComponent.transform);
        }

        #region Internal

        void DrawAllPaths(BeltConveyorItemPath component)
        {
            // デフォルトパスを描画
            // Draw default path
            if (component.DefaultPath != null)
            {
                bool isSelected = _selectedPathIndex == -1;
                DrawBezierPath(component.DefaultPath, component.transform,
                    isSelected ? SelectedPathColor : DefaultPathColor);
            }

            // 名前付きパスを描画
            // Draw named paths
            for (int i = 0; i < component.Paths.Count; i++)
            {
                bool isSelected = _selectedPathIndex == i;
                var color = isSelected
                    ? SelectedPathColor
                    : Color.HSVToRGB((float)i / Mathf.Max(1, component.Paths.Count), 0.7f, 0.9f);
                DrawBezierPath(component.Paths[i].BezierPath, component.transform, color);
            }
        }

        void DrawBezierPath(BezierPath path, Transform pathTransform, Color color)
        {
            Vector3 p0 = pathTransform.TransformPoint(path.StartPoint);
            Vector3 p1 = pathTransform.TransformPoint(path.StartControlWorldPosition);
            Vector3 p2 = pathTransform.TransformPoint(path.EndControlWorldPosition);
            Vector3 p3 = pathTransform.TransformPoint(path.EndPoint);

            Handles.DrawBezier(p0, p3, p1, p2, color, null, 3f);

            // 端点を描画
            // Draw endpoints
            Handles.color = color;
            float handleSize = HandleUtility.GetHandleSize(p0) * 0.1f;
            Handles.SphereHandleCap(0, p0, Quaternion.identity, handleSize, EventType.Repaint);
            Handles.SphereHandleCap(0, p3, Quaternion.identity, handleSize, EventType.Repaint);
        }

        void EditPath(BezierPath path, Transform pathTransform)
        {
            EditorGUI.BeginChangeCheck();

            // 始点のハンドル
            // Start point handle
            Vector3 startWorld = pathTransform.TransformPoint(path.StartPoint);
            Vector3 newStartWorld = Handles.PositionHandle(startWorld, Quaternion.identity);

            // 終点のハンドル
            // End point handle
            Vector3 endWorld = pathTransform.TransformPoint(path.EndPoint);
            Vector3 newEndWorld = Handles.PositionHandle(endWorld, Quaternion.identity);

            // 制御点のハンドル（始点側）
            // Control point handle (start side)
            Vector3 startControlWorld = pathTransform.TransformPoint(path.StartControlWorldPosition);
            Handles.color = ControlLineColor;
            Handles.DrawLine(startWorld, startControlWorld);
            Vector3 newStartControlWorld = Handles.PositionHandle(startControlWorld, Quaternion.identity);

            // 制御点のハンドル（終点側）
            // Control point handle (end side)
            Vector3 endControlWorld = pathTransform.TransformPoint(path.EndControlWorldPosition);
            Handles.color = ControlLineColor;
            Handles.DrawLine(endWorld, endControlWorld);
            Vector3 newEndControlWorld = Handles.PositionHandle(endControlWorld, Quaternion.identity);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Edit Belt Conveyor Path");

                // ワールド座標からローカル座標に変換して保存
                // Convert from world to local coordinates and save
                var newStartLocal = pathTransform.InverseTransformPoint(newStartWorld);
                var newEndLocal = pathTransform.InverseTransformPoint(newEndWorld);
                path.SetStartPoint(newStartLocal);
                path.SetEndPoint(newEndLocal);
                path.SetStartControlPoint(pathTransform.InverseTransformPoint(newStartControlWorld) - newStartLocal);
                path.SetEndControlPoint(pathTransform.InverseTransformPoint(newEndControlWorld) - newEndLocal);

                EditorUtility.SetDirty(target);
            }
        }

        #endregion
    }
}
