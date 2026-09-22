using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Game.InGame.Map.MapObject;
using Client.MapScene.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UnitTest.MapPreview.Integration
{
    public class GeneratedMapPreviewSmallWorldTest
    {
        [UnityTest]
        public IEnumerator EmptySingleMissingAndRetryUsePublicRegenerationAndReadCurrentMaster()
        {
            var folder = $"Assets/GeneratedMapPreviewSmallWorldTest_{Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            var main = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(main, $"{folder}/Main.unity");
            using var fixture = new GeneratedMapPreviewSmallWorld();
            var stage = ScriptableObject.CreateInstance<GeneratedMapPreviewStage>();
            try
            {
                fixture.Initialize();
                StageUtility.GoToStage(stage, true);
                fixture.SetObjectCount(0, false);
                stage.Regenerate();
                yield return GeneratedMapPreviewObservation.WaitForCompletion(stage);
                AssertReady(0);
                Assert.That(stage.scene.GetRootGameObjects().Single().GetComponentsInChildren<UnityEngine.Terrain>().Length, Is.EqualTo(1));

                fixture.SetObjectCount(1, false);
                stage.Regenerate();
                yield return GeneratedMapPreviewObservation.WaitForCompletion(stage);
                AssertReady(1);

                // コピーした外部入力の参照を壊し、欠損の理由と全生成物の破棄を観測する
                // Break a copied external reference and observe its reason and complete content disposal
                fixture.SetObjectCount(1, true);
                LogAssert.Expect(LogType.Error, new Regex(Regex.Escape($"[GeneratedMapPreview] Prefab unavailable. MapObjectGuid:{fixture.ObjectGuid} Address:{GeneratedMapPreviewSmallWorld.MissingAddress}")));
                LogAssert.Expect(LogType.Error, "[GeneratedMapPreview] Incomplete preview discarded. 期待数: 1 / 作成数: 0 / 欠損数: 1");
                stage.Regenerate();
                yield return GeneratedMapPreviewObservation.WaitForCompletion(stage);
                Assert.That(stage.State, Is.EqualTo(GeneratedMapPreviewState.Failed));
                Assert.That(stage.StatusText, Does.Contain("欠損数: 1"));
                Assert.That(stage.scene.rootCount, Is.Zero);

                fixture.SetObjectCount(1, false);
                stage.Regenerate();
                yield return GeneratedMapPreviewObservation.WaitForCompletion(stage);
                AssertReady(1);
                Assert.That(main.isDirty, Is.False);
                Debug.Log("[GeneratedMapPreviewIntegration] Empty → one → missing/Failed → restored/Ready completed through public Regenerate.");
            }
            finally
            {
                StageUtility.GoToMainStage();
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
            }

            #region Internal

            void AssertReady(int count)
            {
                Assert.That(stage.State, Is.EqualTo(GeneratedMapPreviewState.Ready), stage.StatusText);
                Assert.That(stage.StatusText, Is.EqualTo($"期待数: {count} / 作成数: {count} / 欠損数: 0"));
                Assert.That(stage.scene.GetRootGameObjects().Single().GetComponentsInChildren<MapObjectGameObject>().Length, Is.EqualTo(count));
                Assert.That(EditorApplication.isPlaying, Is.False);
            }

            #endregion
        }
    }
}
