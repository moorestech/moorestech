using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class TreeExporter : EditorWindow
{
    // UI Elements
    private ObjectField _terrainField;
    private DropdownField _treeTypeDropdown;
    private ObjectField _targetPrefabField;
    private ObjectField _parentTransformField;
    private Button _exportButton;
    private Label _infoLabel;
    
    // Data
    private Terrain _selectedTerrain;
    private List<TreePrototype> _treePrototypes;
    private int _selectedPrototypeIndex = -1;
    
    [MenuItem("moorestech/Terrain/TreeExporter")]
    public static void ShowWindow()
    {
        TreeExporter wnd = GetWindow<TreeExporter>();
        wnd.titleContent = new GUIContent("Tree Exporter");
        wnd.minSize = new Vector2(350, 350);
    }
    
    public void CreateGUI()
    {
        // ルートビジュアル要素の取得
        VisualElement root = rootVisualElement;
        
        // スタイルの設定
        root.style.paddingTop = 10;
        root.style.paddingBottom = 10;
        root.style.paddingLeft = 10;
        root.style.paddingRight = 10;
        
        // --- 1. UIパーツの初期化と配置 (先にすべて生成する) ---
        
        // タイトル
        var title = new Label("Terrain Tree Exporter")
        {
            style = {fontSize = 18, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 10}
        };
        root.Add(title);
        
        // Terrain Field
        _terrainField = new ObjectField("Target Terrain")
        {
            objectType = typeof(Terrain),
            allowSceneObjects = true
        };
        root.Add(_terrainField);
        
        // Tree Type Dropdown
        _treeTypeDropdown = new DropdownField("Select Tree Type");
        _treeTypeDropdown.SetEnabled(false);
        root.Add(_treeTypeDropdown);
        
        // 区切り線
        root.Add(CreateDivider());
        
        // Target Prefab
        _targetPrefabField = new ObjectField("Place Prefab")
        {
            objectType = typeof(GameObject),
            allowSceneObjects = false
        };
        root.Add(_targetPrefabField);
        
        // Parent Transform
        _parentTransformField = new ObjectField("Parent Transform (Optional)")
        {
            objectType = typeof(Transform),
            allowSceneObjects = true
        };
        root.Add(_parentTransformField);
        
        // 区切り線
        root.Add(CreateDivider());
        
        // Export Button
        _exportButton = new Button(OnExportClicked)
        {
            text = "Export Trees to GameObjects",
            style = {height = 30, marginTop = 10}
        };
        root.Add(_exportButton);
        
        // Info / Error Label
        _infoLabel = new Label("")
        {
            style = {marginTop = 5, color = Color.yellow, whiteSpace = WhiteSpace.Normal}
        };
        root.Add(_infoLabel);
        
        
        // --- 2. イベント登録と初期ロジック実行 (UI生成後に実行) ---
        
        // Terrain変更イベント
        _terrainField.RegisterValueChangedCallback(evt => OnTerrainChanged((Terrain) evt.newValue));
        
        // Dropdown変更イベント
        _treeTypeDropdown.RegisterValueChangedCallback(evt =>
        {
            _selectedPrototypeIndex = _treeTypeDropdown.index;
            UpdatePrefabFieldIfPossible();
        });
        
        // 現在選択中のオブジェクトがTerrainなら自動セット
        // 注意: すべてのUI変数が初期化された後に呼ぶ必要がある
        if (Selection.activeGameObject != null)
        {
            var terrain = Selection.activeGameObject.GetComponent<Terrain>();
            if (terrain != null)
            {
                _terrainField.value = terrain;
                // イベントコールバック経由で OnTerrainChanged が呼ばれる
            }
        }
    }
    
    private VisualElement CreateDivider()
    {
        var divider = new VisualElement();
        divider.style.height = 1;
        divider.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        divider.style.marginTop = 10;
        divider.style.marginBottom = 10;
        return divider;
    }
    
    private void OnTerrainChanged(Terrain terrain)
    {
        _selectedTerrain = terrain;
        _treePrototypes = new List<TreePrototype>();
        _selectedPrototypeIndex = -1;
        
        // UIの安全なクリア処理
        if (_treeTypeDropdown != null)
        {
            _treeTypeDropdown.choices.Clear();
            _treeTypeDropdown.value = null;
            _treeTypeDropdown.SetEnabled(false);
        }
        
        if (terrain == null || terrain.terrainData == null)
        {
            if (_infoLabel != null) _infoLabel.text = "Please select a valid Terrain.";
            return;
        }
        
        _treePrototypes = terrain.terrainData.treePrototypes.ToList();
        
        if (_treePrototypes.Count == 0)
        {
            if (_infoLabel != null) _infoLabel.text = "No trees registered on this Terrain.";
            return;
        }
        
        // ドロップダウンの選択肢を作成
        var choices = new List<string>();
        for (int i = 0; i < _treePrototypes.Count; i++)
        {
            var prefabName = _treePrototypes[i].prefab != null ? _treePrototypes[i].prefab.name : "Unknown Mesh";
            choices.Add($"[{i}] {prefabName}");
        }
        
        if (_treeTypeDropdown != null)
        {
            _treeTypeDropdown.choices = choices;
            _treeTypeDropdown.SetEnabled(true);
            _treeTypeDropdown.index = 0; // デフォルトで最初を選択
        }
        
        _selectedPrototypeIndex = 0;
        UpdatePrefabFieldIfPossible();
        
        if (_infoLabel != null) _infoLabel.text = "";
    }
    
    private void UpdatePrefabFieldIfPossible()
    {
        if (_selectedTerrain == null || _selectedPrototypeIndex == -1 || _treePrototypes == null || _selectedPrototypeIndex >= _treePrototypes.Count) return;
        
        var prototype = _treePrototypes[_selectedPrototypeIndex];
        if (prototype.prefab != null && _targetPrefabField != null)
        {
            _targetPrefabField.value = prototype.prefab;
        }
    }
    
    private void OnExportClicked()
    {
        if (_selectedTerrain == null)
        {
            EditorUtility.DisplayDialog("Error", "Terrain is not selected.", "OK");
            return;
        }
        if (_selectedPrototypeIndex < 0)
        {
            EditorUtility.DisplayDialog("Error", "Tree type is not selected.", "OK");
            return;
        }
        if (_targetPrefabField.value == null)
        {
            EditorUtility.DisplayDialog("Error", "Target Prefab is not selected.", "OK");
            return;
        }
        
        GameObject targetPrefab = (GameObject) _targetPrefabField.value;
        Transform parentTransform = (Transform) _parentTransformField.value;
        TerrainData data = _selectedTerrain.terrainData;
        
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Export Terrain Trees");
        var undoGroupIndex = Undo.GetCurrentGroup();
        
        int count = 0;
        var instances = data.treeInstances;
        
        Vector3 terrainPos = _selectedTerrain.transform.position;
        Vector3 terrainSize = data.size;
        
        for (int i = 0; i < instances.Length; i++)
        {
            TreeInstance tree = instances[i];
            
            if (tree.prototypeIndex != _selectedPrototypeIndex) continue;
            
            // 1. 位置計算
            Vector3 localPos = Vector3.Scale(tree.position, terrainSize);
            Vector3 worldPos = terrainPos + localPos;
            
            // 2. 回転計算 (Y軸回転のみ)
            Quaternion rotation = Quaternion.Euler(0f, tree.rotation * Mathf.Rad2Deg, 0f);
            
            // 3. Prefab生成
            GameObject newObj = (GameObject) PrefabUtility.InstantiatePrefab(targetPrefab);
            newObj.transform.position = worldPos;
            newObj.transform.rotation = rotation;
            
            // 4. スケール計算
            Vector3 originalScale = targetPrefab.transform.localScale;
            newObj.transform.localScale = new Vector3(
                originalScale.x * tree.widthScale,
                originalScale.y * tree.heightScale,
                originalScale.z * tree.widthScale
            );
            
            if (parentTransform != null)
            {
                newObj.transform.SetParent(parentTransform, true);
            }
            
            Undo.RegisterCreatedObjectUndo(newObj, "Create Tree Object");
            count++;
        }
        
        Undo.CollapseUndoOperations(undoGroupIndex);
        
        _infoLabel.text = $"Success! Exported {count} trees.";
        Debug.Log($"[TreeExporter] Successfully exported {count} trees from Terrain.");
    }
}