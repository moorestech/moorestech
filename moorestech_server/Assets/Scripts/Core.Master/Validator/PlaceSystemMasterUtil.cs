using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.PlaceSystemModule;

namespace Core.Master.Validator
{
    public static class PlaceSystemMasterUtil
    {
        public static bool Validate(PlaceSystem placeSystem, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += PlaceItemValidation();
            errorLogs += BeltConveyorParamValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            // 接続ツール3モードのビルドメニュー用フィールド（name/icon/設置ブロック）を検証
            // Validate build-menu fields (name/icon/place block) for the three connect tool modes
            string PlaceItemValidation()
            {
                var logs = "";
                foreach (var element in placeSystem.Data)
                {
                    // BeltConveyor等の非接続モードはビルドメニューに出ないためスキップ
                    // Skip non-connect modes (e.g. BeltConveyor) since they do not appear in the build menu
                    if (!IsConnectMode(element.PlaceMode)) continue;

                    if (string.IsNullOrEmpty(element.Name))
                        logs += $"[PlaceSystemMaster] PlaceMode:{element.PlaceMode} has empty name\n";

                    if (element.IconItemGuid == null || MasterHolder.ItemMaster.GetItemIdOrNull(element.IconItemGuid.Value) == null)
                        logs += $"[PlaceSystemMaster] PlaceMode:{element.PlaceMode} has invalid iconItemGuid:{element.IconItemGuid}\n";

                    if (element.PlaceBlockGuid != null && MasterHolder.BlockMaster.GetBlockIdOrNull(element.PlaceBlockGuid.Value) == null)
                        logs += $"[PlaceSystemMaster] PlaceMode:{element.PlaceMode} has invalid placeBlockGuid:{element.PlaceBlockGuid}\n";
                }

                return logs;

                bool IsConnectMode(string placeMode) =>
                    placeMode is PlaceSystemMasterElement.PlaceModeConst.TrainRailConnect
                        or PlaceSystemMasterElement.PlaceModeConst.GearChainPoleConnect
                        or PlaceSystemMasterElement.PlaceModeConst.ElectricWireConnect;
            }

            // BeltConveyorモードのブロック参照と長尺構成を検証
            // Validate block references and length composition of BeltConveyor mode entries
            string BeltConveyorParamValidation()
            {
                var logs = "";
                foreach (var element in placeSystem.Data)
                {
                    if (element.PlaceParam is not BeltConveyorPlaceParam param) continue;

                    logs += ValidateBlockGuidExists(param.UpBlockGuid, "upBlockGuid");
                    logs += ValidateBlockGuidExists(param.DownBlockGuid, "downBlockGuid");

                    var lengthOneCount = 0;
                    var seenLengths = new HashSet<int>();
                    foreach (var straightBlock in param.StraightBlocks)
                    {
                        logs += ValidateBlockGuidExists(straightBlock.BlockGuid, "straightBlocks");
                        var block = MasterHolder.BlockMaster.Blocks.Data.FirstOrDefault(b => b.BlockGuid == straightBlock.BlockGuid);
                        if (block == null) continue;

                        // lengthは1以上・重複禁止・length==1がちょうど1件
                        // Length must be >=1, unique, and exactly one length-1 entry must exist
                        if (straightBlock.Length < 1)
                            logs += $"[PlaceSystemMaster] BeltConveyor straight block {block.Name} has invalid length:{straightBlock.Length}\n";
                        if (!seenLengths.Add(straightBlock.Length))
                            logs += $"[PlaceSystemMaster] BeltConveyor duplicated length:{straightBlock.Length} block:{block.Name}\n";
                        if (straightBlock.Length == 1) lengthOneCount++;

                        // マスターのlengthとblockSizeの食い違いはデータ不整合として検出
                        // Mismatch between master length and blockSize is reported as a data error
                        if (block.BlockSize.x != 1 || block.BlockSize.y != 1 || block.BlockSize.z != straightBlock.Length)
                            logs += $"[PlaceSystemMaster] BeltConveyor straight block {block.Name} blockSize must be [1,1,{straightBlock.Length}]\n";
                    }

                    if (lengthOneCount != 1)
                        logs += "[PlaceSystemMaster] BeltConveyor entry must contain exactly one length-1 straight block\n";
                }

                return logs;

                // BlockGuidがBlockMasterに存在するかを検証する
                // Validate that the given BlockGuid exists in BlockMaster
                string ValidateBlockGuidExists(Guid blockGuid, string fieldName)
                {
                    var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(blockGuid);
                    return blockId == null ? $"[PlaceSystemMaster] BeltConveyor has invalid {fieldName}:{blockGuid}\n" : "";
                }
            }

            #endregion
        }

        public static void Initialize(PlaceSystem placeSystem)
        {
            // PlaceSystemMasterは追加の初期化処理がないため、空実装
            // PlaceSystemMaster has no additional initialization, so empty implementation
        }
    }
}
