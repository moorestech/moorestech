using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Test.PlayModeTest
{
    public class PlayModeTestPrefabInstantiate
    {
        public static GameObject Instantiate(string prefabName)
        {
            return Instantiate(prefabName, Vector3.zero, Quaternion.identity);
        }
        public static GameObject Instantiate(string prefabName,Vector3 pos,Quaternion rot)
        {
            var prefabPaths = AssetDatabase.FindAssets("t:Prefab " + prefabName)
                .Select(AssetDatabase.GUIDToAssetPath).ToList();

            if (prefabPaths.Count == 0)  throw new Exception("Prefab not found");
            if (1 < prefabPaths.Count) throw new Exception("Multiple prefabs found");
            
            var loadedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths.First());
            
            return Object.Instantiate(loadedPrefab, new Vector3(0, 0, 0), Quaternion.identity);
        }
    }
}