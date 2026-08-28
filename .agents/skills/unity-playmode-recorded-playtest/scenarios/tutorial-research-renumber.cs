// シナリオ: ADR 0038 改番後のチェーン（風力掘削機設置(石鉱脈ピン)→木の鉱脈へ設置→研究1→粘土入手(粘土鉱脈ピン)）を実走検証する
// Scenario: verify the post-ADR-0038 chain (drill on stone-vein pin → drill on log vein → research 1 → clay with clay-vein pin)
// 旧原始研究1〜3と石器・石の斧ラインは削除されたため、それらを消化する前準備は削除した。
// 改番は表示名のみで、旧4〜9のGUIDがそのまま新1〜6になっている
// The old research 1-3 and the stone-tool line are gone, so their grind preamble was removed;
// the renumbering only changed display names, the old 4-9 GUIDs became the new 1-6.
// 足場生成やSetupDebugEnvironmentは呼ばない（自然なマップ=鉱脈露頭とスポーンを残すため）
// Do NOT flatten ground or SetupDebugEnvironment (keep the natural map: vein outcrops & spawn)
using System;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapVein;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

var research1 = new Guid("858bcb10-b8ba-478e-9bc5-473ca61281a2"); // 旧原始研究4
var research2 = new Guid("b47c5e3c-1b58-42c5-a477-d485d2eae747"); // 旧5
var research3 = new Guid("bc5e7786-6759-4271-8095-836703b54490"); // 旧6
var research4 = new Guid("0d76f2e5-be1c-4ad4-b460-97a8aad0495f"); // 旧7
var research5 = new Guid("48f75a7e-36f3-4845-a0bc-f8de8b3d7baf"); // 旧8
var research6 = new Guid("3bca3b97-14d7-4cc1-a661-2266670bb6cb"); // 旧9
var placeWindDrill = new Guid("a6497c0b-82eb-5280-82c7-d339bc32de14"); // 風力掘削機を設置する
var placeDrillOnLogVein = new Guid("5dc1a5d0-97a2-550c-bfca-bc5046bb3ee6"); // 木の鉱脈に風力掘削機を設置する
var completeResearch1 = new Guid("7b9ddaf3-2d63-5876-83ed-03602bf44742"); // 原始研究1を完了する
var obtainClay = new Guid("14f3b765-be4d-51ef-983f-685c043c265b"); // 粘土を入手する
var placeFurnace = new Guid("603e84c0-10b1-501f-a03d-598584d34d58"); // 石窯を設置する
var brickRecipe = new Guid("3e0459d2-71b7-419a-84d6-6d33c193c9bd"); // 石窯: 粘土+原木→レンガ
var windDrillPinTutorial = "a62599e4-4a0f-5773-b134-c51038475c19"; // 風力掘削機設置 veinPin(石鉱脈)
var logVeinPinTutorial = "9e946232-65a2-5487-a9f0-051f2d44b4f1"; // 木の鉱脈設置 veinPin(原木鉱脈)
var clayPinTutorial = "39473729-f5d0-5d7d-b6b9-a6c8940437d5"; // 粘土入手 veinPin(粘土鉱脈)
var stoneVein = new Guid("735633b7-7aac-4fb8-8b42-022f6bfb9e53"); // 石鉱脈
var logVein = new Guid("56ab3155-1479-49fa-a656-922021e4556a"); // 原木鉱脈
var clayVein = new Guid("18d2bd1f-737d-42d6-8c1e-27fa3a9ce1ca"); // 粘土鉱脈

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("tutorial-research-renumber", options, async p =>
{
    var challengeStore = p.ServerService<Game.Challenge.ChallengeDatastore>();
    var pinStore = Client.Game.InGame.Tutorial.WorldPinStateStore.Instance;
    var api = ClientContext.VanillaApi.SendOnly;

    p.Note("開幕スキットを飛ばし、風力掘削機設置までの素材チャレンジをサーバー直付与で消化する");
    await p.SkipOpeningSkit();

    // 序盤5本はすべてinInventoryItemなので、素材を直に積むだけでカレントが進む
    // The first five challenges are all inInventoryItem, so stacking the items alone advances the current challenge
    p.GiveItemDirect("原木", 3);
    await p.WaitSeconds(1f);
    p.GiveItemDirect("木の板", 3);
    await p.WaitSeconds(1f);
    p.GiveItemDirect("木の棒", 3);
    await p.WaitSeconds(1f);
    p.GiveItemDirect("石", 3);
    await p.WaitSeconds(1f);
    p.GiveItemDirect("砕いた石材", 3);

    // 検証1: 素材チャレンジの後は「風力掘削機を設置する」がカレントで、ピンは石鉱脈の露頭を指す
    // Verify 1: the wind-drill challenge becomes current and its pin sits on a stone-vein outcrop
    var windDrillCurrent = await PollUntil(() => IsCurrent(placeWindDrill), 30);
    p.Assert(windDrillCurrent, "素材チャレンジの後に「風力掘削機を設置する」がカレントになった");
    var windDrillPinShown = await PollUntil(() => pinStore.GetCurrent().Pins
        .Any(x => x.PinId == "vein-pin" && x.TutorialGuid == windDrillPinTutorial), 30);
    p.Assert(windDrillPinShown, "風力掘削機設置のveinPinが表示された");
    var outcrops = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.Map.Outcrop.OutcropGameObjectDatastore>();
    var veinPin = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.Tutorial.VeinPin>(FindObjectsInactive.Include);
    p.Assert(veinPin != null, "VeinPinがシーンに存在する");
    p.Assert(IsPinOn(stoneVein), "ピンが石鉱脈の露頭を指している");
    p.Assert(!IsPinOn(clayVein), "ピンは粘土露頭ではない");
    await p.Screenshot("01-wind-drill-stone-vein-pin");

    // 検証2: 掘削機を1台置くと「木の鉱脈に風力掘削機を設置する」へ進み、ピンが原木鉱脈へ移る
    // Verify 2: placing one drill advances to the log-vein challenge and moves the pin to the log vein
    p.Note("風力掘削機を直設置して次の目標が木の鉱脈設置になることを確認する");
    p.PlaceBlockDirect("風力掘削機", Vector3Int.RoundToInt(p.PlayerPosition) + new Vector3Int(3, 0, 3), Game.Block.Interface.BlockDirection.North);
    var logVeinCurrent = await PollUntil(() => IsCurrent(placeDrillOnLogVein), 30);
    p.Assert(logVeinCurrent, "風力掘削機設置の後に「木の鉱脈に風力掘削機を設置する」がカレントになった");
    var logVeinPinShown = await PollUntil(() => pinStore.GetCurrent().Pins
        .Any(x => x.PinId == "vein-pin" && x.TutorialGuid == logVeinPinTutorial), 30);
    p.Assert(logVeinPinShown, "木の鉱脈設置のveinPinが表示された");
    p.Assert(IsPinOn(logVein), "ピンが原木鉱脈の露頭を指している");
    await p.Screenshot("02-log-vein-pin");

    // 検証3: 原木鉱脈の内側へ置くと研究1がカレントになる（粘土入手はまだ先）
    // Verify 3: placing inside the log vein makes research 1 current (clay is still further down)
    p.Note("原木鉱脈の内側へ風力掘削機を置く");
    var veinRegistry = ClientDIContext.DIContainer.DIContainerResolver.Resolve<MapVeinAabbRegistry>();
    var logVeinAabb = veinRegistry.Veins.Where(vein => vein.VeinGuid == logVein)
        .OrderBy(vein => (vein.Bounds.center - p.PlayerPosition).sqrMagnitude).FirstOrDefault();
    p.Assert(logVeinAabb != null, "ワールドレイアウトに原木鉱脈がある");
    if (logVeinAabb != null)
    {
        var veinCell = Vector3Int.FloorToInt(logVeinAabb.Bounds.center);
        p.WarpPlayer(new Vector3(veinCell.x, logVeinAabb.Bounds.max.y + 3f, veinCell.z - 6f));
        await p.WaitSeconds(1f);
        p.PlaceBlockDirect("風力掘削機", veinCell, Game.Block.Interface.BlockDirection.North);
    }
    var r1Current = await PollUntil(() => IsCurrent(completeResearch1), 30);
    p.Assert(r1Current, "木の鉱脈への設置の後に「原始研究1を完了する」がカレントになった");
    p.Assert(!IsCurrent(obtainClay), "粘土入手はまだカレントでない");

    // 検証4: 研究1完了の後に粘土入手がカレントになり、粘土鉱脈ピンが出る
    // Verify 4: after research 1, clay becomes current with a clay-vein pin
    p.GiveItemDirect("木の板", 3); p.GiveItemDirect("木の棒", 3); p.GiveItemDirect("砕いた石材", 2);
    await p.WaitSeconds(1f);
    api.CompleteResearch(research1);
    var clayCurrent = await PollUntil(() => IsCurrent(obtainClay), 30);
    p.Assert(clayCurrent, "研究1の後に「粘土を入手する」がカレントになった");
    var clayPinShown = await PollUntil(() => pinStore.GetCurrent().Pins
        .Any(x => x.PinId == "vein-pin" && x.TutorialGuid == clayPinTutorial), 30);
    p.Assert(clayPinShown, "粘土入手のveinPinが表示された");
    p.Assert(IsPinOn(clayVein), "粘土ピンが最寄りの粘土露頭を指している");
    await p.Screenshot("03-clay-vein-pin");

    // 検証5: 研究マスタが1〜6へ改番され、7以降と4.5が残っていない。研究1の機械レシピ解放は1本
    // Verify 5: the research master is renumbered to 1-6 with no 7+ or 4.5 left; research 1 unlocks exactly one machine recipe
    var names = Core.Master.MasterHolder.ResearchMaster.ResearchElements.Values.Select(x => x.ResearchNodeName).ToList();
    p.Assert(!names.Contains("原始研究4.5"), "原始研究4.5が存在しない");
    p.Assert(new[] { "原始研究1", "原始研究2", "原始研究3", "原始研究4", "原始研究5", "原始研究6" }.All(names.Contains), "原始研究1〜6が存在する");
    p.Assert(!new[] { "原始研究7", "原始研究8", "原始研究9" }.Any(names.Contains), "原始研究7〜9は改番で消えている");
    var r1 = Core.Master.MasterHolder.ResearchMaster.ResearchElements[research1];
    var r1RecipeGuids = r1.ClearedActions.items.Where(a => a.GameActionType == "unlockMachineRecipe")
        .SelectMany(a => ((Mooresmaster.Model.GameActionModule.UnlockMachineRecipeGameActionParam)a.GameActionParam).UnlockMachineRecipeGuids).ToList();
    p.Assert(r1RecipeGuids.Count == 1 && r1RecipeGuids[0] == brickRecipe, "研究1の機械レシピ解放は 粘土+原木→レンガ の1本だけ");

    // 検証6: 研究1が根で、1→2→3→4→5→6が単一prevで直列に繋がる
    // Verify 6: research 1 is the root and 1→2→3→4→5→6 forms a single-prev chain
    var chain = new[] { research1, research2, research3, research4, research5, research6 };
    var chainNames = new[] { "原始研究1", "原始研究2", "原始研究3", "原始研究4", "原始研究5", "原始研究6" };
    var chainOk = Enumerable.Range(0, chain.Length).All(i =>
    {
        var node = Core.Master.MasterHolder.ResearchMaster.ResearchElements[chain[i]];
        var prevOk = i == 0
            ? node.PrevResearchNodeGuids.Length == 0
            : node.PrevResearchNodeGuids.Length == 1 && node.PrevResearchNodeGuids[0] == chain[i - 1];
        return prevOk && node.ResearchNodeName == chainNames[i];
    });
    p.Assert(chainOk, "研究1(根)→2→3→4→5→6 が単一prevで直列に繋がり、各GUIDの名称が期待どおり");

    var challenges = Core.Master.MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements.SelectMany(c => c.Challenges).ToList();
    var noDrag = new[] { placeWindDrill, placeFurnace }.All(g => challenges.First(c => c.ChallengeGuid == g).Tutorials.All(t => t.TutorialType != "uiDragGuide"));
    p.Assert(noDrag, "風力掘削機設置・石窯設置に uiDragGuide が無い");
    p.Note("検証完了");

    #region Internal

    // ピンの実座標が指定鉱脈の最寄り露頭に重なっているか
    // Whether the pin transform sits on the nearest outcrop of the given vein
    bool IsPinOn(Guid veinGuid)
    {
        if (outcrops == null || veinPin == null) return false;
        var nearest = outcrops.SearchNearestOutcrop(veinGuid, p.PlayerPosition);
        return nearest != null && (veinPin.transform.position - nearest.transform.position).sqrMagnitude < 0.001f;
    }

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
