using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    /// <summary>
    ///     クリーンルーム境界ブロック（壁・扉・各種ハッチ）の共通テンプレート
    ///     Shared template for clean-room boundary blocks (wall, door, hatches)
    /// </summary>
    public class VanillaCleanRoomBoundaryTemplate : IBlockTemplate
    {
        private readonly CleanRoomBoundaryKind _boundaryKind;

        public VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind boundaryKind)
        {
            _boundaryKind = boundaryKind;
        }

        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Create(blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Create(blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private IBlock Create(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            // 境界マーカーのみを載せる（ハッチ等の搬送挙動は各機能実装側で追加する）
            // Attach only the boundary marker; hatch transfer behaviors are added separately
            var components = new List<IBlockComponent>
            {
                new CleanRoomBoundaryComponent(_boundaryKind),
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
