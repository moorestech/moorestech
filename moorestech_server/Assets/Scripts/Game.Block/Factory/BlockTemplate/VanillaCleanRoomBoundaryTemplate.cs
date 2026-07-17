using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Service;
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
        // パイプハッチは鉄のパイプ相当の容量で固定構成にする
        // The pipe hatch uses a fixed capacity matching the iron pipe
        private const float PipeHatchFluidCapacity = 100f;

        private readonly CleanRoomBoundaryKind _boundaryKind;

        public VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind boundaryKind)
        {
            _boundaryKind = boundaryKind;
        }

        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Create(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Create(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private IBlock Create(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            // 全種別共通の境界マーカーに、ハッチ種別のみ搬送コンポーネントを追加する
            // All kinds share the boundary marker; hatch kinds add transfer components
            var components = new List<IBlockComponent>
            {
                new CleanRoomBoundaryComponent(_boundaryKind),
            };

            switch (_boundaryKind)
            {
                case CleanRoomBoundaryKind.ItemHatch:
                    AddItemHatchComponents();
                    break;
                case CleanRoomBoundaryKind.PipeHatch:
                    AddPipeHatchComponents();
                    break;
            }

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);

            #region Internal

            void AddItemHatchComponents()
            {
                // チェストと同じ接続先ローテーション挿入で毎tick押し出す
                // Push out each tick via the same rotating target inserter as the chest
                var param = (CleanRoomItemHatchBlockParam)blockMasterElement.BlockParam;
                var connectorComponent = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
                var inserter = new ConnectingInventoryListPriorityInsertItemService(blockInstanceId, connectorComponent);
                var hatchComponent = componentStates == null
                    ? new CleanRoomItemHatchComponent(inserter)
                    : new CleanRoomItemHatchComponent(componentStates, inserter);

                components.Add(hatchComponent);
                components.Add(connectorComponent);
            }

            void AddPipeHatchComponents()
            {
                // 流体パイプ実装をそのまま載せ「流体が通る境界」を構成する
                // Mount the fluid pipe implementation as-is to form a fluid-passing boundary
                var param = (CleanRoomPipeHatchBlockParam)blockMasterElement.BlockParam;
                var connectorComponent = IFluidInventory.CreateFluidInventoryConnector(param.FluidInventoryConnectors, blockPositionInfo);
                var pipeComponent = new FluidPipeComponent(blockPositionInfo, connectorComponent, PipeHatchFluidCapacity, componentStates);

                components.Add(pipeComponent);
                components.Add(connectorComponent);
                components.Add(new FluidPipeSaveComponent(pipeComponent));
            }

            #endregion
        }
    }
}
