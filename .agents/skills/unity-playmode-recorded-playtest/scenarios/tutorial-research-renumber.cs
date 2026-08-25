// シナリオ: ADR 0033 のチェーン（研究3→風力掘削機設置(原木鉱脈ピン)→研究4→粘土入手(粘土鉱脈ピン)）を実走検証する
// Scenario: verify the ADR 0033 chain (research 3 → wind drill (log-vein pin) → research 4 → clay (clay-vein pin)) live
// 足場生成やSetupDebugEnvironmentは呼ばない（自然なマップ=鉱脈露頭とスポーンを残すため）
// Do NOT flatten ground or SetupDebugEnvironment (keep the natural map: vein outcrops & spawn)
using System;
using System.Linq;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;

var research1 = new Guid("837e9697-8586-406e-a0f6-16a010050218");
var research2 = new Guid("424be8c1-c40c-4644-8104-06934c59b147");
var research3 = new Guid("07d6226c-ed14-4a6f-aa2a-6fa085fce8ec");
var research4 = new Guid("858bcb10-b8ba-478e-9bc5-473ca61281a2");
var stoneToolRecipe = new Guid("9c20aa73-1877-4e0e-adcc-9f725c9377da");
var stoneAxeRecipe = new Guid("04932724-b122-45ea-8cb1-642d9c834444");
var placeWindDrill = new Guid("a6497c0b-82eb-5280-82c7-d339bc32de14"); // 風力掘削機を設置する
var completeResearch4 = new Guid("7b9ddaf3-2d63-5876-83ed-03602bf44742"); // 原始研究4を完了する
var obtainClay = new Guid("14f3b765-be4d-51ef-983f-685c043c265b"); // 粘土を入手する
var placeFurnace = new Guid("603e84c0-10b1-501f-a03d-598584d34d58"); // 石窯を設置する
var brickRecipe = new Guid("3e0459d2-71b7-419a-84d6-6d33c193c9bd"); // 石窯: 粘土+原木→レンガ
var research5Guid = new Guid("b47c5e3c-1b58-42c5-a477-d485d2eae747"); // 旧4.5
var research6 = new Guid("bc5e7786-6759-4271-8095-836703b54490");
var research7 = new Guid("0d76f2e5-be1c-4ad4-b460-97a8aad0495f");
var research8 = new Guid("48f75a7e-36f3-4845-a0bc-f8de8b3d7baf");
var research9 = new Guid("3bca3b97-14d7-4cc1-a661-2266670bb6cb");
var windDrillPinTutorial = "a62599e4-4a0f-5773-b134-c51038475c19"; // 風力掘削機設置 slot0 veinPin
var clayPinTutorial = "39473729-f5d0-5d7d-b6b9-a6c8940437d5"; // 粘土入手 slot0 veinPin
var logVein = new Guid("56ab3155-1479-49fa-a656-922021e4556a"); // 原木鉱脈
var clayVein = new Guid("18d2bd1f-737d-42d6-8c1e-27fa3a9ce1ca"); // 粘土鉱脈

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("tutorial-research-renumber", options, async p =>
{
    var challengeStore = p.ServerService<Game.Challenge.ChallengeDatastore>();
    var pinStore = Client.Game.InGame.Tutorial.WorldPinStateStore.Instance;
    var api = Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly;

    p.Note("開幕スキットを飛ばし、研究3完了までをサーバー直付与で消化する");
    // スキットは固定world入口では再生されないことがあるため、Skipは受理されなくても続行する
    // The opening skit may not play on the fixed-world entry, so continue even if Skip is never accepted
    var skitStore = Client.Skit.UI.SkitPresentationStateStore.Instance;
    var skipAccepted = await PollUntil(() =>
    {
        var s = skitStore.GetCurrent();
        return skitStore.TrySkip(s.SessionId, s.SceneRevision).Ok;
    }, 10);
    p.Note(skipAccepted ? "開幕スキットをSkipした" : "開幕スキットは再生されていない（Skip不要）");
    p.GiveItemDirect("小石", 3);
    await p.WaitSeconds(1f);
    api.Craft(stoneToolRecipe);
    await p.WaitSeconds(1f);
    await p.EquipItem("石器", 0);
    p.GiveItemDirect("原木", 3);
    p.GiveItemDirect("木の板", 5);
    p.GiveItemDirect("木の棒", 5);
    await p.WaitSeconds(1f);
    p.GiveItemDirect("木の板", 5); p.GiveItemDirect("木の棒", 5);
    api.CompleteResearch(research1);
    await p.WaitSeconds(1f);
    p.GiveItemDirect("石", 5);
    p.GiveItemDirect("砕いた石材", 5);
    await p.WaitSeconds(1f);
    p.GiveItemDirect("木の板", 5); p.GiveItemDirect("木の棒", 5); p.GiveItemDirect("砕いた石材", 5);
    api.CompleteResearch(research2);
    await p.WaitSeconds(1f);
    p.GiveItemDirect("木の棒", 2); p.GiveItemDirect("砕いた石材", 3);
    api.Craft(stoneAxeRecipe);
    await p.WaitSeconds(1f);
    await p.EquipItem("石の斧", 0);
    p.GiveItemDirect("木の板", 10); p.GiveItemDirect("木の棒", 5); p.GiveItemDirect("砕いた石材", 10);
    api.CompleteResearch(research3);

    // 検証1: 研究3の後は「風力掘削機を設置する」がカレントで、ピンは原木鉱脈の露頭を指す
    // Verify 1: after research 3 the wind-drill challenge is current and its pin sits on a log-vein outcrop
    var windDrillCurrent = await PollUntil(() => IsCurrent(placeWindDrill), 30);
    p.Assert(windDrillCurrent, "研究3の後に「風力掘削機を設置する」がカレントになった");
    var windDrillPinShown = await PollUntil(() => pinStore.GetCurrent().Pins
        .Any(x => x.PinId == "vein-pin" && x.TutorialGuid == windDrillPinTutorial), 30);
    p.Assert(windDrillPinShown, "風力掘削機設置のveinPinが表示された");
    var outcrops = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.Map.Outcrop.OutcropGameObjectDatastore>();
    var veinPin = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.Tutorial.VeinPin>(FindObjectsInactive.Include);
    p.Assert(veinPin != null, "VeinPinがシーンに存在する");
    var nearestLog = outcrops != null ? outcrops.SearchNearestOutcrop(logVein, p.PlayerPosition) : null;
    var nearestClay = outcrops != null ? outcrops.SearchNearestOutcrop(clayVein, p.PlayerPosition) : null;
    p.Note($"最寄り原木露頭={(nearestLog != null ? nearestLog.transform.position.ToString() : "なし")} / 最寄り粘土露頭={(nearestClay != null ? nearestClay.transform.position.ToString() : "なし")} / ピン={veinPin?.transform.position}");
    p.Assert(nearestLog != null && veinPin != null && (veinPin.transform.position - nearestLog.transform.position).sqrMagnitude < 0.001f, "ピンが原木鉱脈の露頭を指している");
    p.Assert(nearestClay == null || veinPin == null || (veinPin.transform.position - nearestClay.transform.position).sqrMagnitude > 0.001f, "ピンは粘土露頭ではない");
    await p.Screenshot("01-wind-drill-log-vein-pin");

    // 検証2: 風力掘削機設置の後は研究4がカレント（粘土入手ではない）
    // Verify 2: after placing the drill, research 4 is current (not clay)
    p.Note("風力掘削機を直設置して次の目標が研究4になることを確認する");
    p.PlaceBlockDirect("風力掘削機", Vector3Int.RoundToInt(p.PlayerPosition) + new Vector3Int(3, 0, 3), Game.Block.Interface.BlockDirection.North);
    var r4Current = await PollUntil(() => IsCurrent(completeResearch4), 30);
    p.Assert(r4Current, "風力掘削機設置の後に「原始研究4を完了する」がカレントになった");
    p.Assert(!IsCurrent(obtainClay), "粘土入手はまだカレントでない");

    // 検証3: 研究4完了の後に粘土入手がカレントになり、粘土鉱脈ピンが出る
    // Verify 3: after research 4, clay becomes current with a clay-vein pin
    p.GiveItemDirect("木の板", 20); p.GiveItemDirect("木の棒", 20); p.GiveItemDirect("砕いた石材", 10);
    api.CompleteResearch(research4);
    var clayCurrent = await PollUntil(() => IsCurrent(obtainClay), 30);
    p.Assert(clayCurrent, "研究4の後に「粘土を入手する」がカレントになった");
    var clayPinShown = await PollUntil(() => pinStore.GetCurrent().Pins
        .Any(x => x.PinId == "vein-pin" && x.TutorialGuid == clayPinTutorial), 30);
    p.Assert(clayPinShown, "粘土入手のveinPinが表示された");
    await p.Screenshot("02-clay-vein-pin");

    // 検証4: 研究マスタに 4.5 が無く 5〜9 がある。研究4の機械レシピ解放は1本
    // Verify 4: research master has no 4.5, has 5–9; research 4 unlocks exactly one machine recipe
    var names = Core.Master.MasterHolder.ResearchMaster.ResearchElements.Values.Select(x => x.ResearchNodeName).ToList();
    p.Assert(!names.Contains("原始研究4.5"), "原始研究4.5が存在しない");
    p.Assert(new[] { "原始研究5", "原始研究6", "原始研究7", "原始研究8", "原始研究9" }.All(names.Contains), "原始研究5〜9が存在する");
    var r4 = Core.Master.MasterHolder.ResearchMaster.ResearchElements[research4];
    var r4Recipes = r4.ClearedActions.items.Where(a => a.GameActionType == "unlockMachineRecipe").ToList();
    var r4RecipeGuids = r4Recipes.SelectMany(a => ((Mooresmaster.Model.GameActionModule.UnlockMachineRecipeGameActionParam)a.GameActionParam).UnlockMachineRecipeGuids).ToList();
    p.Assert(r4RecipeGuids.Count == 1 && r4RecipeGuids[0] == brickRecipe, "研究4の機械レシピ解放は 粘土+原木→レンガ の1本だけ");
    var chain = new[] { research4, research5Guid, research6, research7, research8, research9 };
    var chainNames = new[] { "原始研究4", "原始研究5", "原始研究6", "原始研究7", "原始研究8", "原始研究9" };
    var chainOk = Enumerable.Range(0, chain.Length).All(i =>
    {
        var node = Core.Master.MasterHolder.ResearchMaster.ResearchElements[chain[i]];
        var prevOk = i == 0 || (node.PrevResearchNodeGuids.Length == 1 && node.PrevResearchNodeGuids[0] == chain[i - 1]);
        return prevOk && node.ResearchNodeName == chainNames[i];
    });
    p.Assert(chainOk, "研究4→5→6→7→8→9 が単一prevで直列に繋がり、各GUIDの名称が期待どおり");
    var challenges = Core.Master.MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements.SelectMany(c => c.Challenges).ToList();
    var noDrag = new[] { placeWindDrill, placeFurnace }.All(g => challenges.First(c => c.ChallengeGuid == g).Tutorials.All(t => t.TutorialType != "uiDragGuide"));
    p.Assert(noDrag, "風力掘削機設置・石窯設置に uiDragGuide が無い");
    var nearestClayNow = outcrops != null ? outcrops.SearchNearestOutcrop(clayVein, p.PlayerPosition) : null;
    p.Assert(nearestClayNow != null && veinPin != null && (veinPin.transform.position - nearestClayNow.transform.position).sqrMagnitude < 0.001f, "粘土ピンが最寄りの粘土露頭を指している");
    p.Note("検証完了");

    #region Internal

    bool IsCurrent(Guid challengeGuid)
    {
        return challengeStore.CurrentChallengeInfo.CurrentChallenges.Any(c => c.ChallengeMasterElement.ChallengeGuid == challengeGuid);
    }

    async UniTask<bool> PollUntil(Func<bool> condition, int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            if (condition()) return true;
            await p.WaitSeconds(1f);
        }
        return condition();
    }

    #endregion
});
