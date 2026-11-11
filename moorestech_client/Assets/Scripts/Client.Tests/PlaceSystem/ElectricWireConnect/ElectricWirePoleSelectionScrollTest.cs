using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Tests.Common;
using Core.Master;
using Game.Context;
using Game.UnlockState;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    /// 本番の読み取り経路（Mouse.current.scroll→蓄積器）で、ホイール1ノッチが電柱の種類を1段進めることを見る
    /// Through the production read path (Mouse.current.scroll to the accumulator), one wheel notch advances the pole type by one
    /// </summary>
    public class ElectricWirePoleSelectionScrollTest : InputTestFixture
    {
        private Mouse _mouse;
        private ElectricWirePoleSelection _selection;

        public override void Setup()
        {
            base.Setup();
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            TestReflection.ResetInputManagerCache();

            // ロック済み電柱も解放し、サイクルが成り立つ2種にする
            // Also unlock the locked pole so there are two types to cycle through
            var unlockState = ServerContext.GetService<IGameUnlockStateDataController>();
            unlockState.UnlockBlock(MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.LockedElectricPoleId).BlockGuid);
            _selection = new ElectricWirePoleSelection();
            _selection.RefreshUnlockedPoles(unlockState);
        }

        public override void TearDown()
        {
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void ホイール1ノッチで電柱の種類が1段進み逆1ノッチで戻る()
        {
            // 前提: 2種がサイクル対象になっている（1種なら何も試していない）
            // Premise: two types are cyclable (with one type this case tests nothing)
            Assert.IsTrue(_selection.CanCyclePoleType);
            var first = SelectedPoleId();

            // Uniform 設定の実マウス1ノッチと同じ 1.0 を入れる。換算で縮むと 0 段になりここで落ちる
            // Feed 1.0, the same as one real notch under Uniform; a shrinking conversion yields zero steps and fails here
            Set(_mouse.scroll, new Vector2(0f, 1f));
            _selection.UpdateInput();
            var second = SelectedPoleId();
            Assert.AreNotEqual(first, second, "上へ1ノッチで電柱の種類が進まなかった");

            Set(_mouse.scroll, new Vector2(0f, -1f));
            _selection.UpdateInput();
            Assert.AreEqual(first, SelectedPoleId(), "下へ1ノッチで電柱の種類が戻らなかった");
        }

        private BlockId SelectedPoleId()
        {
            _selection.TryGetSelectedPole(out var blockId, out _);
            return blockId;
        }
    }
}
