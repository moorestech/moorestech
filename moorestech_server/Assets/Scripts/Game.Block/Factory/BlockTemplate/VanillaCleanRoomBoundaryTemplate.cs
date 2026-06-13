using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    // 4種のクリーンルーム境界ブロック共通テンプレート。kind 別に密閉マーカー＋I/O挙動を合成
    // Shared template for the 4 boundary block types; composes the sealing marker + I/O behavior per kind
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
            return Build(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement,
            BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Build(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        // componentStates が null なら New、非nullなら Load。kind で合成内容を分岐する
        // null componentStates → New, non-null → Load; switch composition by kind
        private IBlock Build(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement,
            BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            // 全 kind 共通で密閉マーカーを付ける（flood-fill 検出が境界として見る）
            // Every kind carries the sealing marker (flood-fill detection sees it as a boundary)
            var components = new List<IBlockComponent>
            {
                new CleanRoomBoundaryComponent(_kind),
            };

            // kind 別に I/O 挙動コンポーネント（＋コネクタ）を合成する
            // Compose the I/O behavior component (+ connector) per kind
            switch (_kind)
            {
                case CleanRoomBoundaryKind.ItemHatch:
                    AddItemHatch();
                    break;
                case CleanRoomBoundaryKind.PipeHatch:
                    AddPipeHatch();
                    break;
                case CleanRoomBoundaryKind.DoorHatch:
                    components.Add(new CleanRoomDoorHatchComponent());
                    break;
                case CleanRoomBoundaryKind.Wall:
                default:
                    break; // 壁はマーカーのみ
            }

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);

            #region Internal

            void AddItemHatch()
            {
                var param = (CleanRoomItemHatchBlockParam)blockMasterElement.BlockParam;
                var connector = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
                var hatch = componentStates == null
                    ? new CleanRoomItemHatchComponent(blockInstanceId, connector)
                    : new CleanRoomItemHatchComponent(componentStates, blockInstanceId, connector);
                components.Add(connector);
                components.Add(hatch);
            }

            void AddPipeHatch()
            {
                var param = (CleanRoomPipeHatchBlockParam)blockMasterElement.BlockParam;
                var connector = IFluidInventory.CreateFluidInventoryConnector(param.FluidInventoryConnectors, blockPositionInfo);
                const float capacity = 100f; // FluidPipe(本番mod) と同値
                var pipe = componentStates == null
                    ? new CleanRoomPipeHatchComponent(capacity, connector)
                    : new CleanRoomPipeHatchComponent(componentStates, capacity, connector);
                components.Add(connector);
                components.Add(pipe);
            }

            #endregion
        }
    }
}
