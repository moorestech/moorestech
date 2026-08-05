using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Control;
using Client.Input;
using Core.Master;
using Game.Block.Interface;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// 電線ツールで設置する電柱の種類と向きの選択状態。スクロールでサイクル、回転キーで向き変更する
    /// Selected pole type and direction for the wire tool; scroll cycles the type and the rotate key turns it
    /// </summary>
    public class ElectricWirePoleSelection
    {
        // スクロール1ノッチの閾値（ホットバーと同じスケールで読む）
        // Scroll threshold per notch, read at the hot bar's scale
        private const float ScrollThreshold = 0.5f;

        private IReadOnlyList<BlockId> _unlockedPoles;
        private int _selectedIndex;

        public BlockDirection CurrentDirection { get; private set; } = BlockDirection.North;
        public BlockId SelectedBlockId => _unlockedPoles[_selectedIndex];
        public bool HasSelectablePole => 0 < _unlockedPoles.Count;

        public ElectricWirePoleSelection(IReadOnlyList<BlockId> unlockedPoles)
        {
            _unlockedPoles = unlockedPoles;
        }

        /// <summary>
        /// 解放済み電柱リストを更新する。選択中の種が残っていれば選択を維持する
        /// Refresh the unlocked pole list, keeping the current selection when it survives
        /// </summary>
        public void RefreshUnlockedPoles(IGameUnlockStateData unlockState)
        {
            var previousSelected = HasSelectablePole ? SelectedBlockId : (BlockId?)null;
            _unlockedPoles = ListUnlockedPoles(unlockState);
            var index = previousSelected.HasValue ? IndexOf(previousSelected.Value) : 0;
            _selectedIndex = index < 0 ? 0 : index;
        }

        /// <summary>
        /// スクロールで種をサイクルし、回転キーで向きを変更する
        /// Cycle the type by scroll and rotate the direction by the rotate key
        /// </summary>
        public void UpdateInput()
        {
            var scroll = ReadScroll();
            if (ScrollThreshold < scroll) CycleNext();
            else if (scroll < -ScrollThreshold) CyclePrevious();

            // 通常設置と同じ回転キー（+Shiftで垂直回転）を適用する
            // Apply the same rotate key as normal placement (vertical with Shift)
            if (InputManager.Playable.BlockPlaceRotation.GetKeyDown)
                CurrentDirection = HybridInput.GetKey(KeyCode.LeftShift) ? CurrentDirection.VerticalRotation() : CurrentDirection.HorizonRotation();
        }

        public void CycleNext()
        {
            if (!HasSelectablePole) return;
            _selectedIndex = (_selectedIndex + 1) % _unlockedPoles.Count;
        }

        public void CyclePrevious()
        {
            if (!HasSelectablePole) return;
            _selectedIndex = (_selectedIndex - 1 + _unlockedPoles.Count) % _unlockedPoles.Count;
        }

        /// <summary>
        /// 選択中の電柱のIdとマスタを取り出す。解放済みが1つも無ければfalse
        /// Get the selected pole's id and master; false when nothing is unlocked
        /// </summary>
        public bool TryGetSelectedPole(out BlockId blockId, out BlockMasterElement blockMaster)
        {
            if (!HasSelectablePole)
            {
                blockId = default;
                blockMaster = null;
                return false;
            }

            blockId = SelectedBlockId;
            blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            return true;
        }

        /// <summary>
        /// 解放済みElectricPoleブロックをSortPriority昇順で列挙する
        /// List unlocked ElectricPole blocks in ascending SortPriority
        /// </summary>
        public static IReadOnlyList<BlockId> ListUnlockedPoles(IGameUnlockStateData unlockState)
        {
            return MasterHolder.BlockMaster.Blocks.Data
                .Where(block => block.BlockType == BlockMasterElement.BlockTypeConst.ElectricPole)
                .Where(block => unlockState.BlockUnlockStateInfos.TryGetValue(block.BlockGuid, out var info) && info.IsUnlocked)
                .OrderBy(block => block.SortPriority ?? 0)
                .ThenBy(block => block.BlockGuid)
                .Select(block => MasterHolder.BlockMaster.GetBlockId(block.BlockGuid))
                .ToList();
        }

        private int IndexOf(BlockId blockId)
        {
            for (var i = 0; i < _unlockedPoles.Count; i++)
                if (_unlockedPoles[i] == blockId) return i;
            return -1;
        }

        private static float ReadScroll()
        {
            if (UiPointerHitTest.IsPointerOverAnyUi()) return 0f;

            // InputSystemスクロールを読み、無ければlegacyへフォールバック（BlueprintCopySystemと同一）
            // Read Input System scroll with a legacy fallback, identical to BlueprintCopySystem
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 100f : UnityEngine.Input.mouseScrollDelta.y;
        }
    }
}
