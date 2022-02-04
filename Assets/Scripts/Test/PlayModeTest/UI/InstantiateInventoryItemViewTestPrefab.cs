using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Test.PlayModeTest.UI
{
    /// <summary>
    /// このクラスはPrefabを作成するだけです
    /// 実際のテストはPrefabの中のInventoryUITestにあります。
    /// </summary>
    public class InstantiateInventoryItemViewTestPrefab
    {
        private const string PrefabName = "InventoryUITest";
        
        [UnityTest]
        public IEnumerator InventoryViewTest()
        {
            PlayModeTestPrefabInstantiate.Instantiate(PrefabName);
            yield return new WaitForSeconds(0.1f);
        }

    }
}