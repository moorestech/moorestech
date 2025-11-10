using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TransformRounder : EditorWindow
{
    // GameObjectのリスト
    // List of GameObjects
    [SerializeField] private List<GameObject> gameObjects = new();

    private SerializedObject _serializedObject;
    private SerializedProperty _gameObjectsProperty;

    [MenuItem("moorestech/Util/Transform Rounder")]
    private static void ShowWindow()
    {
        var window = GetWindow<TransformRounder>();
        window.titleContent = new GUIContent("Transform Rounder");
        window.Show();
    }

    private void OnEnable()
    {
        _serializedObject = new SerializedObject(this);
        _gameObjectsProperty = _serializedObject.FindProperty(nameof(gameObjects));
    }

    private void OnGUI()
    {
        _serializedObject.Update();

        // GameObjectリストを表示
        // Display GameObject list
        EditorGUILayout.PropertyField(_gameObjectsProperty, true);

        EditorGUILayout.Space();

        // 座標と角度を四捨五入するボタン
        // Button to round positions and rotations
        if (GUILayout.Button("Round Transforms"))
        {
            RoundTransforms();
        }

        _serializedObject.ApplyModifiedProperties();
    }

    #region Internal

    // 各GameObjectの座標と角度を四捨五入
    // Round positions and rotations for each GameObject
    private void RoundTransforms()
    {
        foreach (var go in gameObjects)
        {
            if (go == null) continue;

            // Undo対応
            // Support Undo
            Undo.RecordObject(go.transform, "Round Transform");

            // ローカル座標を四捨五入
            // Round local position
            var pos = go.transform.localPosition;
            pos.x = Mathf.Round(pos.x);
            pos.y = Mathf.Round(pos.y);
            pos.z = Mathf.Round(pos.z);
            go.transform.localPosition = pos;

            // ローカル角度を90度単位で四捨五入
            // Round local rotation to nearest 90 degrees
            var rot = go.transform.localEulerAngles;
            rot.x = Mathf.Round(rot.x / 90f) * 90f;
            rot.y = Mathf.Round(rot.y / 90f) * 90f;
            rot.z = Mathf.Round(rot.z / 90f) * 90f;
            go.transform.localEulerAngles = rot;
        }
    }

    #endregion
}
