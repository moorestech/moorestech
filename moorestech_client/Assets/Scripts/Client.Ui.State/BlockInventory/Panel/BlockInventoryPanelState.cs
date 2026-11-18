using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.Items;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// ブロックインベントリのパネルの状態（閉・ブロック・列車の union。Web の BlockInventoryData の写し）
    /// Block-inventory panel state: a union of closed, block and train (port of the Web BlockInventoryData)
    /// </summary>
    public abstract class BlockInventoryPanelState
    {
        public static readonly BlockInventoryPanelState Closed = new ClosedBlockInventoryPanel();

        private protected BlockInventoryPanelState()
        {
        }

        // 本文を作り直すかの唯一の判定（Web の key=identifier にエラーの有無を足す）
        // The single rule for rebuilding the body (Web key=identifier plus error presence)
        public abstract bool IsSamePanel(BlockInventoryPanelState other);

        // 開いていてエラーが無い（本文を載せ枠を操作できる）の唯一の判定
        // The single rule for open without error (the body mounts and slots are operable)
        public abstract bool IsOpenWithoutError { get; }
    }

    public sealed class ClosedBlockInventoryPanel : BlockInventoryPanelState
    {
        internal ClosedBlockInventoryPanel()
        {
        }

        public override bool IsSamePanel(BlockInventoryPanelState other)
        {
            return other is ClosedBlockInventoryPanel;
        }

        public override bool IsOpenWithoutError => false;
    }

    /// <summary>
    /// 開いたパネル（ブロック・列車）の共通の値。種類を足しても読み手は型で分けずにここを読む
    /// Values shared by every open panel (block and train); readers use this instead of switching on the concrete type
    /// </summary>
    public abstract class OpenInventoryPanel : BlockInventoryPanelState
    {
        public IReadOnlyList<SlotItem> ItemSlots { get; private set; }
        public BlockInventoryPanelError Error { get; private set; }

        private protected OpenInventoryPanel(IReadOnlyList<SlotItem> itemSlots, BlockInventoryPanelError error)
        {
            ItemSlots = itemSlots;
            Error = error;
        }

        public sealed override bool IsOpenWithoutError => Error == BlockInventoryPanelError.None;
    }

    public sealed class OpenBlockInventoryPanel : OpenInventoryPanel
    {
        public string BlockType { get; private set; }
        public Guid BlockGuid { get; private set; }
        public BlockId BlockId { get; private set; }
        public BlockInstanceId BlockInstanceId { get; private set; }
        public Vector3Int Position { get; private set; }
        public BlockInventoryComponents Components { get; private set; }

        internal OpenBlockInventoryPanel(string blockType, Guid blockGuid, BlockId blockId, BlockInstanceId blockInstanceId, Vector3Int position, IReadOnlyList<SlotItem> itemSlots, BlockInventoryPanelError error) : base(itemSlots, error)
        {
            BlockType = blockType;
            BlockGuid = blockGuid;
            BlockId = blockId;
            BlockInstanceId = blockInstanceId;
            Position = position;
            Components = BlockComponentRegistry.Resolve(blockType);
        }

        public override bool IsSamePanel(BlockInventoryPanelState other)
        {
            return other is OpenBlockInventoryPanel block && Position == block.Position && BlockGuid == block.BlockGuid && BlockInstanceId == block.BlockInstanceId && Error == block.Error;
        }
    }

    public sealed class OpenTrainInventoryPanel : OpenInventoryPanel
    {
        public long TrainCarInstanceId { get; private set; }

        internal OpenTrainInventoryPanel(long trainCarInstanceId, IReadOnlyList<SlotItem> itemSlots, BlockInventoryPanelError error) : base(itemSlots, error)
        {
            TrainCarInstanceId = trainCarInstanceId;
        }

        public override bool IsSamePanel(BlockInventoryPanelState other)
        {
            return other is OpenTrainInventoryPanel train && TrainCarInstanceId == train.TrainCarInstanceId && Error == train.Error;
        }
    }
}
