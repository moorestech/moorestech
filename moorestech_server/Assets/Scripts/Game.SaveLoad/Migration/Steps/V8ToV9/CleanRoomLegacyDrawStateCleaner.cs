using System;
using System.Collections.Generic;
using System.Linq;
using Game.SaveLoad.Migration.Steps.V7ToV8;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V8ToV9
{
    /// <summary>
    /// クリーンルーム機械の旧抽選記録（ウェハグレード・品質Tierの無いダイシング）と、継承をやめたレシピの継承レベルを null にする（冪等）
    /// Null out clean-room machines' legacy draw records (wafer grade, dicing without a quality tier) and the inherited levels of recipes that stopped inheriting (idempotent)
    /// </summary>
    public sealed class CleanRoomLegacyDrawStateCleaner : IV8ToV9MigrationPart
    {
        // クリーンルーム機械ProcessorのSaveKey(typeof FullName)。ステップは現行の型を参照しないので文字列で持つ
        // The clean-room processor's SaveKey (typeof FullName); the step never references live types, so it is kept as a string
        private const string CleanRoomMachineSaveKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";
        private const string PendingRealizationKey = "pendingRealization";
        private const string DrawRecordKey = "drawRecord";
        private const string RecipeGuidKey = "recipeGuid";
        private const string InheritedLevelKey = "inheritedLevel";

        private readonly List<JObject> _drawRecordOwners = new();
        private readonly List<JObject> _inheritedLevelOwners = new();

        public bool TryPrepare(JObject save, out string failureReason)
        {
            // world→ブロック→state の走査と拒否文は版7→8の部品と共有する
            // The world->block->state walk and its refusal texts are shared with the version 7-to-8 parts
            if (!V7ToV8SaveJson.TryCollectBlockStates(save, out var states, out failureReason)) return false;

            foreach (var state in states)
            {
                // クリーンルーム機械以外と、processor が null の機械は対象外
                // Blocks other than clean-room machines and machines with a null processor are out of scope
                var processorToken = state[CleanRoomMachineSaveKey];
                if (V7ToV8SaveJson.IsAbsentOrNull(processorToken)) continue;
                if (!(processorToken is JObject processor)) return V7ToV8SaveJson.Reject($"{CleanRoomMachineSaveKey} がオブジェクトではありません。 type={processorToken.Type}", out failureReason);

                // 加工中ジョブの継承レベル
                // The running job's inherited level
                if (!TryCollectDroppedInheritance(processor, CleanRoomMachineSaveKey, out failureReason)) return false;

                // 待機中の確定結果は無いのが正常形（待機していない機械）
                // No waiting realization is the normal shape (a machine that is not waiting)
                var pendingToken = processor[PendingRealizationKey];
                if (V7ToV8SaveJson.IsAbsentOrNull(pendingToken)) continue;
                if (!(pendingToken is JObject pending)) return V7ToV8SaveJson.Reject($"{PendingRealizationKey} がオブジェクトではありません。 type={pendingToken.Type}", out failureReason);
                if (!TryCollectDroppedInheritance(pending, PendingRealizationKey, out failureReason)) return false;
                if (!TryCollectLegacyDrawRecord(pending, out failureReason)) return false;
            }

            return true;
        }

        public string ApplyAndSummarize()
        {
            foreach (var owner in _drawRecordOwners) owner[DrawRecordKey] = JValue.CreateNull();
            foreach (var owner in _inheritedLevelOwners) owner[InheritedLevelKey] = JValue.CreateNull();
            return $"捨てた旧抽選記録={_drawRecordOwners.Count}件 捨てた継承レベル={_inheritedLevelOwners.Count}件";
        }

        // recipeGuid が継承をやめたレシピで、継承レベルが残っていれば対象にする
        // Target the owner when its recipeGuid stopped inheriting and an inherited level remains
        private bool TryCollectDroppedInheritance(JObject owner, string ownerName, out string failureReason)
        {
            failureReason = null;
            var recipeToken = owner[RecipeGuidKey];
            if (V7ToV8SaveJson.IsAbsentOrNull(recipeToken) || V7ToV8SaveJson.IsAbsentOrNull(owner[InheritedLevelKey])) return true;
            if (recipeToken.Type != JTokenType.String) return V7ToV8SaveJson.Reject($"{ownerName}.{RecipeGuidKey} が文字列ではありません。 type={recipeToken.Type}", out failureReason);
            if (!Guid.TryParse((string)recipeToken, out var recipeGuid)) return V7ToV8SaveJson.Reject($"{ownerName}.{RecipeGuidKey} がGUIDではありません。 value={recipeToken}", out failureReason);

            if (WaferGradeRemovalTable.DroppedInheritanceRecipeGuids.Contains(recipeGuid)) _inheritedLevelOwners.Add(owner);
            return true;
        }

        // 版9で読めない抽選記録（ウェハグレード・品質Tierの無いダイシング）を対象にする
        // Target draw records version 9 cannot read (wafer grade, dicing without a quality tier)
        private bool TryCollectLegacyDrawRecord(JObject pending, out string failureReason)
        {
            failureReason = null;
            var drawRecordToken = pending[DrawRecordKey];
            if (V7ToV8SaveJson.IsAbsentOrNull(drawRecordToken)) return true;
            if (!(drawRecordToken is JObject drawRecord)) return V7ToV8SaveJson.Reject($"{DrawRecordKey} がオブジェクトではありません。 type={drawRecordToken.Type}", out failureReason);
            var kindToken = drawRecord["kind"];
            if (kindToken == null || kindToken.Type != JTokenType.String) return V7ToV8SaveJson.Reject($"{DrawRecordKey}.kind が文字列ではありません。 type={kindToken?.Type}", out failureReason);

            var kind = (string)kindToken;
            var isWaferGrade = kind == "waferGrade";
            var isDicingWithoutTier = kind == "dicing" && V7ToV8SaveJson.IsAbsentOrNull(drawRecord["qualityTier"]);
            if (isWaferGrade || isDicingWithoutTier) _drawRecordOwners.Add(pending);
            return true;
        }
    }
}
