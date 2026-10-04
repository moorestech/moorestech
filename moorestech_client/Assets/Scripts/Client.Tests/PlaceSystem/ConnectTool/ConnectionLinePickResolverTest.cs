using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Core.Master;
using Game.UnlockState;
using Game.UnlockState.States;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.ConnectTool
{
    /// <summary>
    ///     接続線のスポイトが線を引いた種類そのものを選び、未解放なら不成立になることを検証する
    ///     Verifies the line eyedropper picks the exact tool the line was drawn with and fails when locked
    /// </summary>
    public class ConnectionLinePickResolverTest
    {
        [Test]
        public void UnlockedLineToolIsPicked()
        {
            // 線の種類が解放済みならその種類の接続ツールを選ぶ
            // An unlocked line tool is picked as-is
            var guid = Guid.NewGuid();
            var result = ConnectionLinePickResolver.Resolve(guid, new FakeUnlockState(guid, true));

            Assert.AreEqual(ConnectionLinePickOutcome.Picked, result.Outcome);
            Assert.AreEqual(guid, ((ConnectToolPlacementTarget)result.Target).ConnectToolGuid);
        }

        [Test]
        public void LockedLineToolFailsPick()
        {
            // 未解放の種類はスポイト自体を不成立にする
            // A locked tool makes the eyedropper fail
            var guid = Guid.NewGuid();
            var result = ConnectionLinePickResolver.Resolve(guid, new FakeUnlockState(guid, false));
            Assert.AreEqual(ConnectionLinePickOutcome.Locked, result.Outcome);
            Assert.IsNull(result.Target);
        }

        [Test]
        public void ToolAbsentFromUnlockStateIsUnknown()
        {
            // 解放状態に無い種類（マスタから消えた等）は理由を分けて不成立にする
            // A tool absent from the unlock state (e.g. removed from the master) fails with its own reason
            var result = ConnectionLinePickResolver.Resolve(Guid.NewGuid(), new FakeUnlockState(Guid.NewGuid(), true));
            Assert.AreEqual(ConnectionLinePickOutcome.UnknownTool, result.Outcome);
            Assert.IsNull(result.Target);
        }

        /// <summary>
        ///     接続ツールの解放状態だけを差し込むテスト用スタブ（前例: CraftActionTest.StubUnlockStateData）
        ///     Test stub injecting only connect-tool unlock state (precedent: CraftActionTest.StubUnlockStateData)
        /// </summary>
        private class FakeUnlockState : IGameUnlockStateData
        {
            public FakeUnlockState(Guid connectToolGuid, bool isUnlocked)
            {
                ConnectToolUnlockStateInfos = new Dictionary<Guid, ConnectToolUnlockStateInfo> { { connectToolGuid, new ConnectToolUnlockStateInfo(connectToolGuid, isUnlocked) } };
            }

            public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos { get; } = new Dictionary<Guid, CraftRecipeUnlockStateInfo>();
            public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos { get; } = new Dictionary<ItemId, ItemUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos { get; } = new Dictionary<Guid, ChallengeCategoryUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos { get; } = new Dictionary<Guid, MachineRecipeUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> BlockUnlockStateInfos { get; } = new Dictionary<Guid, BlockUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> TrainCarUnlockStateInfos { get; } = new Dictionary<Guid, TrainCarUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, ConnectToolUnlockStateInfo> ConnectToolUnlockStateInfos { get; }
            public bool IsBlueprintUnlocked { get; } = false;
        }
    }
}
