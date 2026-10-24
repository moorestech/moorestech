using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V8ToV9
{
    /// <summary>
    /// 未実装の仮ブロック3種の保存済み解放を未解放へ戻す（冪等）
    /// Revert the saved unlocks of the three unimplemented placeholder blocks to locked (idempotent)
    /// </summary>
    /// <remarks>
    /// 3種は空Prefab・機能なしのため研究解放から外した。研究を外すだけでは保存済みの解放がロードで復元され建築候補へ戻る
    /// （裁定 .decisions/2026-10-25-未実装の仮ブロック3種は研究解放を外し保存済みの解放も戻す.md）。
    /// The three have empty prefabs and no function, so they were removed from research unlocks. Removing the research alone
    /// would let saved unlocks restore on load and return them to the build candidates.
    /// </remarks>
    public static class UnimplementedBlockUnlockRevoker
    {
        // 宇宙エレベーター拠点・ロケット発射台・核融合炉。ステップはマスタを引かないのでGUIDを直に持つ
        // Space-elevator base, rocket launch pad and fusion reactor; the step never reads the master, so GUIDs are held directly
        public static readonly IReadOnlyList<Guid> RevokedBlockGuids = new[]
        {
            Guid.Parse("01a162aa-cec0-738c-93c5-f58adc87fa7f"),
            Guid.Parse("01a162b0-7769-7419-af24-bb35528b93d2"),
            Guid.Parse("01a162f1-ab04-76e2-94cf-dc713edeb63b"),
        };

        // GameUnlockStateJsonObject・BlockUnlockStateInfoJsonObject のJSONキー
        // JSON keys of GameUnlockStateJsonObject and BlockUnlockStateInfoJsonObject
        private const string GameUnlockStateKey = "gameUnlockState";
        private const string BlockUnlockStateInfosKey = "blockUnlockStateInfos";
        private const string GuidKey = "guid";
        private const string IsUnlockedKey = "isUnlocked";

        // 書き換える前に辿れる形かを検査し、対象3種の解放記録を集める
        // Check the shape is walkable before rewriting and collect the unlock records of the three targets
        public static bool TryCollectTargets(JObject save, List<JObject> targets, out string failureReason)
        {
            failureReason = null;

            // 解放状態や解放記録の無いセーブは戻すものが無い
            // A save without unlock state or records has nothing to revert
            var unlockStateToken = save[GameUnlockStateKey];
            if (unlockStateToken == null || unlockStateToken.Type == JTokenType.Null) return true;
            if (!(unlockStateToken is JObject unlockState)) return Reject($"{GameUnlockStateKey} がオブジェクトではありません。 type={unlockStateToken.Type}", out failureReason);
            var infosToken = unlockState[BlockUnlockStateInfosKey];
            if (infosToken == null || infosToken.Type == JTokenType.Null) return true;
            if (!(infosToken is JArray infos)) return Reject($"{BlockUnlockStateInfosKey} が配列ではありません。 type={infosToken.Type}", out failureReason);

            foreach (var infoToken in infos)
            {
                if (!(infoToken is JObject info)) return Reject($"{BlockUnlockStateInfosKey} の要素がオブジェクトではありません。 type={infoToken.Type}", out failureReason);

                // GUIDが読めない記録は対象3種ではないので触らない
                // Records whose GUID cannot be read are not among the three targets and are left alone
                if (info[GuidKey] is JValue { Type: JTokenType.String } guidValue && Guid.TryParse((string)guidValue, out var blockGuid) && IsRevokedBlock(blockGuid)) targets.Add(info);
            }

            return true;
        }

        // 対象の解放を未解放にし、解放済みから戻した件数を返す（再実行しても変わらない）
        // Set the targets to locked and return how many were reverted from unlocked (idempotent on rerun)
        public static int Revoke(IReadOnlyList<JObject> targets)
        {
            var revokedCount = 0;
            foreach (var info in targets)
            {
                if (info[IsUnlockedKey] is JValue { Type: JTokenType.Boolean } unlocked && !(bool)unlocked) continue;
                info[IsUnlockedKey] = false;
                revokedCount++;
            }

            return revokedCount;
        }

        private static bool IsRevokedBlock(Guid blockGuid)
        {
            foreach (var revokedGuid in RevokedBlockGuids)
            {
                if (revokedGuid == blockGuid) return true;
            }

            return false;
        }

        private static bool Reject(string reason, out string failureReason)
        {
            failureReason = reason;
            return false;
        }
    }
}
