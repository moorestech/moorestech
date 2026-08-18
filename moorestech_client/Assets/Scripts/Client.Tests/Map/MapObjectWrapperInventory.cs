using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Map
{
    /// <summary>
    ///     ラッパープレハブの検証対象一覧を、生成器が読むのと同じspecies-inventory.jsonから読む
    ///     Reads the wrapper prefabs under test from the very species-inventory.json the generator consumes
    /// </summary>
    public static class MapObjectWrapperInventory
    {
        // 抽出スクリプトの出力がアドレスとラッパーパスの唯一のソース
        // The extraction script's output is the single source of addresses and wrapper paths
        private const string InventoryRelativePath = "../../scripts/mapmaking-parity/species-inventory.json";

        public static List<Element> LoadSpecies()
        {
            var inventoryPath = Path.GetFullPath(Path.Combine(Application.dataPath, InventoryRelativePath));
            Assert.IsTrue(File.Exists(inventoryPath), $"species inventory not found: {inventoryPath}");

            var species = JsonUtility.FromJson<Inventory>(File.ReadAllText(inventoryPath)).species;
            Assert.IsNotEmpty(species, "species inventory is empty; the test would pass vacuously");
            return species;
        }

        [Serializable]
        public class Element
        {
            public string address;
            public string wrapperPath;
            public string kind;
            public string mapObjectGuid;
        }

        [Serializable]
        private class Inventory
        {
            public List<Element> species;
        }
    }
}
