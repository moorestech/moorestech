using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ClearTerrainTreePrototypes : EditorWindow
{
    private Terrain _terrain;
    private bool _clearInstances = true;
    private bool _showSharedTerrainWarning = false;

    private void OnGUI()
    {
        GUILayout.Label("Clear Terrain Tree Prototypes", EditorStyles.boldLabel);
        GUILayout.Space(10);

        _terrain = (Terrain)EditorGUILayout.ObjectField("Target Terrain", _terrain, typeof(Terrain), true);
        
        if (_terrain != null)
        {
            DisplayTerrainInfo();
        }
        else
        {
            EditorGUILayout.HelpBox("Please select a terrain.", MessageType.Warning);
        }
        
        GUILayout.Space(5);
        _clearInstances = EditorGUILayout.Toggle("Clear Tree Instances Too", _clearInstances);
        
        GUILayout.Space(10);
        
        EditorGUI.BeginDisabledGroup(_terrain == null || _terrain.terrainData == null);
        
        if (GUILayout.Button("Clear Tree Prototypes", GUILayout.Height(30)))
        {
            ClearTreePrototypes();
        }
        
        EditorGUI.EndDisabledGroup();
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Clear Selected Terrains", GUILayout.Height(25)))
        {
            ClearSelectedTerrainsPrototypes();
        }
    }

    [MenuItem("moorestech/Terrain/Clear Tree Prototypes")]
    private static void ShowWindow()
    {
        var window = GetWindow<ClearTerrainTreePrototypes>();
        window.titleContent = new GUIContent("Clear Tree Prototypes");
        window.Show();
    }

    private void ClearTreePrototypes()
    {
        if (!ValidateAndConfirm()) return;
        
        var terrains = new List<Terrain> { _terrain };
        ExecuteClearOperation(terrains);
    }
    
    private void ClearSelectedTerrainsPrototypes()
    {
        var selectedTerrains = GetSelectedTerrains();
        if (selectedTerrains.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "No terrains selected in the scene.", "OK");
            return;
        }
        
        if (!ConfirmMultipleTerrains(selectedTerrains)) return;
        
        ExecuteClearOperation(selectedTerrains);
    }
    
    private void ExecuteClearOperation(List<Terrain> terrains)
    {
        var processedData = new HashSet<TerrainData>();
        
        try
        {
            EditorUtility.DisplayProgressBar("Clearing Tree Prototypes", "Processing terrains...", 0f);
            
            for (var i = 0; i < terrains.Count; i++)
            {
                var terrain = terrains[i];
                var terrainData = terrain.terrainData;
                
                var progress = (float)(i + 1) / terrains.Count;
                EditorUtility.DisplayProgressBar("Clearing Tree Prototypes", 
                    $"Processing {terrain.name} ({i + 1}/{terrains.Count})", progress);
                
                if (processedData.Add(terrainData))
                {
                    ProcessTerrainData(terrainData);
                }
                
                terrain.Flush();
            }
            
            EditorUtility.DisplayDialog("Success", 
                $"Successfully cleared tree prototypes from {terrains.Count} terrain(s).", "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        
        #region Internal
        
        void ProcessTerrainData(TerrainData terrainData)
        {
            Undo.RecordObject(terrainData, "Clear Tree Prototypes");
            
            terrainData.treePrototypes = new TreePrototype[0];
            
            if (_clearInstances)
            {
                terrainData.SetTreeInstances(new TreeInstance[0], false);
            }
            
            EditorUtility.SetDirty(terrainData);
        }
        
        #endregion
    }

    private void DisplayTerrainInfo()
    {
        if (_terrain.terrainData == null)
        {
            EditorGUILayout.HelpBox("Selected terrain has no TerrainData.", MessageType.Error);
            return;
        }

        var terrainData = _terrain.terrainData;
        var prototypeCount = terrainData.treePrototypes.Length;
        var instanceCount = terrainData.treeInstances.Length;
        
        EditorGUILayout.LabelField("Tree Prototypes:", prototypeCount.ToString());
        EditorGUILayout.LabelField("Tree Instances:", instanceCount.ToString());
        
        CheckSharedTerrainData();
    }
    
    private void CheckSharedTerrainData()
    {
        var sharingTerrains = GetTerrainsWithSameData(_terrain);
        
        if (sharingTerrains.Count > 0)
        {
            var terrainNames = string.Join(", ", sharingTerrains.Select(t => t.name));
            EditorGUILayout.HelpBox($"Warning: TerrainData is shared with: {terrainNames}", MessageType.Warning);
            _showSharedTerrainWarning = true;
        }
        else
        {
            _showSharedTerrainWarning = false;
        }
    }

    private bool ValidateAndConfirm()
    {
        if (_terrain == null || _terrain.terrainData == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a valid terrain with TerrainData.", "OK");
            return false;
        }

        var message = "Are you sure you want to clear all tree prototypes from this terrain?";
        
        if (_showSharedTerrainWarning)
        {
            var sharingTerrains = GetTerrainsWithSameData(_terrain);
            var terrainNames = string.Join(", ", sharingTerrains.Select(t => t.name));
            message = $"This will affect shared TerrainData used by: {terrainNames}\n\n" + message;
        }
        
        return EditorUtility.DisplayDialog("Confirm Clear Operation", message, "Clear", "Cancel");
    }
    
    private bool ConfirmMultipleTerrains(List<Terrain> terrains)
    {
        var uniqueDataCount = terrains.Select(t => t.terrainData).Distinct().Count();
        var message = $"Clear tree prototypes from {terrains.Count} terrain(s)?\n" +
                     $"This will affect {uniqueDataCount} unique TerrainData asset(s).";
        
        return EditorUtility.DisplayDialog("Confirm Multiple Clear Operation", message, "Clear All", "Cancel");
    }
    
    private List<Terrain> GetSelectedTerrains()
    {
        var terrains = new List<Terrain>();
        
        foreach (var gameObject in Selection.gameObjects)
        {
            if (gameObject == null) continue;
            
            var terrain = gameObject.GetComponent<Terrain>();
            if (terrain != null && terrain.terrainData != null)
            {
                terrains.Add(terrain);
            }
            
            var childTerrains = gameObject.GetComponentsInChildren<Terrain>(true);
            foreach (var childTerrain in childTerrains)
            {
                if (childTerrain != null && childTerrain.terrainData != null && !terrains.Contains(childTerrain))
                {
                    terrains.Add(childTerrain);
                }
            }
        }
        
        return terrains.Distinct().ToList();
    }
    
    private List<Terrain> GetTerrainsWithSameData(Terrain targetTerrain)
    {
        if (targetTerrain?.terrainData == null) return new List<Terrain>();
        
        return FindObjectsOfType<Terrain>()
            .Where(t => t != targetTerrain && t.terrainData == targetTerrain.terrainData)
            .ToList();
    }
}