using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    // 4種のクリーンルーム境界ブロック共通テンプレート。種別付きマーカーを付与。
    // Shared template for the 4 boundary block types; attaches a kinded marker.
    public class VanillaCleanRoomBoundaryTemplate : IBlockTemplate
    {
        private readonly CleanRoomBoundaryKind _kind;

        public VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind kind)
        {
            _kind = kind;
        }

        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId,
            BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Build(blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement,
            BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            // 境界ブロックは保存状態を持たないので New と同じ。
            // Boundary blocks hold no save state, so identical to New.
            return Build(blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private IBlock Build(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId,
            BlockPositionInfo blockPositionInfo)
        {
            // マーカーのみ持つコンポーネントリストを構築してブロックを生成。
            // Build a component list with only the marker, then create the block.
            var components = new List<IBlockComponent>
            {
                new CleanRoomBoundaryComponent(_kind),
            };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
