using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public class TreePlacer : EditorWindow
{
    // 木のPrefabの配列
    [SerializeField] private GameObject[] treePrefabs = Array.Empty<GameObject>();
    
    private float _maxTreeInterval = 8.0f;
    private float _maxTreeSize = 1.3f;
    
    //木同士の間隔
    private float _minTreeInterval = 4.5f;
    
    //木の大きさ
    private float _minTreeSize = 0.8f;
    
    // パーリンノイズのオフセット
    private Vector2 _offset = Vector2.zero;
    
    // 原点
    private Vector2 _origin = Vector2.zero;
    
    private GameObject _parent;
    
    // 配置する範囲
    private Vector2 _range = new(100, 100);
    
    
    // パーリンノイズの大きさ
    private float _scale = 0.1f;
    
    // 配置された木のゲームオブジェクトの配列
    private List<GameObject> _trees = new();
    
    // GUIを描画する処理
    private void OnGUI()
    {
        var so = new SerializedObject(this);
        so.Update();
        
        
        // パーリンノイズの大きさを設定するフィールドを表示
        _scale = EditorGUILayout.FloatField("Scale", _scale);
        // パーリンノイズのオフセットを設定するフィールドを表示
        _offset = EditorGUILayout.Vector2Field("Offset", _offset);
        // 配置する範囲を設定するフィールドを表示
        _range = EditorGUILayout.Vector2Field("Range", _range);
        // 原点を設定するフィールドを表示
        _origin = EditorGUILayout.Vector2Field("Origin", _origin);
        //木同士の感覚を設定するフィールドを表示
        _minTreeInterval = EditorGUILayout.FloatField("MinTreeInterval", _minTreeInterval);
        _maxTreeInterval = EditorGUILayout.FloatField("MaxTreeInterval", _maxTreeInterval);
        //木の大きさを設定するフィールドを表示
        _minTreeSize = EditorGUILayout.FloatField("MinTreeSize", _minTreeSize);
        _maxTreeSize = EditorGUILayout.FloatField("MaxTreeSize", _maxTreeSize);
        
        // 木のPrefabの配列を設定するフィールドを表示
        EditorGUILayout.PropertyField(so.FindProperty(nameof(treePrefabs)), true);
        
        _parent = EditorGUILayout.ObjectField("Parent", _parent, typeof(GameObject), true) as GameObject;
        
        // 木を配置するボタンを表示
        if (GUILayout.Button("Place Trees")) OnPlaceButton();
        
        if (GUILayout.Button("Clear cache")) _trees.Clear();
        
        so.ApplyModifiedProperties();
    }
    
    [MenuItem("Tools/TreePlacer")]
    private static void ShowWindow()
    {
        var window = GetWindow<TreePlacer>();
        window.titleContent = new GUIContent("TreePlacer");
        window.Show();
    }
    
    // 木を配置するボタンが押された時の処理
    private void OnPlaceButton()
    {
        // 木を配置する範囲を計算
        var min = _origin - _range * 0.5f;
        var max = _origin + _range * 0.5f;
        
        // 木をすべて削除
        foreach (var tree in _trees) DestroyImmediate(tree);
        
        _trees = new List<GameObject>();
        
        // 木を配置する範囲内を繰り返し
        for (var x = min.x; x < max.x; x += _minTreeInterval)
        for (var y = min.y; y < max.y; y += _minTreeInterval)
        {
            var tmpX = x + Random.Range(_minTreeInterval, _maxTreeInterval);
            var tmpY = y + Random.Range(_minTreeInterval, _maxTreeInterval);
            // パーリンノイズを取得
            var noise = Mathf.PerlinNoise((tmpX + _offset.x) * _scale, (tmpY + _offset.y) * _scale);
            // パーリンノイズが0.5より大きければ木を配置
            if (!(noise > 0.5f)) continue;
            
            // 配置する木のPrefabをランダムに選択
            var prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
            // 木を配置
            var tree = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            tree.transform.parent = _parent.transform;
            tree.transform.position = new Vector3(tmpX, 0, tmpY);
            var randomRotation = new Vector3(0, Random.Range(0, 360), 0);
            tree.transform.Rotate(randomRotation);
            var randomScale = Random.Range(_minTreeSize, _maxTreeSize);
            tree.transform.localScale = new Vector3(randomScale, randomScale, randomScale);
            // 配置された木を保存
            _trees.Add(tree);
        }
    }
}