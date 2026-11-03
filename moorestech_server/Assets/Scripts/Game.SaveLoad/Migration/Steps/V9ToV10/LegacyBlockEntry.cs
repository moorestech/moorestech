using System;
using Core.Master;
using Game.SaveLoad.Migration.Steps.Shared;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps.V9ToV10
{
    /// <summary>
    /// 版9のブロック1件（world要素かブループリント内ブロック）を寝かせるために読んだ値。書き換え先の JObject と、占有の計算に要る最小の隅・向き・サイズを持つ
    /// One version-9 block (a world element or a blueprint block) read for laying flat; holds the JObject to rewrite plus the min corner, heading and size the occupancy needs
    /// </summary>
    internal sealed class LegacyBlockEntry
    {
        private const string BlockGuidKey = "blockGuid";
        private const string DirectionKey = "direction";

        public readonly JObject Block;
        public readonly Vector3Int MinCorner;
        public readonly int Horizontal;
        public readonly bool WasVertical;

        // 縦のクリーンルームパネルは壁パネルへ替わるので、置き換え後のguid
        // Vertical clean-room panels become wall panels, so this is the guid after replacement
        public readonly string TargetBlockGuid;
        public readonly bool ReplacesWithWallPanel;

        private LegacyBlockEntry(JObject block, Vector3Int minCorner, int horizontal, bool wasVertical, string targetBlockGuid, bool replacesWithWallPanel)
        {
            Block = block;
            MinCorner = minCorner;
            Horizontal = horizontal;
            WasVertical = wasVertical;
            TargetBlockGuid = targetBlockGuid;
            ReplacesWithWallPanel = replacesWithWallPanel;
        }

        // 座標キー（world は X/Y/Z、ブループリントは offsetX/Y/Z）を受けて読む。読めなければ理由を返す
        // Read with the given position keys (X/Y/Z for world, offsetX/Y/Z for blueprints); return the reason when unreadable
        public static bool TryRead(JToken blockToken, string xKey, string yKey, string zKey, string label, out LegacyBlockEntry entry, out string failureReason)
        {
            entry = null;
            if (!(blockToken is JObject block)) return MigrationSaveJson.Reject($"{label} がオブジェクトではありません。 type={blockToken.Type}", out failureReason);

            // 向きと座標は占有の計算に必須
            // Direction and position are required to compute the occupancy
            var directionToken = block[DirectionKey];
            if (!LegacyTwelveDirection.TryRead(directionToken, out var horizontal, out var wasVertical) || !TryReadInt(block[xKey], out var x) || !TryReadInt(block[yKey], out var y) || !TryReadInt(block[zKey], out var z))
            {
                return MigrationSaveJson.Reject($"{label} の向き・座標が読めません。 direction={directionToken} {xKey}={block[xKey]} {yKey}={block[yKey]} {zKey}={block[zKey]}", out failureReason);
            }

            var guidText = block[BlockGuidKey]?.Type == JTokenType.String ? block[BlockGuidKey].Value<string>() : null;
            if (!Guid.TryParse(guidText, out var blockGuid)) return MigrationSaveJson.Reject($"{label} の blockGuid が読めません。 blockGuid={block[BlockGuidKey]}", out failureReason);

            // サイズは置き換え後のブロックで引く
            // Look up the size of the block that will actually stand there
            var replacesWithWallPanel = wasVertical && blockGuid == Guid.Parse(LegacyTwelveDirection.CleanRoomPanelGuid);
            var targetBlockGuid = replacesWithWallPanel ? LegacyTwelveDirection.CleanRoomWallPanelGuid : guidText;
            entry = new LegacyBlockEntry(block, new Vector3Int(x, y, z), horizontal, wasVertical, targetBlockGuid, replacesWithWallPanel);
            failureReason = null;
            return true;
        }

        // 向きと guid を書き換える（占有の判定とは独立）
        // Rewrite the direction and guid (independent of the occupancy decision)
        public void Rewrite()
        {
            Block[DirectionKey] = Horizontal;
            Block[BlockGuidKey] = TargetBlockGuid;
        }

        // 置き換え後のブロックのサイズ。マスタに無い blockGuid は null を返し、占有に数えない（後段の MissingMasterPruner が除去する）
        // Size of the block after replacement; a blockGuid absent from the master yields null and is left out of occupancy (the later MissingMasterPruner removes it)
        public Vector3Int? LookUpMasterBlockSize()
        {
            var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(Guid.Parse(TargetBlockGuid));
            if (blockId == null) return null;
            return MasterHolder.BlockMaster.GetBlockMaster(blockId.Value).BlockSize;
        }

        private static bool TryReadInt(JToken token, out int value)
        {
            value = 0;
            if (token == null || token.Type != JTokenType.Integer) return false;
            var raw = token.Value<long>();
            if (raw < int.MinValue || raw > int.MaxValue) return false;
            value = (int)raw;
            return true;
        }
    }
}
