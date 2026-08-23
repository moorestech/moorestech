using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BuildMenuModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    /// 自動接続プレビューのconnectTool選定が、サーバーと同じ解放フィルタで動くことを検証する
    /// Verifies the auto-connect preview's connectTool selection applies the same unlock filter as the server
    /// </summary>
    public class ElectricWireAutoConnectToolSelectorTest
    {
        private static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        private IGameUnlockStateDataController _unlockState;

        [SetUp]
        public void SetUp()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();
        }

        [Test]
        public void 解放済みelectricWireが0件なら配線なしで設置可となりコストは0になる()
        {
            // テストmodのconnectToolは全てinitialUnlocked=falseなので初期状態は全未解放
            // Every connectTool in the test mod is initialUnlocked=false, so all start locked
            Assert.IsTrue(_unlockState.ConnectToolUnlockStateInfos.Values.All(info => !info.IsUnlocked));

            var selected = ElectricWireAutoConnectToolSelector.TrySelect(CreateTargets(), CreateVirtualInventory(), _unlockState, out var materials, out var cost);

            // 接続先はあるが解放済みツールが無いため、配線なしで設置だけ許可される
            // Targets exist but nothing is unlocked, so placement is allowed with no wiring at all
            Assert.IsTrue(selected);
            Assert.AreEqual(0, cost);
            Assert.IsNull(materials);
        }

        [Test]
        public void electricWireを解放するとコストと消費素材が選定される()
        {
            _unlockState.UnlockConnectTool(FirstElectricWireToolGuid());

            var selected = ElectricWireAutoConnectToolSelector.TrySelect(CreateTargets(), CreateVirtualInventory(), _unlockState, out var materials, out var cost);

            // 解放済みツールが選ばれ、距離に応じた電線コストと消費素材が返る
            // The unlocked tool is picked, returning the distance-based wire cost and the materials to consume
            Assert.IsTrue(selected);
            Assert.Less(0, cost);
            Assert.IsNotNull(materials);
            Assert.IsTrue(materials.Any(material => material.ItemId == MasterHolder.ItemMaster.GetItemId(WireItemGuid) && 0 < material.Count));
        }

        #region TestUtil

        private static Guid FirstElectricWireToolGuid()
        {
            return MasterHolder.ConnectToolMaster.All
                .Where(element => element.ToolType == ConnectToolMasterElement.ToolTypeConst.electricWire)
                .OrderBy(element => element.SortPriority)
                .First().ConnectToolGuid;
        }

        private static List<(Vector3Int TargetPos, float Distance)> CreateTargets()
        {
            return new List<(Vector3Int, float)> { (new Vector3Int(3, 0, 0), 3f) };
        }

        private static ElectricWireAutoConnectVirtualInventory CreateVirtualInventory()
        {
            // 電線を潤沢に持たせ、素材不足で選定が落ちないようにする
            // Hold plenty of wire so the selection never fails for lack of materials
            var wireStack = ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(WireItemGuid), 100);
            return new ElectricWireAutoConnectVirtualInventory(new StubLocalPlayerInventory(wireStack), Array.Empty<(ItemId itemId, int count)>());
        }

        // 仮想在庫が読むのは列挙だけなので、所持アイテムを列挙するだけのスタブを使う
        // The virtual inventory only enumerates, so a stub that merely lists held items is enough
        private class StubLocalPlayerInventory : ILocalPlayerInventory
        {
            private readonly List<IItemStack> _items;
            private readonly Subject<int> _onItemChange = new();

            public StubLocalPlayerInventory(IItemStack itemStack)
            {
                _items = new List<IItemStack> { itemStack };
            }

            public IItemStack this[int index] => _items[index];
            public IObservable<int> OnItemChange => _onItemChange;
            public int Count => _items.Count;
            public int MainSlotCount => _items.Count;
            public bool IsItemExist(ItemId itemId, int itemSlot) => _items[itemSlot].Id == itemId;
            public IEnumerator<IItemStack> GetEnumerator() => _items.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        #endregion
    }
}
