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
    public class InventoryItemViewTest:IPrebuildSetup
    {
        private const string SceneName = "MainInventoryTest";
        
        
        [PrebuildSetup(typeof(InventoryItemViewTest))]
        public void Setup()
        {
        }
        
        
        [UnityTest]
        public IEnumerator InventoryViewTest() {

            var sceneAssets = AssetDatabase.FindAssets("ItemUITest")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)))
                .Where(obj => obj != null)
                .Select(obj => (Gameo) obj)
                .Where(asset => asset.name == SceneName);
            var scenePath = AssetDatabase.GetAssetPath(sceneAssets.First());
            
            EditorSceneManager.OpenScene(scenePath);

            yield return new WaitForSeconds(5);

            Assert.IsTrue(true);

        }

    }
}