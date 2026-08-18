using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// species-inventory.jsonに載った樹種・岩を全件ラッパープレハブ化し、Addressableへ登録する入口
// Entry point that turns every tree and rock listed in species-inventory.json into a wrapper prefab and registers it to Addressables
public static class MapObjectWrapperGeneratorMenu
{
    // 抽出スクリプトの出力がアドレス・ラッパーパスの唯一のソース
    // The extraction script's output is the single source of addresses and wrapper paths
    private const string InventoryRelativePath = "../../scripts/mapmaking-parity/species-inventory.json";

    [MenuItem("moorestech/MapObjectWrapper/Generate All")]
    public static void GenerateAll()
    {
        var species = LoadInventorySpecies();

        foreach (var element in species) WrapperPrefabFactory.CreateWrapperPrefab(element);
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

// species-inventory.jsonの1species分。生成側とAddressable登録側で共有する
// One species entry of species-inventory.json, shared by the prefab factory and the Addressable registrar
[Serializable]
public class MapObjectWrapperSpecies
{
    public string key;
    public string prefabPath;
    public string kind;
    public string address;
    public string wrapperPath;
    public string mapObjectGuid;
    public string mapObjectName;
}
