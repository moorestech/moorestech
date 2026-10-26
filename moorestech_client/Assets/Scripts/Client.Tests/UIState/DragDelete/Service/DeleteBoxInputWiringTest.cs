using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.UIState.State;
using Game.Block.Interface;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState.State.DragDelete.Box;
using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using Client.Tests.Common;
using Client.Tests.UIState.Fakes;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Client.Localization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.UIState.DragDelete.Service
{
    public class DeleteBoxInputWiringTest : DeleteInputFixture
    {
        [TestCase(true)]
        [TestCase(false)]
        public void 始点押下時の意図を解放と終点確定まで保持する(bool removing)
        {
            var car = CreateCar();
            if (removing) Selection.ApplyBoxTargets(new[] { car }, DeleteSelectionIntent.Add);
            Frame(true, removing ? new[] { Key.LeftShift, Key.LeftCtrl } : new[] { Key.LeftShift });
            Service.Update(0, false);
            // 押下後にCtrlを反転しても始点の意図は変わらない
            // Flipping Ctrl after the press must not change the start intent
            Frame(false, removing ? Array.Empty<Key>() : new[] { Key.LeftCtrl });
            Service.Update(0, false);
            Assert.AreEqual(removing ? DeleteSelectionIntent.Remove : DeleteSelectionIntent.Add, Model().Intent);
            Frame(true, Array.Empty<Key>());
            Service.Update(0, false);
            Frame(false, Array.Empty<Key>());
            Service.Update(0, false);
            Assert.AreEqual(DeleteBoxPhase.Idle, Model().Phase);
            Assert.AreEqual(removing ? 0 : 1, Selection.Count);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void 箱除外の表示復元が失敗してもIdleと全件解除を保つ(int failureIndex)
        {
            var targets = new List<FakeDeleteTarget>();
            for (var index = 0; index < 3; index++)
            {
                var car = CreateCar();
                targets.Add(new FakeDeleteTarget { Key = car.GetDeleteTargetKey(), Removable = true });
            }
            Selection.ApplyBoxTargets(targets, DeleteSelectionIntent.Add);
            targets[failureIndex].ThrowOnReset = true;
            StartRemoveBox();
            Frame(true, Array.Empty<Key>());
            Service.Update(0, false);
            Frame(false, Array.Empty<Key>());
            Assert.Throws<InvalidOperationException>(() => Service.Update(0, false));
            Assert.AreEqual(DeleteBoxPhase.Idle, Model().Phase);
            Assert.AreEqual(0, Selection.Count);
            foreach (var target in targets) Assert.AreEqual(1, target.ResetCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 箱除外通知で進捗を戻し残件数で待ち直す(bool removeAll)
        {
            var car = CreateCar();
            var removed = new FakeDeleteTarget { Key = car.GetDeleteTargetKey(), Removable = true };
            var kept = CreateTarget();
            Selection.ApplyBoxTargets(new[] { removed }, DeleteSelectionIntent.Add);
            if (!removeAll) Selection.ApplyBoxTargets(new[] { kept }, DeleteSelectionIntent.Add);
            StartRemoveBox();
            Frame(false, new[] { Key.F });
            Service.Update(0.5f, false);
            Assert.IsTrue(Progress.IsShown);
            Frame(true, new[] { Key.F });
            Service.Update(0, false);
            Frame(false, new[] { Key.F });
            Service.Update(0, false);
            Assert.AreEqual(0, Progress.CurrentProgress);
            Assert.AreEqual(!removeAll, Progress.IsShown);
            if (removeAll) Assert.AreEqual(0, Selection.Count);
            else
            {
                Service.Update(0.625f, false);
                Assert.AreEqual(0, kept.Target.DeleteCount);
                Service.Update(0.125f, false);
                Assert.AreEqual(1, kept.Target.DeleteCount);
            }
        }

        [Test]
        public void 実Resolverが未選択と撤去保留を除き0件の終点を拒否する()
        {
            var pendingCar = CreateCar();
            var pending = new FakeDeleteTarget { Key = pendingCar.GetDeleteTargetKey(), Removable = true, CompleteImmediately = false };
            Selection.ApplyBoxTargets(new[] { pending }, DeleteSelectionIntent.Add);
            Selection.CommitDelete();
            var unselected = CreateCar();
            var selected = CreateCar();
            Selection.ApplyBoxTargets(new[] { selected }, DeleteSelectionIntent.Add);
            var min = new Vector3Int(10000, 0, 10000);
            Assert.AreEqual(1, Resolver.Resolve(min, min + Vector3Int.one, Selection, DeleteSelectionIntent.Remove).Count);
            Assert.AreEqual(2, Resolver.Resolve(min, min + Vector3Int.one, Selection, DeleteSelectionIntent.Add).Count);
            Selection.ApplyBoxTargets(new[] { selected }, DeleteSelectionIntent.Remove);
            Assert.AreEqual(0, Resolver.Resolve(min, min + Vector3Int.one, Selection, DeleteSelectionIntent.Remove).Count);
            StartRemoveBox();
            var lines = new List<TooltipLine>();
            Frame(true, Array.Empty<Key>());
            Box.Update(Selection, lines, false, DeleteSelectionIntent.Add);
            Frame(false, Array.Empty<Key>());
            lines.Clear();
            Box.Update(Selection, lines, false, DeleteSelectionIntent.Add);
            Assert.AreEqual(DeleteBoxPhase.SelectingEnd, Model().Phase);
            Assert.IsTrue(lines.Exists(line => line.Key.Equals(LocalizationKeys.Ui.Tooltip.DeleteBoxNoSelectedInRange)));
            Assert.AreEqual(0, Selection.Count);
            Assert.IsFalse(Selection.IsSelected(unselected));
        }

        [Test]
        public void 実Resolverが選択中に撤去不可となった対象を残し0件の終点を拒否する()
        {
            var block = CreateComponent<BlockGameObject>("SelectedDeniedBlock");
            var child = block.gameObject.AddComponent<BlockGameObjectChild>();
            child.Init(block);
            var min = new Vector3Int(10000, 0, 10000);
            TestReflection.SetField(block, "<BlockPosInfo>k__BackingField", new BlockPositionInfo(min, BlockDirection.North, Vector3Int.one));
            TestReflection.SetField(block, "<DeleteTarget>k__BackingField", child);
            var blocks = TestReflection.GetField<BlockGameObjectDataStore>(Resolver, "_blockGameObjectDataStore");
            TestReflection.GetField<Dictionary<BlockInstanceId, BlockGameObject>>(blocks, "_blockObjectsByInstanceIdDictionary").Add(new BlockInstanceId(1), block);
            // 表示だけ代役にし、実resolverの対象と同じキーを選択する
            // Substitute only the preview while selecting the real resolver target's key
            Assert.IsTrue(child.IsRemovable(out _));
            Assert.Contains(child, new List<IDeleteTarget>(Resolver.Resolve(min, min + Vector3Int.one, Selection, DeleteSelectionIntent.Add)));
            var selected = new FakeDeleteTarget { Key = block, Removable = true };
            Selection.ApplyBoxTargets(new[] { selected }, DeleteSelectionIntent.Add);
            Assert.IsTrue(Selection.IsSelected(child));
            TestReflection.SetField(child, "_removeDeniedReason", (LocalizationKey?)LocalizationKeys.Ui.Delete.InventoryFull);
            TestReflection.SetField(child, "_removeDeniedReasonUntil", float.PositiveInfinity);
            // 選択を保持したまま撤去不可へ遷移させる
            // Become non-removable while keeping the selection intact
            selected.Removable = false;
            Assert.IsFalse(child.IsRemovable(out _));
            Assert.AreEqual(0, Resolver.Resolve(min, min + Vector3Int.one, Selection, DeleteSelectionIntent.Remove).Count);
            StartRemoveBox();
            var lines = new List<TooltipLine>();
            Frame(true, Array.Empty<Key>());
            Box.Update(Selection, lines, false, DeleteSelectionIntent.Remove);
            Frame(false, Array.Empty<Key>());
            lines.Clear();
            Box.Update(Selection, lines, false, DeleteSelectionIntent.Remove);
            Assert.AreEqual(DeleteBoxPhase.SelectingEnd, Model().Phase);
            Assert.IsTrue(lines.Exists(line => line.Key.Equals(LocalizationKeys.Ui.Tooltip.DeleteBoxNoSelectedInRange)));
            Assert.IsTrue(lines.Exists(line => line.Key.Equals(LocalizationKeys.Ui.Tooltip.DeleteBoxSelectedInRange) && line.TextParams.Count == 1 && line.TextParams[0].Equals(new LocalizationTextParam("count", "0"))));
            Assert.AreEqual(1, Selection.Count);
            Assert.IsTrue(Selection.IsSelected(child));
            Assert.AreEqual(0, selected.ResetCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 箱件数は意図ごとのキーと名前付き件数を渡す(bool removing)
        {
            var car = CreateCar();
            if (removing) Selection.ApplyBoxTargets(new[] { car }, DeleteSelectionIntent.Add);
            Frame(true, removing ? new[] { Key.LeftShift, Key.LeftCtrl } : new[] { Key.LeftShift });
            Service.Update(0, false);
            Frame(false, Array.Empty<Key>());
            var lines = new List<TooltipLine>();
            Box.Update(Selection, lines, false, removing ? DeleteSelectionIntent.Remove : DeleteSelectionIntent.Add);
            var key = removing ? LocalizationKeys.Ui.Tooltip.DeleteBoxSelectedInRange : LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange;
            Assert.IsTrue(lines.Exists(line => line.Key.Equals(key) && line.TextParams.Count == 1 &&
                line.TextParams[0].Equals(new LocalizationTextParam("count", "1"))));
        }

        private void StartRemoveBox()
        {
            var ray = Camera.ScreenPointToRay(Client.Game.InGame.Control.ViewMode.AimPointProvider.GetAimScreenPoint());
            Physics.Raycast(ray, out var hit, float.PositiveInfinity, Client.Common.LayerConst.Without_Player_MapObject_Block_LayerMask);
            Assert.IsTrue(Client.Game.InGame.BlockSystem.PlaceSystem.Util.PlacementUnitCellResolver.TryGetCursorCell(Camera, 0, out var cell), $"hit={hit.collider} point={hit.point} ray={ray}");
            Frame(true, new[] { Key.LeftShift, Key.LeftCtrl });
            Service.Update(0, false);
            Frame(false, Array.Empty<Key>());
            Service.Update(0, false);
            Assert.AreEqual(DeleteBoxPhase.SelectingEnd, Model().Phase);
        }

        private DeleteBoxSelection Model() => TestReflection.GetField<DeleteBoxSelection>(Box, "_selection");
    }
}
