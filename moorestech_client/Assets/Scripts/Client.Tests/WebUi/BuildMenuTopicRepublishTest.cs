using System;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Construction;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Topics.BuildMenu;
using Core.Master;
using Game.Construction;
using Game.Context;
using Game.PlacementTarget;
using Game.UnlockState;
using Game.UnlockState.States;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.WebUi
{
    /// <summary>
    /// 所持変化の再配信ゲート（ビルドメニュー表示中のみ）の回帰試験
    /// Regression test for the inventory-change republish gate (only while the build menu is up)
    /// </summary>
    public class BuildMenuTopicRepublishTest
    {
        private const int MainSlot = 3;
        private static readonly Guid HeldItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void ビルドメニュー非表示中の所持変化は配り直さない()
        {
            var (topic, controller, controlObject) = CreateTopic();
            try
            {
                controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(HeldItemGuid), 1));

                Assert.IsFalse(IsPublishScheduled(topic));
            }
            finally
            {
                topic.Dispose();
                UnityEngine.Object.DestroyImmediate(controlObject);
            }
        }

        [Test]
        public void ビルドメニュー表示中の所持変化は配り直す()
        {
            var (topic, controller, controlObject) = CreateTopic();
            try
            {
                // 入場自体はBPライブラリのサーバー往復を伴うため、表示中フラグだけを立てて所持変化の分岐を見る
                // Entering the menu would round-trip the blueprint library to the server, so only the active flag is raised to isolate the inventory branch
                SetBuildMenuActive(topic);
                controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(HeldItemGuid), 1));

                Assert.IsTrue(IsPublishScheduled(topic));
            }
            finally
            {
                topic.Dispose();
                UnityEngine.Object.DestroyImmediate(controlObject);
            }
        }

        private static (BuildMenuTopic topic, LocalPlayerInventoryController controller, GameObject controlObject) CreateTopic()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var controlObject = new GameObject("BuildMenuTopicRepublishTest.Control");
            var control = controlObject.AddComponent<UIStateControl>();
            var blueprintLibrary = new ClientBlueprintLibrary();
            var resolver = new PlacementTargetResolver(new PlacementTargetCatalog(), blueprintLibrary, new NothingUnlockedStateData());
            var controller = new LocalPlayerInventoryController(new LocalPlayerInventory(), new LocalPlayerEquipment());
            var topic = new BuildMenuTopic(
                new WebSocketHub(), control, blueprintLibrary, resolver,
                new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore()), controller);
            return (topic, controller, controlObject);
        }

        // 配信予約はフレーム末まで持ち越されるため、予約フラグが再配信の観測点になる
        // The publish is deferred to the frame end, so the scheduled flag is the observation point for a republish
        private static bool IsPublishScheduled(BuildMenuTopic topic)
        {
            return (bool)typeof(BuildMenuTopic)
                .GetField("_publishScheduled", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(topic);
        }

        private static void SetBuildMenuActive(BuildMenuTopic topic)
        {
            typeof(BuildMenuTopic)
                .GetField("_buildMenuActive", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(topic, true);
        }

        /// <summary>
        /// 何も解放していない状態。配信内容ではなく再配信の有無だけを見る
        /// Nothing unlocked; this test watches whether a republish happens, not what it carries
        /// </summary>
        private class NothingUnlockedStateData : IGameUnlockStateData
        {
            public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> BlockUnlockStateInfos { get; } = new Dictionary<Guid, BlockUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, ConnectToolUnlockStateInfo> ConnectToolUnlockStateInfos { get; } = new Dictionary<Guid, ConnectToolUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos { get; } = new Dictionary<Guid, CraftRecipeUnlockStateInfo>();
            public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos { get; } = new Dictionary<ItemId, ItemUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos { get; } = new Dictionary<Guid, ChallengeCategoryUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos { get; } = new Dictionary<Guid, MachineRecipeUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> TrainCarUnlockStateInfos { get; } = new Dictionary<Guid, TrainCarUnlockStateInfo>();
            public bool IsBlueprintUnlocked => false;
        }
    }
}
