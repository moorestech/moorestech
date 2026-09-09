using System.Collections.Generic;
using Client.Localization;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Client.Tests.Localization.Display
{
    /// <summary>
    /// ADR 0052でuGUIのまま残した例外シーン（MainMenu / GameInitialaizer のローディング表示）の翻訳配線を守る。
    /// Web UIホスト起動前に動くためWeb側の代替が無く、キー切れは無言の英日欠落になる。
    /// Guards the translation wiring of the scenes ADR 0052 keeps on uGUI (MainMenu and the GameInitialaizer loading display).
    /// They run before the Web UI host starts, so there is no web fallback and a broken key silently loses its text.
    /// </summary>
    public class ExceptionSceneLocalizedTextTest
    {
        [Test]
        public void MainMenuシーンの翻訳付きTMPがバニラ辞書のキーへ配線されている()
        {
            AssertSceneLocalizedTexts("Assets/Scenes/Game/MainMenu.unity");
        }

        [Test]
        public void GameInitialaizerシーンの翻訳付きTMPがバニラ辞書のキーへ配線されている()
        {
            AssertSceneLocalizedTexts("Assets/Scenes/Game/GameInitialaizer.unity");
        }

        // 対象が0件なら「配線が消えた」ものとして落とす（vacuousな緑を作らない）
        // Zero targets means the wiring is gone, so fail instead of passing vacuously
        private static void AssertSceneLocalizedTexts(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var localizedTexts = CollectSceneLocalizedTexts(scene);
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

        private static List<TextMeshProLocalize> CollectSceneLocalizedTexts(Scene scene)
        {
            var result = new List<TextMeshProLocalize>();
            foreach (var root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<TextMeshProLocalize>(true));
            return result;
        }
    }
}
