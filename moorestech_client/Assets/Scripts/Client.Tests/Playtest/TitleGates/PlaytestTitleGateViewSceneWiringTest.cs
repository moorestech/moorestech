using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Tests.Playtest.TitleGates
{
    /// <summary>
    /// タイトルの合成ルート（PlaytestTitleGateView）とポップアップのシーン配線を守る。配線が切れると確認が出ず、関所が恒久に開始を断る。
    /// Client.MainMenu はasmdefを持たずテストから型を参照できないので、型名とSerializedObjectで読む（シーンは開いて読むだけ）。
    /// Guards the scene wiring of the title's composition root (PlaytestTitleGateView) and its popups; broken wiring hides the confirmations and the checkpoint refuses every start forever.
    /// Client.MainMenu has no asmdef the tests can reference, so the types are read by name through SerializedObject (the scene is only opened and read).
    /// </summary>
    public class PlaytestTitleGateViewSceneWiringTest
    {
        private const string ScenePath = "Assets/Scenes/Game/MainMenu.unity";

        [Test]
        public void タイトルの合成ルートと各ポップアップの参照が配線されている()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var views = CollectSceneBehavioursNamed(scene, "PlaytestTitleGateView");
                Assert.AreEqual(1, views.Count, $"{ScenePath} の PlaytestTitleGateView が1つでない");

                // 合成ルートの2参照と、その先の各ポップアップが持つ参照（ボタン・入力欄・文言）を全て辿る
                // Walk the root's two references and every reference each popup holds (buttons, input field, texts)
                Assert.AreEqual("PlaytestTitleGates", views[0].gameObject.name);
                var view = new SerializedObject(views[0]);
                foreach (var popupField in new[] { "consentPopup", "crashReportPopup" })
                {
                    var popup = view.FindProperty(popupField).objectReferenceValue;
                    Assert.IsNotNull(popup, $"{ScenePath} の PlaytestTitleGateView.{popupField} が未配線");
                    AssertAllObjectReferencesWired(popup, popupField);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertAllObjectReferencesWired(Object popup, string popupField)
        {
            var property = new SerializedObject(popup).GetIterator();
            var checkedCount = 0;
            property.NextVisible(true);
            while (property.NextVisible(false))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script") continue;
                Assert.IsNotNull(property.objectReferenceValue, $"{ScenePath} の {popupField}（{popup.GetType().Name}）.{property.name} が未配線");
                checkedCount++;
            }

            // 0件なら参照を辿れていない。vacuousな緑を作らない
            // Zero references means nothing was walked; fail instead of passing vacuously
            Assert.Greater(checkedCount, 0, $"{ScenePath} の {popupField}（{popup.GetType().Name}）に検査できる参照が1つも無い");
        }

        private static List<MonoBehaviour> CollectSceneBehavioursNamed(Scene scene, string typeName)
        {
            var result = new List<MonoBehaviour>();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour.GetType().Name == typeName) result.Add(behaviour);
            return result;
        }
    }
}
