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
using static Client.Tests.Map.Vein.MapVeinAabbRegistryFixture;

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

        // item2本+fluid1本。同時表示は種別で絞られるため、種別ごとの本数で数える
        // Two item veins plus one fluid; display is filtered per kind, so counts are per kind
        private const int ItemVeinCount = 2;
        private const int FluidVeinCount = 1;
        private const int VeinTypeCount = 2;

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
            var registry = new MapVeinAabbRegistry(CreateHandshakeResponse());
            var service = new MapVeinRangeViewService(registry, _camera);
            var root = GameObject.Find(MapVeinRangeViewService.RootObjectName).transform;

            // 指定した種別のveinだけが表示されること。種別の絞り込みが効かないと本数で落ちる
            // Only the requested kind shows; a broken kind filter fails on the count
            service.SetVeinDisplay(VeinDisplay.OfVeins(SelectVeinsOfKind(registry, MapVeinKind.Item), false));
            Assert.AreEqual(ItemVeinCount, CountVisibleBoxes(root), "item veins did not get exactly one range view box each");

            var sharedMaterials = CollectVisibleBoxMaterials(root);
            service.SetVeinDisplay(VeinDisplay.OfVeins(SelectVeinsOfKind(registry, MapVeinKind.Fluid), false));
            Assert.AreEqual(FluidVeinCount, CountVisibleBoxes(root), "fluid veins did not get exactly one range view box each");

            // item側とfluid側で材質は合計2枚だけ。ボックス毎に作っていれば3枚になり、数で分岐して落ちる
            // The item and fluid sides share only two materials in total; per-box creation would make it three and diverge by count alone
            sharedMaterials.UnionWith(CollectVisibleBoxMaterials(root));
            Assert.AreEqual(VeinTypeCount, sharedMaterials.Count, "range boxes do not share one material per vein type");

            var materialBaseline = CountRangeBoxMaterials();
            var boxBaseline = root.childCount;

            for (var cycle = 0; cycle < ShowHideCycleCount; cycle++)
            {
                // 種別ごとに同じMaterialインスタンスが戻ってくること。表示毎の作り直しは命名にも破棄挙動にも依らずここで落ちる
                // The very same Material instance must come back for each kind; per-show rebuilding fails here without relying on naming or destroy behaviour
                var cycleMaterials = new HashSet<Material>();
                foreach (var veinKind in new[] { MapVeinKind.Item, MapVeinKind.Fluid })
                {
                    service.SetVeinDisplay(VeinDisplay.Hidden);
                    service.SetVeinDisplay(VeinDisplay.OfVeins(SelectVeinsOfKind(registry, veinKind), false));
                    cycleMaterials.UnionWith(CollectVisibleBoxMaterials(root));
                }

                Assert.IsTrue(sharedMaterials.SetEquals(cycleMaterials), $"range box materials were rebuilt on cycle {cycle}");
                Assert.AreEqual(materialBaseline, CountRangeBoxMaterials(), $"range box materials increased on cycle {cycle}");
                Assert.AreEqual(boxBaseline, root.childCount, $"range box objects increased on cycle {cycle}");
            }

            #region Internal

            int CountRangeBoxMaterials()
            {
                // 破棄されずに残ったマテリアルも拾うので、増分が0であることがリーク無しの証拠になる
                // This also picks up materials nobody destroyed, so a zero delta is what proves there is no leak
                var count = 0;
                foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
                    if (material.name.StartsWith(MapVeinRangeBoxMaterials.MaterialNamePrefix)) count++;
                return count;
            }

            int CountVisibleBoxes(Transform rangeViewRoot)
            {
                var count = 0;
                foreach (Transform child in rangeViewRoot)
                    if (child.gameObject.activeSelf) count++;
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
            // itemを2本並べる。ForUnitTestのitem veinは1種しかないが、範囲表示はguid重複を問題にしない
            // Two item veins side by side; ForUnitTest only defines one item vein, and the range view does not care about duplicate guids
            var veinLayouts = new List<VeinLayoutMessagePack>
            {
                new(ItemVeinGuid, 0, 0, 0, 2, 2, 2),
                new(ItemVeinGuid, 8, 0, 8, 10, 2, 10),
                new(FluidVeinGuid, 4, 0, 4, 6, 2, 6)
            };
            var mapLayout = new GetMapDataProtocol.ResponseMapDataMessagePack(new Vector3MessagePack(Vector3.zero),
                new List<MapObjectLayoutMessagePack>(), veinLayouts, TerrainTransferMeta.CreateWithoutWorldDirectory(), string.Empty);
            var handshake = new InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack(new Vector3MessagePack(Vector3.zero), null, -1, null, null, null);

            return new InitialHandshakeResponse(handshake, (default, default, default, default, default, default, default, mapLayout));
        }
    }
}
