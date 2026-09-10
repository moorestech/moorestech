using System;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace;
using Client.Game.InGame.Construction;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Construction;
using Game.Context;
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
        public void 所持素材ゼロの張替え列はサーバーと同じく1セルもPlaceableに残らない()
        {
            // 所持素材ゼロ・両財布0。GearBeltConveyor(PlacementsPerCost=3)6セルを同コストのLargeGearBeltConveyorへ張り替える
            // No materials and both wallets empty; six GearBeltConveyor cells (PlacementsPerCost=3) become the same-cost LargeGearBeltConveyor
            var placeInfos = BuildReplaceRun(6);

            var simulation = BeltReplaceCostSimulator.TrySimulate(placeInfos, _dataStore, BuildWalletQuery(), Array.Empty<IItemStack>());
            simulation.MarkUnaffordableCellsAsNotPlaceable();

            // 先頭セルが払えず撤去も財布操作も起きないため、後続セルの状態も1つも進まない
            // The first cell cannot pay and nothing is removed or moved in the wallet, so no later cell's state advances either
            Assert.IsTrue(placeInfos.TrueForAll(placeInfo => !placeInfo.Placeable));
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

        private static void SetBackingField(BlockGameObject blockGameObject, string propertyName, object value)
        {
            typeof(BlockGameObject)
                .GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(blockGameObject, value);
        }
    }
}
