using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Test.PlayModeTest.UI
{
    public class InstantiateInventoryItemViewTestPrefab
    {
        private const string PrefabName = "InventoryUITest";
        
        [UnityTest]
        public IEnumerator InventoryViewTest() {

            var prefabPath = AssetDatabase.FindAssets("t:Prefab " + PrefabName)
                .Select(AssetDatabase.GUIDToAssetPath).First();
            
            var loadedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var prefabInstance = Object.Instantiate(loadedPrefab, new Vector3(0, 0, 0), Quaternion.identity);
            

            yield return new WaitForSeconds(0.1f);
        }

    }
}