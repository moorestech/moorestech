using System.Collections.Generic;
using Client.Localization;
using Client.Starter;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Client.Tests.Localization.Display
{
    /// <summary>
    /// ADR 0052でuGUIのまま残した例外シーン（MainMenu / GameInitialaizer のローディング表示）の翻訳配線を守る。
    /// Web UIホスト起動前に動くためWeb側の代替が無く、配線が切れても無言で文字が消えるだけになる。
    /// Guards the translation wiring of the scenes ADR 0052 keeps on uGUI (MainMenu and the GameInitialaizer loading display).
    /// They run before the Web UI host starts, so there is no web fallback and broken wiring only makes text vanish silently.
    /// </summary>
    public class ExceptionSceneLocalizedTextTest
    {
        // MainMenuはInspectorのキーで引くため、キーの実在まで検査する
        // MainMenu resolves Inspector keys, so the keys themselves are checked for existence
        [Test]
        public void MainMenuシーンの翻訳付きTMPがバニラ辞書のキーへ配線されている()
        {
            const string scenePath = "Assets/Scenes/Game/MainMenu.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var localizedTexts = CollectSceneComponents<TextMeshProLocalize>(scene);

                // 0件なら「配線が消えた」ものとして落とす（vacuousな緑を作らない）
                // Zero targets means the wiring is gone, so fail instead of passing vacuously
                Assert.IsNotEmpty(localizedTexts, $"{scenePath} に翻訳付きTMPが1つも無い（例外シーンの翻訳配線が失われている）");

                foreach (var localizedText in localizedTexts)
                {
                    var key = new SerializedObject(localizedText).FindProperty("key").stringValue;
                    Assert.IsNotEmpty(key, $"{scenePath} の {localizedText.name} に翻訳キーが設定されていない");
                    Assert.IsTrue(
                        VanillaLocalizationTable.SourceTexts.ContainsKey(key),
                        $"{scenePath} の {localizedText.name} が未知の翻訳キー '{key}' を指している");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ローディング表示は実行時にLoadingProgressLogが型付きキーで流し込むため、検査点は出力先の参照が生きていること
        // The loading text is filled at runtime by LoadingProgressLog with typed keys, so the check point is that its sink reference survives
        [Test]
        public void GameInitialaizerシーンのローディング表示先が配線されている()
        {
            const string scenePath = "Assets/Scenes/Game/GameInitialaizer.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var pipelines = CollectSceneComponents<InitializeScenePipeline>(scene);
                Assert.AreEqual(1, pipelines.Count, $"{scenePath} の InitializeScenePipeline が1つでない");

                var loadingLog = new SerializedObject(pipelines[0]).FindProperty("loadingLog").objectReferenceValue;
                Assert.IsNotNull(loadingLog, $"{scenePath} の loadingLog が未配線（ローディング進捗が無言で消える）");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static List<T> CollectSceneComponents<T>(Scene scene) where T : UnityEngine.Component
        {
            var result = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<T>(true));
            return result;
        }
    }
}
