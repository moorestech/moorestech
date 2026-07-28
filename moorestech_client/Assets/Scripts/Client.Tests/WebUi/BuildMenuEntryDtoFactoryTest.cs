using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.WebUiHost.Game.Topics.BuildMenu;
using Core.Master;
using Game.PlacementTarget;
using Game.UnlockState;
using Game.UnlockState.States;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.WebUi
{
    /// <summary>
    /// BuildMenuEntryDtoFactory.CreateDtos の回帰テスト: 実DTO生成でid=Guid・kindが契約5値・idの一意性を検証する
    /// Regression test for BuildMenuEntryDtoFactory.CreateDtos: verifies id is a GUID, kind is one of the contract's 5 values, and ids are unique, via real DTO generation
    /// </summary>
    public class BuildMenuEntryDtoFactoryTest
    {
        // Web契約が許すkind文字列5値（buildMenu.tsのBuildMenuEntryKindSchemaと同一集合）
        // The 5 kind strings the web contract allows (same set as buildMenu.ts's BuildMenuEntryKindSchema)
        private static readonly HashSet<string> AllowedKinds = new() { "block", "trainCar", "connectTool", "buildTool", "blueprint" };

        [Test]
        public void CreateDtosは全件がGuid形状のidと契約5値のkindをユニークに持つ()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var unlockState = new AllBlockAndConnectToolUnlockedStateData();

            var dtos = BuildMenuEntryDtoFactory.CreateDtos(unlockState, new PlacementTargetCatalog(new ClientBlueprintLibrary()));

            // 実マスタ規模で複数エントリが返ること（空リストでは以降の検証が無意味）
            // Multiple entries must come back at real-master scale (an empty list would make the rest of this test meaningless)
            Assert.Greater(dtos.Count, 0);

            foreach (var dto in dtos)
            {
                Assert.IsTrue(Guid.TryParse(dto.Id, out _), $"id is not GUID-shaped: {dto.Id}");
                Assert.IsTrue(AllowedKinds.Contains(dto.Kind), $"kind is not one of the contract's 5 values: {dto.Kind}");
            }

            var ids = dtos.Select(d => d.Id).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
        }

        /// <summary>
        /// ブロック・接続ツールを全解放するスタブ。車両は意図的に未解放のまま残す
        /// （ClientContext.TrainCarImageContainerはゲーム起動なしでは未初期化のため、車両分岐に踏み込ませない）
        /// Stub that unlocks every block and connect tool. Train cars are deliberately left locked
        /// (ClientContext.TrainCarImageContainer stays uninitialized without a game boot, so the train-car branch is avoided)
        /// </summary>
        private class AllBlockAndConnectToolUnlockedStateData : IGameUnlockStateData
        {
            public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> BlockUnlockStateInfos { get; } =
                MasterHolder.BlockMaster.Blocks.Data.ToDictionary(b => b.BlockGuid, b => new BlockUnlockStateInfo(b.BlockGuid, true));

            public IReadOnlyDictionary<Guid, ConnectToolUnlockStateInfo> ConnectToolUnlockStateInfos { get; } =
                MasterHolder.ConnectToolMaster.All.ToDictionary(c => c.ConnectToolGuid, c => new ConnectToolUnlockStateInfo(c.ConnectToolGuid, true));

            public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos { get; } = new Dictionary<Guid, CraftRecipeUnlockStateInfo>();
            public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos { get; } = new Dictionary<ItemId, ItemUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos { get; } = new Dictionary<Guid, ChallengeCategoryUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos { get; } = new Dictionary<Guid, MachineRecipeUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> TrainCarUnlockStateInfos { get; } = new Dictionary<Guid, TrainCarUnlockStateInfo>();
        }
    }
}
