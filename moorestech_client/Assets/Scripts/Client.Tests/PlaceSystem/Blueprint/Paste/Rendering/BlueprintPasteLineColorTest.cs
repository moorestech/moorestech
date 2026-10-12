using System;
using System.Collections.Generic;
using System.Reflection;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
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
    public class BlueprintPasteLineColorTest
    {
        private BlueprintPasteLinePreview _preview;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _preview = new BlueprintPasteLinePreview();
            _root = ((Transform)typeof(BlueprintPasteLinePreview)
                .GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_preview)).gameObject;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root == null) return;
            foreach (var renderer in _root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials) Object.DestroyImmediate(material);
            }
            foreach (var filter in _root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null) Object.DestroyImmediate(filter.sharedMesh);
            }
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void 電線はコピーと接続の可否で実材質が赤青赤へ切り替わる()
        {
            Show(BlueprintPasteLineKind.ElectricWire, BlueprintPasteCopyState.MaterialShortage,
                BlueprintPasteLineFailureReason.None);
            AssertWireColor(MaterialConst.NotPlaceableColor);

            // 同じ端点のまま色を変え、メッシュ再構築なしの経路も通す
            // Change color at fixed endpoints to exercise the cached mesh path
            Show(BlueprintPasteLineKind.ElectricWire, BlueprintPasteCopyState.Placeable,
                BlueprintPasteLineFailureReason.None);
            AssertWireColor(MaterialConst.PlaceableColor);

            Show(BlueprintPasteLineKind.ElectricWire, BlueprintPasteCopyState.Placeable,
                BlueprintPasteLineFailureReason.OutOfRange);
            AssertWireColor(MaterialConst.NotPlaceableColor);
        }

        [Test]
        public void チェーンはコピーと接続の可否で二本とも赤青赤へ切り替わる()
        {
            Show(BlueprintPasteLineKind.GearChain, BlueprintPasteCopyState.MaterialShortage,
                BlueprintPasteLineFailureReason.None);
            AssertChainColor(MaterialConst.NotPlaceableColor);

            Show(BlueprintPasteLineKind.GearChain, BlueprintPasteCopyState.Placeable,
                BlueprintPasteLineFailureReason.None);
            AssertChainColor(MaterialConst.PlaceableColor);

            Show(BlueprintPasteLineKind.GearChain, BlueprintPasteCopyState.Placeable,
                BlueprintPasteLineFailureReason.OutOfRange);
            AssertChainColor(MaterialConst.NotPlaceableColor);
        }

        private void Show(BlueprintPasteLineKind kind, BlueprintPasteCopyState state,
            BlueprintPasteLineFailureReason failureReason)
        {
            var blockId = ForUnitTestModBlockId.BlockId;
            var first = new BlueprintPlacementElement(0, Vector3Int.zero, BlockDirection.North,
                blockId, new Dictionary<string, string>());
            var second = new BlueprintPlacementElement(1, Vector3Int.right, BlockDirection.North,
                blockId, new Dictionary<string, string>());
            var line = new BlueprintPasteLine(kind, 0, 1, first.Position, second.Position,
                Guid.Empty, Array.Empty<ConnectToolMaterialCost>(), failureReason);
            var draft = new BlueprintPasteCopyDraft(Vector3Int.zero, true, new[] { first, second },
                new[] { true, true }, new[] { line }, 0, 0, 0, true);
            var copy = new BlueprintPasteCopyPlan(draft, state);
            var plan = new BlueprintPastePlan(new[] { copy }, false, Array.Empty<(ItemId, int, int)>());
            IReadOnlyList<IReadOnlyList<BlockPreviewObject>> ghosts = new[]
            {
                (IReadOnlyList<BlockPreviewObject>)new BlockPreviewObject[] { null, null }
            };
            _preview.Show(plan, ghosts);
        }

        private void AssertWireColor(Color expected)
        {
            var renderer = _root.GetComponentInChildren<MeshRenderer>();
            Assert.IsNotNull(renderer);
            Assert.IsTrue(renderer.gameObject.activeInHierarchy);
            expected.a = 0.5f;
            var actual = renderer.sharedMaterial.GetColor(MaterialConst.PreviewColorPropertyName);
            Assert.That(actual, Is.EqualTo(expected).Using(ColorEqualityComparer.Instance));
        }

        private void AssertChainColor(Color expected)
        {
            var lines = _root.GetComponentsInChildren<LineRenderer>();
            Assert.AreEqual(2, lines.Length);
            foreach (var line in lines)
            {
                Assert.IsTrue(line.gameObject.activeInHierarchy);
                Assert.That(line.startColor, Is.EqualTo(expected).Using(ColorEqualityComparer.Instance));
                Assert.That(line.endColor, Is.EqualTo(expected).Using(ColorEqualityComparer.Instance));
            }
        }
    }
}
