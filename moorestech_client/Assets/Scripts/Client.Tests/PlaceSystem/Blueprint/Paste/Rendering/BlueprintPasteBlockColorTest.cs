using System;
using System.Collections.Generic;
using System.Reflection;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.Context;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using Object = UnityEngine.Object;

namespace Client.Tests.PlaceSystem.Blueprint.Paste
{
    public class BlueprintPasteBlockColorTest
    {
        private GameObject _parent;
        private GameObject _prefab;
        private Material _sourceMaterial;
        private BlockGameObjectPrefabContainer _previousContainer;
        private BlueprintPastePreviewController _preview;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 実ゴースト生成を通すため、最小の描画Prefabを登録する
            // Register a minimal rendered prefab to exercise real ghost creation
            var blockId = ForUnitTestModBlockId.BlockId;
            _parent = new GameObject("BlueprintPasteBlockColorParent");
            _prefab = new GameObject("BlueprintPasteBlockColorPrefab");
            _sourceMaterial = new Material(Shader.Find("Sprites/Default"));
            _prefab.AddComponent<MeshRenderer>().sharedMaterial = _sourceMaterial;
            var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var info = new BlockPrefabInfo(blockId, _prefab, master);
            var container = new BlockGameObjectPrefabContainer(null, new Dictionary<BlockId, BlockPrefabInfo> { { blockId, info } });
            _previousContainer = ClientContext.BlockGameObjectPrefabContainer;
            SetPrefabContainer(container);
            _preview = new BlueprintPastePreviewController(_parent.transform);
        }

        [TearDown]
        public void TearDown()
        {
            SetPrefabContainer(_previousContainer);
            if (_preview != null)
            {
                var lines = typeof(BlueprintPastePreviewController)
                    .GetField("_lines", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_preview);
                var lineRoot = (Transform)lines.GetType()
                    .GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(lines);
                Object.DestroyImmediate(lineRoot.gameObject);
            }
            if (_parent != null)
            {
                foreach (var ghost in _parent.GetComponentsInChildren<BlockPreviewObject>(true))
                {
                    foreach (var renderer in ghost.GetComponentsInChildren<Renderer>(true))
                    {
                        foreach (var material in renderer.sharedMaterials) Object.DestroyImmediate(material);
                    }
                }
                Object.DestroyImmediate(_parent);
            }
            if (_prefab != null) Object.DestroyImmediate(_prefab);
            if (_sourceMaterial != null) Object.DestroyImmediate(_sourceMaterial);
        }

        [Test]
        public void コピー可否と重なりで実ゴースト材質が赤青赤へ切り替わる()
        {
            _preview.UpdatePreview(Plan(BlueprintPasteCopyState.MaterialShortage, true));
            AssertGhostColor(MaterialConst.NotPlaceableColor);

            _preview.UpdatePreview(Plan(BlueprintPasteCopyState.Placeable, true));
            AssertGhostColor(MaterialConst.PlaceableColor);

            _preview.UpdatePreview(Plan(BlueprintPasteCopyState.Placeable, false));
            AssertGhostColor(MaterialConst.NotPlaceableColor);
        }

        private void AssertGhostColor(Color expected)
        {
            var ghost = _parent.GetComponentInChildren<BlockPreviewObject>(true);
            Assert.IsNotNull(ghost);
            Assert.IsTrue(ghost.gameObject.activeSelf);
            var renderer = ghost.GetComponentInChildren<MeshRenderer>();
            var actual = renderer.sharedMaterial.GetColor(MaterialConst.PreviewColorPropertyName);
            Assert.That(actual, Is.EqualTo(expected).Using(ColorEqualityComparer.Instance));
        }

        private static BlueprintPastePlan Plan(BlueprintPasteCopyState state, bool nonOverlap)
        {
            var element = new BlueprintPlacementElement(0, Vector3Int.zero, BlockDirection.North,
                ForUnitTestModBlockId.BlockId, new Dictionary<string, string>());
            var draft = new BlueprintPasteCopyDraft(Vector3Int.zero, true, new[] { element }, new[] { nonOverlap },
                Array.Empty<BlueprintPasteLine>(), 0, 0, 0, true);
            var copy = new BlueprintPasteCopyPlan(draft, state);
            return new BlueprintPastePlan(new[] { copy }, false, Array.Empty<(ItemId, int, int)>());
        }

        private static void SetPrefabContainer(BlockGameObjectPrefabContainer container)
        {
            typeof(ClientContext).GetProperty(nameof(ClientContext.BlockGameObjectPrefabContainer))
                .GetSetMethod(true).Invoke(null, new object[] { container });
        }
    }
}
