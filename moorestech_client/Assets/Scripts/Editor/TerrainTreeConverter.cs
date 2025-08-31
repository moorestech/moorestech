using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TerrainTreeConverter : EditorWindow
{
    private Terrain _terrain;
    private bool _groupByPrefab = true;
    private bool _clearOriginalTrees = false;
    private bool _clearTreePrototypes = false;
    private string _rootObjectName = "Converted Trees";

    private void OnGUI()
    {
        GUILayout.Label("Terrain Tree to GameObject Converter", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        _terrain = (Terrain)EditorGUILayout.ObjectField("Target Terrain", _terrain, typeof(Terrain), true);
        GUILayout.Space(5);
        
        _groupByPrefab = EditorGUILayout.Toggle("Group by Tree Type", _groupByPrefab);
        _clearOriginalTrees = EditorGUILayout.Toggle("Clear Original Trees", _clearOriginalTrees);
        _clearTreePrototypes = EditorGUILayout.Toggle("Clear Tree Prototypes", _clearTreePrototypes);
        _rootObjectName = EditorGUILayout.TextField("Root Object Name", _rootObjectName);
        
        GUILayout.Space(10);
        
        if (_terrain != null)
        {
            var treeCount = _terrain.terrainData.treeInstances.Length;
            var prototypeCount = _terrain.terrainData.treePrototypes.Length;
            
            EditorGUILayout.LabelField("Tree Instances:", treeCount.ToString());
            EditorGUILayout.LabelField("Tree Types:", prototypeCount.ToString());
        }
        
        GUILayout.Space(10);
        
        EditorGUI.BeginDisabledGroup(_terrain == null);
        
        if (GUILayout.Button("Convert Trees to GameObjects", GUILayout.Height(30)))
        {
            ConvertTreesToGameObjects();
        }
        
        EditorGUI.EndDisabledGroup();
    }
    
    [MenuItem("moorestech/Terrain/Tree Converter")]
    private static void ShowWindow()
    {
        var window = GetWindow<TerrainTreeConverter>();
        window.titleContent = new GUIContent("Terrain Tree Converter");
        window.Show();
    }
    
    private void ConvertTreesToGameObjects()
    {
        if (!ValidateInputs()) return;
        
        var terrainData = _terrain.terrainData;
        var treeInstances = terrainData.treeInstances;
        var treePrototypes = terrainData.treePrototypes;
        var treeCount = treeInstances.Length;
        
        if (treeCount == 0)
        {
            EditorUtility.DisplayDialog("Info", "No trees found on the terrain.", "OK");
            return;
        }
        
        var rootObject = CreateRootObject();
        var groupObjects = CreateGroupObjects(treePrototypes, rootObject);
        
        for (var i = 0; i < treeCount; i++)
        {
            var progress = (float)i / treeCount;
            if (EditorUtility.DisplayCancelableProgressBar("Converting Trees", $"Processing tree {i + 1} of {treeCount}", progress))
            {
                EditorUtility.ClearProgressBar();
                return;
            }
            
            ConvertTreeInstance(treeInstances[i], treePrototypes, groupObjects);
        }
        
        EditorUtility.ClearProgressBar();
        
        if (_clearOriginalTrees)
        {
            ClearOriginalTrees(terrainData);
        }
        
        if (_clearTreePrototypes)
        {
            ClearTreePrototypes(terrainData);
        }
        
        EditorUtility.DisplayDialog("Success", $"Successfully converted {treeCount} trees to GameObjects.", "OK");
        
        #region Internal
        
        bool ValidateInputs()
        {
            if (_terrain == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a terrain.", "OK");
                return false;
            }
            
            if (_terrain.terrainData == null)
            {
                EditorUtility.DisplayDialog("Error", "Selected terrain has no terrain data.", "OK");
                return false;
            }
            
            if (string.IsNullOrEmpty(_rootObjectName))
            {
                EditorUtility.DisplayDialog("Error", "Please specify a root object name.", "OK");
                return false;
            }
            
            return true;
        }
        
        GameObject CreateRootObject()
        {
            var rootObject = new GameObject(_rootObjectName);
            rootObject.transform.position = _terrain.transform.position;
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Tree Converter Root");
            return rootObject;
        }
        
        Dictionary<int, GameObject> CreateGroupObjects(TreePrototype[] treePrototypes, GameObject rootObject)
        {
            var groupObjects = new Dictionary<int, GameObject>();
            
            if (_groupByPrefab)
            {
                for (var i = 0; i < treePrototypes.Length; i++)
                {
                    var prototype = treePrototypes[i];
                    if (prototype.prefab == null) continue;
                    
                    var groupName = $"{prototype.prefab.name}_Group";
                    var groupObject = new GameObject(groupName);
                    groupObject.transform.SetParent(rootObject.transform);
                    Undo.RegisterCreatedObjectUndo(groupObject, $"Create Tree Group: {groupName}");
                    groupObjects[i] = groupObject;
                }
            }
            
            return groupObjects;
        }
        
        void ConvertTreeInstance(TreeInstance treeInstance, TreePrototype[] treePrototypes, Dictionary<int, GameObject> groupObjects)
        {
            var prototype = treePrototypes[treeInstance.prototypeIndex];
            if (prototype.prefab == null) return;
            
            var worldPosition = CalculateWorldPosition(treeInstance);
            var rotation = Quaternion.Euler(0, treeInstance.rotation * Mathf.Rad2Deg, 0);
            var scale = new Vector3(treeInstance.widthScale, treeInstance.heightScale, treeInstance.widthScale);
            
            var treeGameObject = (GameObject)PrefabUtility.InstantiatePrefab(prototype.prefab);
            Undo.RegisterCreatedObjectUndo(treeGameObject, "Create Tree GameObject");
            
            treeGameObject.transform.position = worldPosition;
            treeGameObject.transform.rotation = rotation;
            treeGameObject.transform.localScale = scale;
            
            if (_groupByPrefab && groupObjects.ContainsKey(treeInstance.prototypeIndex))
            {
                treeGameObject.transform.SetParent(groupObjects[treeInstance.prototypeIndex].transform);
            }
            else if (groupObjects.Count == 0)
            {
                var rootObject = GameObject.Find(_rootObjectName);
                if (rootObject != null)
                {
                    treeGameObject.transform.SetParent(rootObject.transform);
                }
            }
        }
        
        Vector3 CalculateWorldPosition(TreeInstance treeInstance)
        {
            var terrainData = _terrain.terrainData;
            var terrainPosition = _terrain.transform.position;
            
            return new Vector3(
                treeInstance.position.x * terrainData.size.x,
                treeInstance.position.y * terrainData.size.y,
                treeInstance.position.z * terrainData.size.z
            ) + terrainPosition;
        }
        
        void ClearOriginalTrees(TerrainData terrainData)
        {
            Undo.RecordObject(terrainData, "Clear Original Trees");
            terrainData.treeInstances = new TreeInstance[0];
            EditorUtility.SetDirty(terrainData);
        }
        
        void ClearTreePrototypes(TerrainData terrainData)
        {
            Undo.RecordObject(terrainData, "Clear Tree Prototypes");
            terrainData.treePrototypes = new TreePrototype[0];
            EditorUtility.SetDirty(terrainData);
        }
        
        #endregion
    }
}