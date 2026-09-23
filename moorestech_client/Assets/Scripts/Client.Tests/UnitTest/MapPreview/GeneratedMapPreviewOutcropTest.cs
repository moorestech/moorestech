using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Client.Game.InGame.Map.Outcrop;
using Client.MapScene.Editor;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Map.Interface.Json;
using NUnit.Framework;
using Server.Boot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Client.Tests.UnitTest.MapPreview
{
    public class GeneratedMapPreviewOutcropTest
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private string _assetFolder;
        private GeneratedMapPreviewStage _stage;
        private GeneratedMapPreviewRun _run;
        private GeneratedMapPreviewWorld _world;
        private GeneratedMapPreviewContent _content;
        private EditorTerrainAssetLoader _assets;
        private Transform _outcrops;
        private CancellationTokenSource _cancellation;

        [SetUp]
        public void SetUp()
        {
            Assert.That(EditorApplication.isPlaying, Is.False);
            Assert.That(StageUtility.GetCurrentStage(), Is.SameAs(StageUtility.GetMainStage()));
            _assetFolder = $"Assets/GeneratedMapPreviewOutcropTest_{Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", _assetFolder.Substring("Assets/".Length));
            var main = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(main, $"{_assetFolder}/Main.unity");

            // 実生成の公開結果を使い、地形全量の描画だけを省いて配置経路を検査する
            // Use real generated public results and inspect placement without rebuilding all terrain visuals
            _world = GeneratedMapPreviewWorld.Create(ServerDirectory.GetDirectory());
            _stage = ScriptableObject.CreateInstance<GeneratedMapPreviewStage>();
            StageUtility.GoToStage(_stage, true);
            _content = new GeneratedMapPreviewContent(_stage.scene);
            _outcrops = new GameObject("VeinOutcrops").transform;
            _outcrops.SetParent(_content.Root, false);
            _run = new GeneratedMapPreviewRun();
            _assets = new EditorTerrainAssetLoader();
            _cancellation = new CancellationTokenSource();
            SetField(_run, "_world", _world);
            SetField(_run, "_content", _content);
            SetField(_stage, "_run", _run);
            SetField(_stage, "_cancellation", _cancellation);
            typeof(GeneratedMapPreviewStage).GetProperty(nameof(_stage.State)).SetValue(_stage, GeneratedMapPreviewState.Generating);
        }

        [TearDown]
        public void TearDown()
        {
            StageUtility.GoToMainStage();
            _run?.Dispose();
            _world?.Dispose();
            _cancellation?.Dispose();
            if (_stage != null) Object.DestroyImmediate(_stage);
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(_assetFolder);
        }

        [UnityTest]
        public IEnumerator GeneratedVeinsAppearAtRuntimeCentersWithPrefabTransformsAndOwnedLifetime()
        {
            var veins = _world.Map.MapVeins;
            Assert.That(veins, Is.Not.Empty);
            yield return ObserveAsync(PlaceAsync()).ToCoroutine();
            Assert.That(_stage.State, Is.EqualTo(GeneratedMapPreviewState.Ready));
            Assert.That(_outcrops.childCount, Is.EqualTo(veins.Count));
            Assert.That(_stage.StatusText, Is.EqualTo($"期待数: {veins.Count} / 作成数: {veins.Count} / 欠損数: 0"));

            // GUIDだけでなく全配置を照合し、同種鉱脈が複数あっても取りこぼさない
            // Compare every placement rather than only GUIDs so repeated vein types cannot be omitted
            var borrowed = new HashSet<GameObject>();
            for (var index = 0; index < veins.Count; index++)
            {
                var vein = veins[index];
                var instance = _outcrops.GetChild(index);
                var prefab = _assets.LoadAsync<GameObject>(MasterHolder.MapVeinMaster.GetElementOrNull(vein.VeinGuid).OutcropAddressablePath, CancellationToken.None).GetAwaiter().GetResult();
                borrowed.Add(prefab);
                Assert.That(instance.position, Is.EqualTo(((Vector3)vein.MinPosition + vein.MaxPosition + Vector3.one) * 0.5f));
                Assert.That(Quaternion.Angle(instance.rotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(instance.localScale, Is.EqualTo(prefab.transform.localScale));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject), Is.SameAs(prefab));
                Assert.That(instance.gameObject.scene, Is.EqualTo(_stage.scene));
                Assert.That(instance.gameObject.activeInHierarchy, Is.True);
                Assert.That(instance.GetComponentsInChildren<Renderer>(), Is.Not.Empty);
            }
            // Prefabに既存の採掘componentがあっても、ランタイム初期化を呼ばない
            // Even when a prefab already carries a mining component, do not invoke its runtime initialization
            foreach (var outcrop in _content.Root.GetComponentsInChildren<OutcropGameObject>(true))
                Assert.That(typeof(OutcropGameObject).GetField("_veinGuid", PrivateInstance).GetValue(outcrop), Is.EqualTo(Guid.Empty));
            Assert.That(_content.Root.GetComponentsInChildren<OutcropGameObjectDatastore>(true), Is.Empty);
            Assert.That(EditorApplication.isPlaying, Is.False);

            var root = _content.Root.gameObject;
            StageUtility.GoToMainStage();
            Assert.That(root == null && _outcrops == null, Is.True);
            foreach (var prefab in borrowed) Assert.That(AssetDatabase.Contains(prefab), Is.True);
        }

        [UnityTest]
        public IEnumerator MissingOutcropAssetsAndMastersCountEveryPlacementAndPreventReady()
        {
            var vein = _world.Map.MapVeins[0];
            var address = MasterHolder.MapVeinMaster.GetElementOrNull(vein.VeinGuid).OutcropAddressablePath;
            var unknown = new MapVeinInfoJson { VeinGuidStr = Guid.NewGuid().ToString() };
            _world.Map.MapVeins = new List<MapVeinInfoJson> { vein, vein, unknown };

            // ローダーの住所スナップショットだけを壊し、実マスタや登録資産を編集しない
            // Break only the loader's address snapshot without editing actual masters or registered assets
            var paths = (Dictionary<string, string>)typeof(EditorTerrainAssetLoader).GetField("_paths", PrivateInstance).GetValue(_assets);
            Assert.That(paths.Remove(address), Is.True);
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape($"[GeneratedMapPreview] Outcrop prefab unavailable. VeinGuid:{vein.VeinGuid} Address:{address}")));
            LogAssert.Expect(LogType.Error, $"[GeneratedMapPreview] Missing vein master. VeinGuid:{unknown.VeinGuid}");
            LogAssert.Expect(LogType.Error, "[GeneratedMapPreview] Incomplete preview discarded. 期待数: 3 / 作成数: 0 / 欠損数: 3");

            yield return ObserveAsync(PlaceAsync()).ToCoroutine();

            Assert.That(_stage.State, Is.EqualTo(GeneratedMapPreviewState.Failed));
            Assert.That(_stage.StatusText, Is.EqualTo("期待数: 3 / 作成数: 0 / 欠損数: 3"));
            Assert.That(_content.Root == null && _outcrops == null, Is.True);
        }

        [Test]
        public void CloseCancelsPendingOutcropPlacementAndReclaimsItsInstances()
        {
            var vein = _world.Map.MapVeins[0];
            _world.Map.MapVeins = new List<MapVeinInfoJson>();
            for (var index = 0; index < 51; index++) _world.Map.MapVeins.Add(vein);
            var root = _content.Root.gameObject;
            var completion = ObserveAsync(PlaceAsync()).AsTask();
            Assert.That(completion.IsCompleted, Is.False);
            Assert.That(_outcrops.childCount, Is.EqualTo(50));

            // 配置中のyieldで閉じても、終端キャンセルから所有rootを回収する
            // Closing during a placement yield must reclaim the owned root through terminal cancellation
            StageUtility.GoToMainStage();
            Assert.That(completion.IsCompleted, Is.True);
            Assert.Throws<OperationCanceledException>(() => completion.GetAwaiter().GetResult());
            Assert.That(root == null && _outcrops == null, Is.True);
            Assert.That(_stage.State, Is.EqualTo(GeneratedMapPreviewState.Closed));
        }

        private UniTask PlaceAsync()
        {
            return (UniTask)typeof(GeneratedMapPreviewRun).GetMethod("PlaceOutcropsAsync", PrivateInstance)
                .Invoke(_run, new object[] { _assets, _outcrops, _cancellation.Token });
        }

        private UniTask ObserveAsync(UniTask execution)
        {
            return (UniTask)typeof(GeneratedMapPreviewStage).GetMethod("GenerateAsync", PrivateInstance)
                .Invoke(_stage, new object[] { _run, execution, _cancellation.Token });
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
        }
    }
}
