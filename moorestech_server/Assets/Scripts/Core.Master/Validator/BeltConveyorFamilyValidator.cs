using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator
{
    /// <summary>
    /// ベルトコンベアファミリーの構成（代表・長尺・斜面）がコード側の解決規則を満たすか検証する
    /// Validates that belt conveyor families satisfy the code-side resolution rules (representative/length/slope)
    /// </summary>
    public static class BeltConveyorFamilyValidator
    {
        public static string Validate(Blocks blocks)
        {
            // ファミリー名でベルトブロックを束ねてから、ファミリー単位で構成を検証する
            // Group belt blocks by family name, then validate each family's composition
            var logs = "";
            var membersByFamilyName = new Dictionary<string, List<BlockMasterElement>>();
            foreach (var block in blocks.Data)
            {
                if (block.BlockParam is not IBeltConveyorFamilyParam beltParam) continue;

                var familyName = beltParam.BeltConveyorFamily;
                if (string.IsNullOrEmpty(familyName))
                {
                    logs += $"[BlockMaster] BeltConveyor block {block.Name} has empty beltConveyorFamily\n";
                    continue;
                }

                if (!membersByFamilyName.TryGetValue(familyName, out var members))
                {
                    members = new List<BlockMasterElement>();
                    membersByFamilyName[familyName] = members;
                }

                members.Add(block);
            }

            foreach (var (familyName, members) in membersByFamilyName) logs += ValidateFamily(familyName, members);

            return logs;

            #region Internal

            string ValidateFamily(string familyName, List<BlockMasterElement> members)
            {
                var familyLogs = "";
                var blockType = members[0].BlockType;
                var lengthOneStraightCount = 0;
                var slopeCounts = new Dictionary<string, int>();
                var seenStraightLengths = new HashSet<int>();

                foreach (var member in members)
                {
                    // ベルトは1×1×N（Nは進行方向の長さ）でなければ経路分解できない
                    // Belts must be 1x1xN (N is the length along travel) or the path cannot be decomposed
                    if (member.BlockSize.x != 1 || member.BlockSize.y != 1 || member.BlockSize.z < 1)
                        familyLogs += $"[BlockMaster] BeltConveyor block {member.Name} blockSize must be [1,1,N]\n";

                    // 同一ファミリーで型が混在すると設置システムの振り分けが破綻する
                    // Mixing block types within one family breaks placement-system routing
                    if (member.BlockType != blockType)
                        familyLogs += $"[BlockMaster] BeltConveyorFamily {familyName} mixes blockType {blockType} and {member.BlockType}\n";

                    var slopeType = ((IBeltConveyorFamilyParam)member.BlockParam).SlopeType;
                    if (slopeType == BeltConveyorBlockParam.SlopeTypeConst.Straight)
                    {
                        if (!seenStraightLengths.Add(member.BlockSize.z))
                            familyLogs += $"[BlockMaster] BeltConveyorFamily {familyName} has duplicated straight length:{member.BlockSize.z}\n";
                        if (member.BlockSize.z == 1) lengthOneStraightCount++;
                        continue;
                    }

                    // 斜面は1マスのみ。長尺の斜面は経路分解が扱えない
                    // Slopes must be single-cell; the path decomposition cannot handle long slopes
                    if (member.BlockSize.z != 1)
                        familyLogs += $"[BlockMaster] BeltConveyor slope block {member.Name} blockSize must be [1,1,1]\n";

                    slopeCounts.TryGetValue(slopeType, out var count);
                    slopeCounts[slopeType] = count + 1;
                }

                // 代表（長さ1の直線）はちょうど1件、斜面は各方向1件までに限る
                // Exactly one representative (length-1 straight); at most one block per slope direction
                if (lengthOneStraightCount != 1)
                    familyLogs += $"[BlockMaster] BeltConveyorFamily {familyName} must contain exactly one length-1 straight block\n";
                foreach (var (slopeType, count) in slopeCounts)
                {
                    if (count > 1) familyLogs += $"[BlockMaster] BeltConveyorFamily {familyName} has {count} {slopeType} slope blocks\n";
                }

                return familyLogs;
            }

            #endregion
        }
    }
}
