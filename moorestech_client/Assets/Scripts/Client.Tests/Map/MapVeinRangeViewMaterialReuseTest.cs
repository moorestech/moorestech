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
    ///     範囲表示の表示/非表示を繰り返してもマテリアルとボックスが作り捨てられないことを検証する
    ///     Verifies that repeated show/hide of the range view never throws away and rebuilds materials or boxes
    /// </summary>
    public class MapVeinRangeViewMaterialReuseTest
    {
        // ForUnitTest map.json に定義済みのテスト用鉱脈GUID。item/fluidの2色を両方作らせる
        // Test vein GUIDs defined in ForUnitTest map.json; these force both the item and the fluid color into existence
        private const string ItemVeinGuid = "11111111-0000-0000-0000-000000000001";
        private const string FluidVeinGuid = "11111111-0000-0000-0000-000000000002";

        // 1周では溜まりが見えないので複数周させる
        // A single cycle would not reveal accumulation, so run several
        private const int ShowHideCycleCount = 5;

        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            // DIコンテナ生成でMasterHolderをForUnitTest modからロードする
            // Load MasterHolder from ForUnitTest mod via DI container generation
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            _camera = new GameObject("MapVeinRangeViewTestCamera").AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            // EditModeなので開いているシーンに残さないよう即時破棄する
            // This runs in EditMode, so destroy immediately to leave nothing behind in the open scene
            var root = GameObject.Find(MapVeinRangeViewService.RootObjectName);
            if (root != null) Object.DestroyImmediate(root);
            if (_camera != null) Object.DestroyImmediate(_camera.gameObject);
        }

        [Test]
        public void 表示と非表示を繰り返してもマテリアルとボックスが増えない()
        {
            var service = new MapVeinRangeViewService(CreateHandshakeResponse(), _camera);
            var root = GameObject.Find(MapVeinRangeViewService.RootObjectName).transform;

            // 1周目でボックスとマテリアルが揃った状態を基準にする
            // Take the state after the first cycle, once boxes and materials exist, as the baseline
            RunShowHideCycle(service);
            var materialBaseline = CountRangeBoxMaterials();
            var boxBaseline = root.childCount;

            for (var cycle = 0; cycle < ShowHideCycleCount; cycle++)
            {
                RunShowHideCycle(service);
                Assert.AreEqual(materialBaseline, CountRangeBoxMaterials(), $"range box materials increased on cycle {cycle}");
                Assert.AreEqual(boxBaseline, root.childCount, $"range box objects increased on cycle {cycle}");
            }

            // 使われている材質はitem/fluidの2枚だけ。ボックス毎・表示毎の生成はここで落ちる
            // Only two materials are ever in use, one per vein type; per-box or per-show creation fails here
            service.ManualUpdate(true);
            Assert.AreEqual(2, CollectVisibleBoxMaterials(root).Count, "range boxes do not share one material per vein type");

            #region Internal

            void RunShowHideCycle(MapVeinRangeViewService rangeView)
            {
                rangeView.ManualUpdate(true);
                rangeView.ManualUpdate(false);
            }

            int CountRangeBoxMaterials()
            {
                // 破棄されずに残ったマテリアルも拾うので、増分が0であることがリーク無しの証拠になる
                // This also picks up materials nobody destroyed, so a zero delta is what proves there is no leak
                var count = 0;
                foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
                    if (material.name.StartsWith(MapVeinRangeBoxMaterials.MaterialNamePrefix)) count++;
                return count;
            }

            HashSet<Material> CollectVisibleBoxMaterials(Transform rangeViewRoot)
            {
                var materials = new HashSet<Material>();
                foreach (Transform child in rangeViewRoot)
                    if (child.gameObject.activeSelf) materials.Add(child.GetComponent<MeshRenderer>().sharedMaterial);
                return materials;
            }

            #endregion
        }

        private InitialHandshakeResponse CreateHandshakeResponse()
        {
            // 範囲表示が読むのはMapLayout.MapVeinsだけなので、他の応答はdefaultで埋める
            // The range view only reads MapLayout.MapVeins, so every other response is left at default
            var veinLayouts = new List<VeinLayoutMessagePack>
            {
                new(ItemVeinGuid, 0, 0, 0, 2, 2, 2),
                new(FluidVeinGuid, 4, 0, 4, 6, 2, 6)
            };
            var mapLayout = new GetMapDataProtocol.ResponseMapDataMessagePack(new Vector3MessagePack(Vector3.zero),
                new List<MapObjectLayoutMessagePack>(), veinLayouts, TerrainTransferMeta.CreateWithoutWorldDirectory(), string.Empty);
            var handshake = new InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack(new Vector3MessagePack(Vector3.zero), null, -1, null);

            return new InitialHandshakeResponse(handshake, (default, default, default, default, default, default, default, mapLayout));
        }
    }
}
