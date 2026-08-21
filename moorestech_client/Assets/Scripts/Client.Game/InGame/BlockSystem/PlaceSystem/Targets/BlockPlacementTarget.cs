using System;
using System.Collections.Generic;
using System.Linq;
using Client.Localization;
using Core.Master;
using Game.Block.Interface;
using Game.PlacementTarget;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class BlockPlacementTarget : IPlacementTarget
    {
        // 設置対象IDは揮発BlockIdではなくマスタのBlockGuid。揮発IDは設置処理が要求する時だけ解決する
        // The placement target id is the master's BlockGuid, not the volatile BlockId; the volatile id is resolved only where placement needs it
        public readonly Guid BlockGuid;

        // スポイト由来の向き（メニュー選択時はnull）
        // Direction picked by the eyedropper (null when selected from the menu)
        public readonly BlockDirection? PickedDirection;

        public BlockId BlockId => MasterHolder.BlockMaster.GetBlockId(BlockGuid);

        public Guid Id => BlockGuid;
        public PlacementTargetKind Kind => PlacementTargetKind.Block;
        public string DisplayName => Localize.GetContent(ContentLocalizationKeys.BlockName(BlockGuid));

        public IReadOnlyList<(Guid itemGuid, int count)> CreateRequiredItems()
        {
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(BlockGuid).RequiredItems;
            if (requiredItems == null) return Array.Empty<(Guid, int)>();
            return requiredItems.Select(item => (item.ItemGuid, item.Count)).ToList();
        }

        public BlockPlacementTarget(Guid blockGuid, BlockDirection? pickedDirection)
        {
            BlockGuid = blockGuid;
            PickedDirection = pickedDirection;
        }

        public bool Equals(IPlacementTarget other)
        {
            return other is BlockPlacementTarget target && BlockGuid == target.BlockGuid && PickedDirection == target.PickedDirection;
        }

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => HashCode.Combine(BlockGuid, PickedDirection);
    }
}
