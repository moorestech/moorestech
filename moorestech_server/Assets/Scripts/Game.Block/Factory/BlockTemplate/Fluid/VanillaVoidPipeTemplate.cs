using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate.Fluid
{
    /// <summary>
    ///     ボイドパイプのテンプレート。可変状態が無いので New と Load は同じ組み立てになる（ADR 0056）
    ///     Template for the void pipe; with no mutable state, New and Load assemble the same block (ADR 0056)
    /// </summary>
    public class VanillaVoidPipeTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return GetBlock(blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private static BlockSystem GetBlock(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var voidPipeParam = (blockMasterElement.BlockParam as VoidPipeBlockParam)!;

            // 受け入れコネクタとボイド本体のみ組立
            // Assemble only the inflow connector and the void body
            var connectorComponent = IFluidInventory.CreateFluidInventoryConnector(voidPipeParam.FluidInventoryConnectors, blockPositionInfo);
            var voidPipeComponent = new VoidPipeComponent();
            var components = new List<IBlockComponent>
            {
                voidPipeComponent,
                connectorComponent,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
