using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using UnityEngine;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Game.Blueprint
{
    public static class BlueprintCreateService
    {
        // レール系はブロック外ドメイン（RailSegments）を持つためコピー対象外
        // Rail-family blocks are excluded; their graph lives outside block states
        private static readonly HashSet<string> ExcludedBlockTypes = new()
        {
            BlockTypeConst.TrainRail,
            BlockTypeConst.TrainStation,
            BlockTypeConst.TrainItemPlatform,
            BlockTypeConst.TrainFluidPlatform,
        };

        public static bool TryCreateFromArea(string name, Vector3Int min, Vector3Int max, out BlueprintJsonObject blueprint)
        {
            var targets = CollectTargets();
            if (targets.Count == 0)
            {
                blueprint = null;
                return false;
            }

            // アンカー = ボックスXZ中心セル・ボックス最下段Y（負座標でも1セルずれないようfloorで丸める）
            // Anchor: XZ center cell and bottom Y of the box; floor so negative coordinates never shift a cell
            var anchor = new Vector3Int(Mathf.FloorToInt((min.x + max.x) / 2f), min.y, Mathf.FloorToInt((min.z + max.z) / 2f));
            var blocks = new List<BlueprintBlockJsonObject>();
            foreach (var data in targets)
            {
                blocks.Add(CreateBlockJson(data, anchor));
            }

            blueprint = new BlueprintJsonObject(name, blocks);
            return true;

            #region Internal

            List<WorldBlockData> CollectTargets()
            {
                var result = new List<WorldBlockData>();
                foreach (var data in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
                {
                    var master = MasterHolder.BlockMaster.GetBlockMaster(data.Block.BlockId);
                    if (ExcludedBlockTypes.Contains(master.BlockType)) continue;
                    if (!IntersectsBox(data)) continue;
                    result.Add(data);
                }

                return result;
            }

            // 占有セルの一部がボックス内なら対象
            // Included when any occupied cell intersects the XYZ bounding box
            bool IntersectsBox(WorldBlockData data)
            {
                foreach (var pos in data.Block.BlockPositionInfo.EnumeratePositions())
                {
                    if (min.x <= pos.x && pos.x <= max.x &&
                        min.y <= pos.y && pos.y <= max.y &&
                        min.z <= pos.z && pos.z <= max.z) return true;
                }

                return false;
            }

            BlueprintBlockJsonObject CreateBlockJson(WorldBlockData data, Vector3Int anchorPos)
            {
                var master = MasterHolder.BlockMaster.GetBlockMaster(data.Block.BlockId);
                var offset = data.Block.BlockPositionInfo.OriginalPos - anchorPos;
                var direction = (int)data.Block.BlockPositionInfo.BlockDirection;

                // 設定持ちコンポーネントからJSON収集
                // Collect settings JSON from settings-providing components
                var settings = new Dictionary<string, string>();
                foreach (var component in data.Block.ComponentManager.GetComponents<IBlockBlueprintSettings>())
                {
                    settings[component.BlueprintSettingsKey] = component.GetBlueprintSettingsJson();
                }

                return new BlueprintBlockJsonObject(offset, master.BlockGuid.ToString(), direction, settings);
            }

            #endregion
        }
    }
}
