using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.MapVein;
using Client.Network.API;
using Game.MapGeneration.Transfer;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.MapData;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Map
{
    /// <summary>
    ///     チュートリアルが指す1鉱脈だけを描く強調モードと、GUID指定の内包判定を検証する
    ///     Verifies the highlight mode that draws only the vein a tutorial points at, and the per-GUID containment query
    /// </summary>
    public class MapVeinRangeViewHighlightTest
    {
        private const string ItemVeinAGuid = "11111111-0000-0000-0000-000000000001";
        private const string ItemVeinBGuid = "11111111-0000-0000-0000-000000000004";
        private const string FluidVeinGuid = "11111111-0000-0000-0000-000000000002";

        private static readonly Guid ItemVeinA = Guid.Parse(ItemVeinAGuid);
        private static readonly Guid ItemVeinB = Guid.Parse(ItemVeinBGuid);

        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _camera = new GameObject("MapVeinRangeViewHighlightTestCamera").AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            var root = GameObject.Find(MapVeinRangeViewService.RootObjectName);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (_camera != null) UnityEngine.Object.DestroyImmediate(_camera.gameObject);
        }

        [Test]
        public void 強調鉱脈を指定するとその鉱脈だけを別マテリアルで描く()
        {
            var (service, root) = CreateService();
            service.SetVeinDisplay(VeinDisplay.OfKind(MapVeinKind.Item));
            Assert.AreEqual(2, CountVisibleBoxes(root));

            service.SetVeinDisplay(VeinDisplay.OfVeinType(ItemVeinA));

            Assert.AreEqual(1, CountVisibleBoxes(root), "highlight mode must show exactly the target vein");
            foreach (Transform child in root)
            {
                if (!child.gameObject.activeSelf) continue;
                StringAssert.Contains("Highlight", child.GetComponent<MeshRenderer>().sharedMaterial.name);
            }
        }

        [Test]
        public void 強調は表示種別を無視して対象鉱脈を描く()
        {
            var (service, root) = CreateService();
            service.SetVeinDisplay(VeinDisplay.OfKind(MapVeinKind.Fluid));

            service.SetVeinDisplay(VeinDisplay.OfVeinType(ItemVeinB));

            Assert.AreEqual(1, CountVisibleBoxes(root));
        }

        [Test]
        public void 強調を解除すると種別表示へ戻る()
        {
            var (service, root) = CreateService();
            service.SetVeinDisplay(VeinDisplay.OfKind(MapVeinKind.Item));
            service.SetVeinDisplay(VeinDisplay.OfVeinType(ItemVeinB));

            service.SetVeinDisplay(VeinDisplay.OfKind(MapVeinKind.Item));

            Assert.AreEqual(2, CountVisibleBoxes(root));
            foreach (Transform child in root)
            {
                if (!child.gameObject.activeSelf) continue;
                StringAssert.Contains("Item", child.GetComponent<MeshRenderer>().sharedMaterial.name);
            }
        }

        [Test]
        public void 台帳はGUID指定の内包判定を返す()
        {
            var registry = new MapVeinAabbRegistry(CreateHandshakeResponse());

            Assert.IsTrue(registry.IsInsideAnyVeinOfType(new Vector3Int(1, 1, 1), ItemVeinA));
            Assert.IsFalse(registry.IsInsideAnyVeinOfType(new Vector3Int(1, 1, 1), ItemVeinB));
            Assert.IsTrue(registry.IsInsideAnyVeinOfType(new Vector3Int(30, 0, 30), ItemVeinB));
        }

        private (MapVeinRangeViewService service, Transform root) CreateService()
        {
            var service = new MapVeinRangeViewService(new MapVeinAabbRegistry(CreateHandshakeResponse()), _camera);
            return (service, GameObject.Find(MapVeinRangeViewService.RootObjectName).transform);
        }

        private static int CountVisibleBoxes(Transform rangeViewRoot)
        {
            var count = 0;
            foreach (Transform child in rangeViewRoot)
                if (child.gameObject.activeSelf) count++;
            return count;
        }

        private InitialHandshakeResponse CreateHandshakeResponse()
        {
            // 強調対象のitem鉱脈2本と、種別違いを混ぜるためのfluid鉱脈1本
            // Two item veins to switch the highlight between, plus one fluid vein to mix kinds in
            var veinLayouts = new List<VeinLayoutMessagePack>
            {
                new(ItemVeinAGuid, 0, 0, 0, 2, 2, 2),
                new(FluidVeinGuid, 4, 0, 4, 6, 2, 6),
                new(ItemVeinBGuid, 30, 0, 30, 31, 0, 31)
            };
            var mapLayout = new GetMapDataProtocol.ResponseMapDataMessagePack(new Vector3MessagePack(Vector3.zero),
                new List<MapObjectLayoutMessagePack>(), veinLayouts, TerrainTransferMeta.CreateWithoutWorldDirectory(), string.Empty);
            var handshake = new InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack(new Vector3MessagePack(Vector3.zero), null, -1, null, null, null);

            return new InitialHandshakeResponse(handshake, (default, default, default, default, default, default, default, mapLayout));
        }
    }
}
