using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 全speciesをラッパー化しAddressable登録する
// Entry point that wraps every species and registers it to Addressables
public static class MapObjectWrapperGeneratorMenu
{
    // 抽出スクリプトの出力がアドレス・ラッパーパスの唯一のソース
    // The extraction script's output is the single source of addresses and wrapper paths
    private const string InventoryRelativePath = "../../scripts/mapmaking-parity/species-inventory.json";

    [MenuItem("moorestech/MapObjectWrapper/Generate All")]
    public static void GenerateAll()
    {
        var species = LoadInventorySpecies();

        // 開いているシーンを組み立て場に使うとシーンがdirtyのまま残り、以後のテスト実行がシーン保存ダイアログで止まる
        // Assembling in the open scene would leave it dirty, and a later test run would then stall on the save-scene dialog
        var workScene = EditorSceneManager.NewPreviewScene();
        foreach (var element in species) WrapperPrefabFactory.CreateWrapperPrefab(element, workScene);
        EditorSceneManager.ClosePreviewScene(workScene);
        AssetDatabase.SaveAssets();

        // プレハブが全件importされてからでないとGUIDが引けないので、登録は生成後にまとめて行う
        // GUIDs only resolve once every prefab is imported, so registration runs in one pass after generation
        var registeredCount = WrapperAddressableRegistrar.RegisterAll(species);
        Debug.Log($"MapObjectWrapper: generated {species.Count} wrapper prefabs, registered {registeredCount} addressable entries");
    }

    private static List<MapObjectWrapperSpecies> LoadInventorySpecies()
    {
        var inventoryPath = Path.GetFullPath(Path.Combine(Application.dataPath, InventoryRelativePath));
        if (!File.Exists(inventoryPath)) throw new InvalidOperationException($"species inventory not found: {inventoryPath}");

        var inventory = JsonUtility.FromJson<SpeciesInventory>(File.ReadAllText(inventoryPath));
        if (inventory.species == null || inventory.species.Count == 0) throw new InvalidOperationException($"species inventory has no species: {inventoryPath}");

        return inventory.species;
    }

    [Serializable]
    private class SpeciesInventory
    {
        public List<MapObjectWrapperSpecies> species;
    }
}
