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
            // 逆順の範囲も同じ箱として扱う
            // Treat a reversed range as the same box
            var boxMin = Vector3Int.Min(min, max);
            var boxMax = Vector3Int.Max(min, max);
            var targets = CollectTargets();
            if (targets.Count == 0)
            {
                blueprint = null;
                return false;
            }

            // 選択余白による位置ずれを避けるため、コピー対象の外形からアンカーを決める
            // Derive the anchor from copied blocks so selection margins cannot shift placement
            var anchor = CalcMinCorner(targets);
            var blocks = new List<BlueprintBlockJsonObject>();
            foreach (var data in targets)
            {
                blocks.Add(CreateBlockJson(data, anchor));
            }

            // 保存したブロック順で内部配線を記録する
            // Record internal lines using the saved block order
            var (wires, chains) = BlueprintLineCollector.Collect(targets.ConvertAll(data => data.Block));
            blueprint = new BlueprintJsonObject(name, blocks, wires, chains, GameRandom.NextGuid());
            return true;

            #region Internal

            List<WorldBlockData> CollectTargets()
            {
                var result = new List<WorldBlockData>();
                var box = BlueprintCopyTargetRule.CreateBox(boxMin, boxMax);
                foreach (var data in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
                {
                    var master = MasterHolder.BlockMaster.GetBlockMaster(data.Block.BlockId);
                    if (!BlueprintCopyTargetRule.IsCopiedByBox(master, data.Block.BlockPositionInfo, box)) continue;
                    result.Add(data);
                }

                return result;
            }

            // 原点の成分最小を基準にする
            // Anchor at the component-wise minimum of block origins
            Vector3Int CalcMinCorner(List<WorldBlockData> copyTargets)
            {
                var minCorner = copyTargets[0].Block.BlockPositionInfo.OriginalPos;
                foreach (var data in copyTargets)
                {
                    minCorner = Vector3Int.Min(minCorner, data.Block.BlockPositionInfo.OriginalPos);
                }

                return minCorner;
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
