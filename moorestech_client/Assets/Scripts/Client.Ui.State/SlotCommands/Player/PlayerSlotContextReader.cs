using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;
using Client.Ui.State.SlotCommands.Block;
using Core.Item.Interface;
using Core.Master;
using UnityEngine;

namespace Client.Ui.State.SlotCommands.Player
{
    /// <summary>
    /// 押下の瞬間のブロックの開閉と束縛からプレイヤーの枠の計画の文脈を組む（Web slotActions の ctx）
    /// Builds the player-slot planning context from the block's open state and bindings at gesture time (the Web slotActions ctx)
    /// </summary>
    public sealed class PlayerSlotContextReader
    {
        private readonly IBlockInventoryPanelSource _panel;
        private readonly IBlockInventoryOperations _block;
        private readonly BlockSlotRestrictionInputBuilder _restrictionInput;
        private readonly IItemStackLevelLookup _stackLevels;

        public PlayerSlotContextReader(IBlockInventoryPanelSource panel, IBlockInventoryOperations block, BlockSlotRestrictionInputBuilder restrictionInput, IItemStackLevelLookup stackLevels)
        {
            _panel = panel;
            _block = block;
            _restrictionInput = restrictionInput;
            _stackLevels = stackLevels;
        }

        public PlayerSlotContext Read(PlayerInventorySnapshot inventory, ItemId clickedItem, bool shiftPressed)
        {
            // 列車は束縛なし（Web は source が block のときだけ束縛を引く）。束縛の入力は枠数しか読まないのでパネルの状態のまま
            // Trains are unrestricted (the Web resolves bindings only for block sources); the binding input reads only the slot count, so it stays on the panel state
            return _panel.Current.Value switch
            {
                OpenBlockInventoryPanel block => PlayerSlotContext.WithBlockOpen(inventory, DestinationSlots(block.ItemSlots), BlockSlotRestrictionResolver.Resolve(_restrictionInput.Build(block), clickedItem), _stackLevels),
                OpenTrainInventoryPanel train => PlayerSlotContext.WithBlockOpen(inventory, TrainDestinationSlots(train), BlockSlotRestriction.Unrestricted, _stackLevels),
                _ => PlayerSlotContext.WithBlockClosed(inventory, _stackLevels),
            };

            #region Internal

            // 宛先の枠はブロック側の操作と同じ口から押下の瞬間の生の値で読む（先書き後の古い写しで宛先を選ばない。D-37）
            // Destination slots are read raw at press time from the same port as block-side commands (never pick a target from a stale copy after a pre-write; D-37)
            IReadOnlyList<SlotItem> DestinationSlots(IReadOnlyList<SlotItem> panelSlots)
            {
                // silent-ok: 口が偽になるのはエラー中のパネルだけ。ブロックは束縛の入力が取得失敗の断りを返し通知と Warning ログが出る / 列車は TrainDestinationSlots が Warning ログを出す
                return _block.TryReadBlockSlots(out var slots) ? slots : panelSlots;
            }

            // 列車には取得失敗の断りが無く、エラー中の Shift は状態の枠で計画される。実行器の拒否ログは列車のエラーを言わず、計画器が満杯等で断ると実行器まで届かないので、Shift の押下でだけここで知らせる
            // Trains have no fetch-failure refusal, so an erroring train's Shift plans on state slots; the executor log omits the train error and planner refusals never reach it, so report here on Shift presses only
            IReadOnlyList<SlotItem> TrainDestinationSlots(OpenTrainInventoryPanel train)
            {
                if (shiftPressed && train.Error != BlockInventoryPanelError.None) Debug.LogWarning($"[inventory.shift_move] train car {train.TrainCarInstanceId} is in error ({train.Error}); planning against its last panel slots, block-side moves will be rejected");
                return DestinationSlots(train.ItemSlots);
            }

            #endregion
        }

        // ログに出すブロックの識別（Web の block.identifier）
        // Block identity for logs (the Web block.identifier)
        public string DescribeOpenBlock()
        {
            return _panel.Current.Value switch
            {
                OpenBlockInventoryPanel block => block.Position.ToString(),
                OpenTrainInventoryPanel train => train.TrainCarInstanceId.ToString(),
                _ => "(closed)",
            };
        }
    }
}
