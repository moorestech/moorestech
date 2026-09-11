using System;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.Construction;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Construction;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    /// <summary>
    /// 張替え列のコスト先読みがサーバーのセル逐次評価と同値になることを検証する
    /// Verifies that the replace run's cost look-ahead matches the server's cell-by-cell evaluation
    ///
    /// 期待値はサーバー側のBeltReplaceRunCostTest（同条件を実際に走らせたもの）に一致させている
    /// The expectations mirror the server-side BeltReplaceRunCostTest, which runs the very same conditions for real
    /// </summary>
    public class BeltReplaceCostSimulatorTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        private GameObject _dataStoreObject;
        private BlockGameObjectDataStore _dataStore;
        private readonly List<GameObject> _blockObjects = new();

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _dataStoreObject = new GameObject("BlockGameObjectDataStore");
            _dataStore = _dataStoreObject.AddComponent<BlockGameObjectDataStore>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var blockObject in _blockObjects) Object.DestroyImmediate(blockObject);
            _blockObjects.Clear();
            Object.DestroyImmediate(_dataStoreObject);
        }

        [Test]
        public void 所持素材ゼロでも課金元次第で成立する張替え列はPlaceableのまま送られる()
        {
            // 所持素材ゼロ・自分の財布0。GearBeltConveyor(PlacementsPerCost=3)6セルを同コストのLargeGearBeltConveyorへ張り替える
            // No materials and the player's own wallet empty; six GearBeltConveyor cells (PlacementsPerCost=3) become the same-cost LargeGearBeltConveyor
            var placeInfos = BuildReplaceRun(6);

            var simulation = BeltReplaceCostSimulator.TrySimulate(placeInfos, _dataStore, BuildWalletQuery(), Array.Empty<IItemStack>());
            simulation.MarkUnaffordableCellsAsNotPlaceable();

            // 既設を置いて支払ったのが他人なら撤去返却だけで成立する。課金元を知らない見積りで送信を止めない
            // If someone else placed and paid for the existing belts, the removal refund alone makes it work; an estimate blind to the payer never blocks the send
            Assert.IsTrue(placeInfos.TrueForAll(placeInfo => placeInfo.Placeable));

            // 不確実なのは全セル。プレビュー色だけが確実な張替えと分かれる
            // Every cell is uncertain, and only the preview color separates them from a certain replace
            var recorder = new PreviewBlockIndexRecorder();
            simulation.ApplyUncertainRefundColors(recorder);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, recorder.RequestedIndices);
        }

        [Test]
        public void 課金元を見込んでも払えないセルはPlaceableが落ちる()
        {
            // 張替えセルの次に、財布も素材も無い新規設置セルを置く。返却元が無いので仮定のしようがない
            // A brand-new placement cell follows the replace cell with neither wallet nor materials; there is nothing to assume a refund from
            var placeInfos = BuildReplaceRun(1);
            placeInfos.Add(new PlaceInfo { Position = new Vector3Int(0, 0, 10), BlockId = ForUnitTestModBlockId.GearBeltConveyor, IsReplace = false, Placeable = true });

            var simulation = BeltReplaceCostSimulator.TrySimulate(placeInfos, _dataStore, BuildWalletQuery(), Array.Empty<IItemStack>());
            simulation.MarkUnaffordableCellsAsNotPlaceable();

            Assert.IsTrue(placeInfos[0].Placeable);
            Assert.IsFalse(placeInfos[1].Placeable);
        }

        [Test]
        public void 所持1セットあれば6セルの張替えが全てPlaceableのまま残る()
        {
            var placeInfos = BuildReplaceRun(6);

            var simulation = BeltReplaceCostSimulator.TrySimulate(placeInfos, _dataStore, BuildWalletQuery(), BuildOneCostSet());
            simulation.MarkUnaffordableCellsAsNotPlaceable();

            // 1セット払って新財布が満ち、3セル目・6セル目の撤去で1セットずつ戻るので最後まで払い続けられる
            // One paid set fills the new wallet, and the third and sixth removals each hand a set back, so every cell stays payable
            Assert.IsTrue(placeInfos.TrueForAll(placeInfo => placeInfo.Placeable));
        }

        [Test]
        public void 張替えセルの無い列はシミュレートせず通常設置の判定へ委ねる()
        {
            var placeInfos = new List<PlaceInfo>();
            for (var i = 0; i < 3; i++) placeInfos.Add(new PlaceInfo { BlockId = ForUnitTestModBlockId.GearBeltConveyor, Placeable = true });

            Assert.IsNull(BeltReplaceCostSimulator.TrySimulate(placeInfos, _dataStore, BuildWalletQuery(), Array.Empty<IItemStack>()));
        }

        [Test]
        public void 所持1セットの張替え列はCostCheckItemsが凝縮返却分まで個数を含む()
        {
            var placeInfos = BuildReplaceRun(6);

            var simulation = BeltReplaceCostSimulator.TrySimulate(placeInfos, _dataStore, BuildWalletQuery(), BuildOneCostSet());

            // 所持1セット＋3セル目・6セル目の凝縮返却で両素材とも3個に届く
            // Holdings of one set plus the third and sixth cells' condensed refunds bring each material to three
            var costCheckCounts = SumByItemId(simulation.CostCheckItems);
            Assert.AreEqual(3, costCheckCounts[MasterHolder.ItemMaster.GetItemId(Material1Guid)]);
            Assert.AreEqual(3, costCheckCounts[MasterHolder.ItemMaster.GetItemId(Material2Guid)]);
        }

        [Test]
        public void 所持素材ゼロの張替え列は仮定した返却をCostCheckItemsに数えない()
        {
            var placeInfos = BuildReplaceRun(6);

            var simulation = BeltReplaceCostSimulator.TrySimulate(placeInfos, _dataStore, BuildWalletQuery(), Array.Empty<IItemStack>());

            // 自分の財布で確実に凝縮する撤去は1回だけ。課金元不明を見込んだ返却は不足表示に現れない
            // Only one removal condenses for certain against the player's own wallet; refunds assumed from an unknown payer never reach the shortage display
            var costCheckCounts = SumByItemId(simulation.CostCheckItems);
            Assert.AreEqual(1, costCheckCounts[MasterHolder.ItemMaster.GetItemId(Material1Guid)]);
            Assert.AreEqual(1, costCheckCounts[MasterHolder.ItemMaster.GetItemId(Material2Guid)]);
        }

        private List<PlaceInfo> BuildReplaceRun(int length)
        {
            var placeInfos = new List<PlaceInfo>();
            for (var i = 0; i < length; i++)
            {
                var position = new Vector3Int(0, 0, i);
                Register(position, ForUnitTestModBlockId.GearBeltConveyor);
                placeInfos.Add(new PlaceInfo { Position = position, BlockId = ForUnitTestModBlockId.LargeGearBeltConveyor, IsReplace = true, Placeable = true });
            }
            return placeInfos;
        }

        private static Dictionary<ItemId, int> SumByItemId(IReadOnlyList<IItemStack> items)
        {
            var counts = new Dictionary<ItemId, int>();
            foreach (var item in items) counts[item.Id] = counts.GetValueOrDefault(item.Id) + item.Count;
            return counts;
        }

        private static ConstructionWalletQuery BuildWalletQuery()
        {
            return new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
        }

        private static List<IItemStack> BuildOneCostSet()
        {
            var factory = ServerContext.ItemStackFactory;
            return new List<IItemStack>
            {
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 1),
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 1),
            };
        }

        private void Register(Vector3Int position, BlockId blockId)
        {
            var blockObject = new GameObject($"Block_{position}");
            _blockObjects.Add(blockObject);
            var blockGameObject = blockObject.AddComponent<BlockGameObject>();
            SetBackingField(blockGameObject, nameof(BlockGameObject.BlockPosInfo), new BlockPositionInfo(position, BlockDirection.North, Vector3Int.one));
            SetBackingField(blockGameObject, nameof(BlockGameObject.BlockId), blockId);

            var dictionary = (Dictionary<Vector3Int, BlockGameObject>)typeof(BlockGameObjectDataStore)
                .GetField("_blockObjectsDictionary", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_dataStore);
            dictionary.Add(position, blockGameObject);
        }

        // 不確実色を塗りに来たセル添字だけを記録する。実体のマテリアルには触れない
        // Records only the cell indices that came for the uncertain color, never touching a real material
        private class PreviewBlockIndexRecorder : IPlacementPreviewBlockGameObjectController
        {
            public readonly List<int> RequestedIndices = new();

            public bool IsActive => true;

            public void SetPreview(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster) { }

            public IReadOnlyList<bool> DetectGroundOverlaps() => Array.Empty<bool>();

            public void UpdatePlaceableColors(List<PlaceInfo> placeInfos) { }

            public void SetActive(bool active) { }

            public bool TryGetPreviewBlock(int index, out BlockPreviewObject previewBlock)
            {
                RequestedIndices.Add(index);
                previewBlock = null;
                return false;
            }
        }

        private static void SetBackingField(BlockGameObject blockGameObject, string propertyName, object value)
        {
            typeof(BlockGameObject)
                .GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(blockGameObject, value);
        }
    }
}
