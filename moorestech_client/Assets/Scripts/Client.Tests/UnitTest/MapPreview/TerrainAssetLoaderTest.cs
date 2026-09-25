using System;
using System.Threading;
using Client.Game.InGame.Environment.Terrain.Assets;
using Client.Game.InGame.Environment.Terrain.Build;
using Client.MapScene.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.UnitTest.MapPreview
{
    public class TerrainAssetLoaderTest
    {
        private const string TreeAddress = "Tests/TerrainAssetLoader/Tree";
        private const string GrassAddress = "Tests/TerrainAssetLoader/Grass";
        private string _assetFolder;
        private AddressableAssetSettings _fixtureSettings;
        private AddressableAssetGroup _fixtureGroup;
        private AddressableAssetSettings _projectSettings;

        [SetUp]
        public void SetUp()
        {
            _assetFolder = $"Assets/TerrainAssetLoaderTest_{Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", _assetFolder.Substring("Assets/".Length));

            // 未保存のfixtureグループだけを一時追加し、実登録の保存や編集はしない
            // Temporarily attach an unsaved fixture group without saving or editing real registrations
            _fixtureSettings = AddressableAssetSettings.Create(_assetFolder, "FixtureSettings", false, false);
            _fixtureGroup = _fixtureSettings.CreateGroup("Fixture", false, false, false, null);
            _projectSettings = AddressableAssetSettingsDefaultObject.Settings;
            _projectSettings.groups.Add(_fixtureGroup);
            CreatePrefab("Tree", TreeAddress);
            CreatePrefab("Grass", GrassAddress);
        }

        [TearDown]
        public void TearDown()
        {
            _projectSettings.groups.Remove(_fixtureGroup);
            Object.DestroyImmediate(_fixtureGroup);
            Object.DestroyImmediate(_fixtureSettings);
            AssetDatabase.DeleteAsset(_assetFolder);
        }

        [Test]
        public void ResolvesMaterialAndTrackedFixturePrefabsAsBorrowedAssets()
        {
            var assets = new EditorTerrainAssetLoader();
            var material = TerrainMaterialAssetLoader.LoadAsync(assets, CancellationToken.None).GetAwaiter().GetResult();
            var tree = assets.LoadAsync<GameObject>(TreeAddress, CancellationToken.None).GetAwaiter().GetResult();
            var grass = assets.LoadAsync<GameObject>(GrassAddress, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(material, Is.SameAs(AssetDatabase.LoadAssetAtPath<Material>("Assets/AddressableResources/Environment/Terrain/TerrainLitMaterial.mat")));
            Assert.That(tree, Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>($"{_assetFolder}/Tree.prefab")));
            Assert.That(grass, Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>($"{_assetFolder}/Grass.prefab")));
            Assert.That(AssetDatabase.Contains(material) && AssetDatabase.Contains(tree) && AssetDatabase.Contains(grass), Is.True);
        }

        [Test]
        public void ExpandsRegisteredFoldersAndBorrowsTextureWithoutDestroyingIt()
        {
            AssetDatabase.CreateFolder(_assetFolder, "Nested");
            var texturePath = $"{_assetFolder}/Nested/Grass.asset";
            AssetDatabase.CreateAsset(new Texture2D(2, 2), texturePath);
            Register(_assetFolder, "Tests/TerrainFolder");
            var assets = new EditorTerrainAssetLoader();

            var loaded = assets.LoadAsync<Texture2D>("Tests/TerrainFolder/Nested/Grass.asset", CancellationToken.None).GetAwaiter().GetResult();

            // インポートで再生成される作成時のラッパーでなく、現在の永続資産と照合する
            // Compare with the current persistent asset rather than the creation wrapper replaced by import
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            Assert.That(loaded, Is.SameAs(texture));
            Assert.That(AssetDatabase.Contains(texture), Is.True);
            Assert.That(new EditorTerrainAssetLoader().LoadAsync<Texture2D>("Tests/TerrainFolder/Nested/Grass.asset", CancellationToken.None).GetAwaiter().GetResult(), Is.SameAs(texture));
        }

        [Test]
        public void RejectsRequestedDuplicateAddressWithAllCandidatePaths()
        {
            CreateTexture("First", "Tests/Duplicate");
            CreateTexture("Second", "Tests/Duplicate");

            var assets = new EditorTerrainAssetLoader();
            var exception = Assert.Throws<InvalidOperationException>(() => assets.LoadAsync<Texture2D>("Tests/Duplicate", CancellationToken.None).GetAwaiter().GetResult());

            Assert.That(exception.Message, Does.Contain("Tests/Duplicate").And.Contain("First.asset").And.Contain("Second.asset"));
        }

        [Test]
        public void ResolvesUniqueAddressDespiteUnrelatedDuplicate()
        {
            CreateTexture("First", "Tests/Duplicate");
            CreateTexture("Second", "Tests/Duplicate");
            var assets = new EditorTerrainAssetLoader();

            Assert.That(assets.LoadAsync<GameObject>(TreeAddress, CancellationToken.None).GetAwaiter().GetResult(), Is.Not.Null);
        }

        [TestCase("Tests/Absent")]
        [TestCase("tests/TerrainAssetLoader/Tree")]
        public void RejectsMissingOrWrongCaseAddressWithRequestedType(string address)
        {
            var assets = new EditorTerrainAssetLoader();
            var exception = Assert.Throws<InvalidOperationException>(() => assets.LoadAsync<Texture2D>(address, CancellationToken.None).GetAwaiter().GetResult());

            Assert.That(exception.Message, Does.Contain(address).And.Contain(nameof(Texture2D)));
        }

        [Test]
        public void RejectsWrongAssetTypeWithoutDestroyingBorrowedPrefab()
        {
            var assets = new EditorTerrainAssetLoader();
            var tree = assets.LoadAsync<GameObject>(TreeAddress, CancellationToken.None).GetAwaiter().GetResult();
            var exception = Assert.Throws<InvalidOperationException>(() => assets.LoadAsync<Texture2D>(TreeAddress, CancellationToken.None).GetAwaiter().GetResult());

            Assert.That(exception.Message, Does.Contain(TreeAddress).And.Contain(nameof(Texture2D)));
            Assert.That(AssetDatabase.Contains(tree), Is.True);
        }

        [Test]
        public void RejectsAssetDeletedAfterAddressSnapshot()
        {
            CreateTexture("Deleted", "Tests/Deleted");
            var assets = new EditorTerrainAssetLoader();
            AssetDatabase.DeleteAsset($"{_assetFolder}/Deleted.asset");
            var exception = Assert.Throws<InvalidOperationException>(() => assets.LoadAsync<Texture2D>("Tests/Deleted", CancellationToken.None).GetAwaiter().GetResult());

            Assert.That(exception.Message, Does.Contain("Tests/Deleted").And.Contain(nameof(Texture2D)));
        }

        [Test]
        public void CancellationPrecedesAddressResolution()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var assets = new EditorTerrainAssetLoader();

            Assert.Throws<OperationCanceledException>(() => assets.LoadAsync<Texture2D>("Tests/Absent", cancellation.Token).GetAwaiter().GetResult());
        }

        [Test]
        public void SharedLayerLoaderPreservesRequestedColumnOrder()
        {
            var first = new TerrainLayer();
            var second = new TerrainLayer();
            AssetDatabase.CreateAsset(first, $"{_assetFolder}/First.terrainlayer");
            AssetDatabase.CreateAsset(second, $"{_assetFolder}/Second.terrainlayer");
            Register($"{_assetFolder}/First.terrainlayer", "Tests/FirstLayer");
            Register($"{_assetFolder}/Second.terrainlayer", "Tests/SecondLayer");

            var layers = TerrainLayerAssetLoader.LoadAsync(new[] { "Tests/SecondLayer", "Tests/FirstLayer" }, new EditorTerrainAssetLoader(), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(layers, Is.EqualTo(new[] { second, first }));
        }

        private void CreateTexture(string name, string address)
        {
            var path = $"{_assetFolder}/{name}.asset";
            AssetDatabase.CreateAsset(new Texture2D(2, 2), path);
            Register(path, address);
        }

        private void CreatePrefab(string name, string address)
        {
            var instance = new GameObject(name);
            var path = $"{_assetFolder}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            Register(path, address);
        }

        private void Register(string path, string address)
        {
            _fixtureSettings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), _fixtureGroup, false, false).SetAddress(address, false);
        }
    }
}
