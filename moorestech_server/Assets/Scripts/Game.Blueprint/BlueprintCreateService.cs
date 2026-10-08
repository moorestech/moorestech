using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Interface.Component;
using Game.Context;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.Blueprint
{
    public static class BlueprintCreateService
    {
        public static bool TryCreateFromArea(string name, Vector3Int min, Vector3Int max, out BlueprintJsonObject blueprint)
        {
            var targets = CollectTargets();
            if (targets.Count == 0)
            {
                blueprint = null;
                return false;
            }

            // 選択余白による位置ずれを避けるため、コピー対象の外形からアンカーを決める
            // Derive the anchor from copied blocks so selection margins cannot shift placement
            var anchor = CalcAnchor(targets);
            var blocks = new List<BlueprintBlockJsonObject>();
            foreach (var data in targets)
            {
                blocks.Add(CreateBlockJson(data, anchor));
            }

            blueprint = new BlueprintJsonObject(name, blocks, GameRandom.NextGuid());
            return true;

            #region Internal

            List<WorldBlockData> CollectTargets()
            {
                var result = new List<WorldBlockData>();
                foreach (var data in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
                {
                    var master = MasterHolder.BlockMaster.GetBlockMaster(data.Block.BlockId);
                    if (!BlueprintCopyTargetRule.IsCopyTarget(master)) continue;
                    if (!BlueprintCopyTargetRule.IntersectsBox(data.Block.BlockPositionInfo, min, max)) continue;
                    result.Add(data);
                }

                return result;
            }

            // 負座標でも中心セルを下方向に丸める
            // Floor the center cell even at negative coordinates
            Vector3Int CalcAnchor(List<WorldBlockData> copyTargets)
            {
                var extentMin = copyTargets[0].Block.BlockPositionInfo.MinPos;
                var extentMax = copyTargets[0].Block.BlockPositionInfo.MaxPos;
                foreach (var data in copyTargets)
                {
                    extentMin = Vector3Int.Min(extentMin, data.Block.BlockPositionInfo.MinPos);
                    extentMax = Vector3Int.Max(extentMax, data.Block.BlockPositionInfo.MaxPos);
                }

                return new Vector3Int(Mathf.FloorToInt((extentMin.x + extentMax.x) / 2f), extentMin.y, Mathf.FloorToInt((extentMin.z + extentMax.z) / 2f));
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
