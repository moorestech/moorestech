using System;
using Core.Master;
using Game.SaveLoad.Migration.Steps.Shared;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps.V9ToV10
{
    /// <summary>
    /// 版9ブロック1件の読取値（書換先JObject・隅・向き・サイズ）
    /// One version-9 block read: target JObject, corner, heading, size
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
        private readonly string _targetBlockGuid;
        public readonly bool ReplacesWithWallPanel;

        private LegacyBlockEntry(JObject block, Vector3Int minCorner, int horizontal, bool wasVertical, string targetBlockGuid, bool replacesWithWallPanel)
        {
            Block = block;
            MinCorner = minCorner;
            Horizontal = horizontal;
            WasVertical = wasVertical;
            _targetBlockGuid = targetBlockGuid;
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
            var replacesWithWallPanel = wasVertical && blockGuid == Guid.Parse(CleanRoomPanelReplacementTable.CleanRoomPanelGuid);
            var targetBlockGuid = replacesWithWallPanel ? CleanRoomPanelReplacementTable.CleanRoomWallPanelGuid : guidText;
            entry = new LegacyBlockEntry(block, new Vector3Int(x, y, z), horizontal, wasVertical, targetBlockGuid, replacesWithWallPanel);
            failureReason = null;
            return true;

            #region Internal

            bool TryReadInt(JToken token, out int value)
            {
                value = 0;
                if (token == null || token.Type != JTokenType.Integer) return false;
                var raw = token.Value<long>();
                if (raw < int.MinValue || int.MaxValue < raw) return false;
                value = (int)raw;
                return true;
            }

            #endregion
        }

        // 向きと guid を書き換える（占有の判定とは独立）
        // Rewrite the direction and guid (independent of the occupancy decision)
        public void Rewrite()
        {
            Block[DirectionKey] = Horizontal;
            Block[BlockGuidKey] = _targetBlockGuid;
        }

        // 置き換え後のブロックのサイズ。マスタに無い blockGuid は null を返し、占有に数えない（後段の MissingMasterPruner が除去する）
        // Size of the block after replacement; a blockGuid absent from the master yields null and is left out of occupancy (the later MissingMasterPruner removes it)
        public Vector3Int? LookUpMasterBlockSize()
        {
            // 床・壁パネルは版10→11で撤去されるので、版9時点の1マスで占有を数える（今のマスタの5マス幅で数えると隣を巻き添えにする）
            // Floor/wall panels are removed in V10->V11, so count them at their version-9 one-cell size (the current 5-wide master size would sweep up neighbors)
            if (CleanRoomPanelReplacementTable.IsPanelGuid(_targetBlockGuid)) return CleanRoomPanelReplacementTable.Version9PanelSize;

            var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(Guid.Parse(_targetBlockGuid));
            if (blockId == null) return null;
            return MasterHolder.BlockMaster.GetBlockMaster(blockId.Value).BlockSize;
        }
    }
}
