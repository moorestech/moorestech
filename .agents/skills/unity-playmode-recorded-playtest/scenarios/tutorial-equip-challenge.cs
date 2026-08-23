// シナリオ: ADR 0029「装備チャレンジ新設・木ピンのearnItem指定・キーヒント/ドラッグ矢印」を実走検証する
// Scenario: end-to-end check of ADR 0029 (equip challenge, earnItem tree pin, key hint / drag guide)
// 足場生成やSetupDebugEnvironmentは呼ばない（自然なマップ=小石mapObjectとスポーンを残すため）
// Do NOT flatten ground or SetupDebugEnvironment (keep the natural map: pebble mapObjects & spawn)
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;

var pebbleChallenge = new Guid("bd5262ed-fbd4-51e0-a75d-2944f366e10a"); // 小石を3個拾う
var craftStoneTool = new Guid("7bafc2cf-d55c-5141-805f-99e0b78a9945"); // 石器を作る
var equipStoneTool = new Guid("24f72113-495c-5302-af05-8b1f0d0c1091"); // 石器を装備する(ADR 0029 新設)
var fellTree = new Guid("fb529cac-5358-57fa-bd0a-08f3a6bb43c4"); // 木を伐採して原木を入手する
var stoneToolRecipe = new Guid("9c20aa73-1877-4e0e-adcc-9f725c9377da"); // 石器クラフトレシピ(小石x3)
var treePinTutorial = "719845cb-0bdc-5703-b430-759640382fe4"; // 伐採チャレンジのmapObjectPin(earnItem指定)
var equipKeyTutorial = "e54eeb3b-d3f4-5454-ac6e-7f1f70dc6d51"; // 「インベントリを開いて石器を装備」keyControl
var stoneToolItemGuid = "76174235-48fb-4944-bca7-ad268385d68c"; // 石器
var logItemGuid = "aafce615-6c30-48c4-a29e-3c5b3266748f"; // 原木(木ピンのearnItem指定値)
var stoneToolAttackSpeedSeconds = 2.1f; // 石器のattackSpeed=2。サーバーのクールダウン許容率を越える間隔で打つ
var stoneToolMaxHits = 6; // 1打あたりの原木は1〜4個で乱数のため、3個に届くまでの上限打撃数

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("tutorial-equip-challenge", options, async p =>
{
    // MapObjectPinの解決失敗はLogErrorのみで、PlaytestLogCollectorはSuccessを落とさないため自前で拾う
    // A failed pin resolution only logs an error and never fails the run, so collect it ourselves
    var pinErrors = new List<string>();
    Application.LogCallback onLog = (condition, _, type) =>
    {
        if (type == LogType.Error && condition.Contains("未破壊のMapObject")) pinErrors.Add(condition);
    };
    Application.logMessageReceived += onLog;

    var challengeStore = p.ServerService<Game.Challenge.ChallengeDatastore>();
    var pinStore = Client.Game.InGame.Tutorial.WorldPinStateStore.Instance;
    var tutorialStore = Client.Game.InGame.Tutorial.TutorialPresentationStateStore.Instance;

    // 検証1: 開幕スキットを飛ばし、小石ピンが小石mapObjectに刺さる（従来挙動の非退行）
    // Verify 1: skip the opening skit; the pebble pin still sticks to the pebble map object
    p.Note("開幕スキットを飛ばして小石ピンを待つ");
    await p.SkipOpeningSkit();
    var pebblePinShown = await PollUntil(() => pinStore.GetCurrent().Pins.Any(x => x.PinId == "map-object-pin"), 30);
    p.Assert(pebblePinShown, "小石ピン(map-object-pin)が表示された");
    await p.Screenshot("01-pebble-pin");

    // 検証2: 小石3個→石器クラフトで「石器を装備する」が現在目標になる（ADR 0029 の新チャレンジ）
    // Verify 2: 3 pebbles -> craft -> the new "equip the stone tool" challenge becomes current
    p.Note("小石3個付与→石器クラフトで装備チャレンジまで進める");
    p.GiveItemDirect("小石", 3);
    await PollUntil(() => challengeStore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == pebbleChallenge), 30);
    Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.Craft(stoneToolRecipe);
    var craftDone = await PollUntil(() => challengeStore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == craftStoneTool), 30);
    p.Assert(craftDone, "チャレンジ「石器を作る」が完了した");
    var equipCurrent = await PollUntil(() => challengeStore.CurrentChallengeInfo.CurrentChallenges
        .Any(c => c.ChallengeMasterElement.ChallengeGuid == equipStoneTool), 30);
    p.Assert(equipCurrent, "チャレンジ「石器を装備する」が現在目標になった");

    // 検証3: 装備誘導のkeyControlヒントとドラッグ矢印がサーバー指定どおり提示される
    // Verify 3: the equip guidance publishes the key hint and the drag guide exactly as the master specifies
    p.Note("キーヒントとドラッグ矢印の提示を待つ");
    var keyHintShown = await PollUntil(() => FindElements()
        .OfType<Client.Game.InGame.Tutorial.TutorialKeyControlElementData>()
        .Any(x => x.TutorialGuid == equipKeyTutorial && x.KeyName == "Tab"), 30);
    p.Assert(keyHintShown, "keyControlヒント(Tab・石器を装備)が提示された");
    var dragGuide = FindElements().OfType<Client.Game.InGame.Tutorial.TutorialDragGuideElementData>()
        .FirstOrDefault(x => x.ToAnchorId == "equipment.selected-slot");
    p.Assert(dragGuide != null, "ドラッグ矢印が選択中装備枠を指している");
    if (dragGuide != null) p.Assert(dragGuide.FromAnchorId == $"inventory.item-{stoneToolItemGuid}", "ドラッグ矢印の始点が石器スロットである");
    var keyHintDom = await PollUntilAsync(async () => (await Client.Playtest.WebUi.PlaytestDomQuery.Query("key-control-hint", 1f)).Found, 20);
    p.Assert(keyHintDom, "key-control-hintがWeb HUDに描画された");
    // 赤字と矢印の56px/3200msはcomputed styleがブリッジを越えないため、スクリーンショットの目視でのみ確認できる
    // The red text and the 56px/3200ms arrow cannot cross the bridge as computed style, so screenshots are the only check
    await p.Screenshot("02-key-hint-red-and-drag-guide");

    // インベントリはTabのトグルで開閉する（ExitToGameScreenはBキー実装のためTab起点では閉じない）
    // The inventory toggles with Tab; ExitToGameScreen taps B and cannot close a Tab-opened inventory
    p.Note("Tabでインベントリを開いてドラッグ矢印を撮る");
    await p.PressKey(UnityEngine.InputSystem.Key.Tab);
    await p.WaitSeconds(1f);
    await p.Screenshot("03-inventory-drag-guide");
    await p.PressKey(UnityEngine.InputSystem.Key.Tab);
    await p.WaitUiState(Client.Game.InGame.UI.UIState.UIStateEnum.GameScreen, 10f);

    // 検証4: 石器を選択中装備枠へ装備した瞬間に装備チャレンジが達成され、伐採へ進む
    // Verify 4: equipping the stone tool into the selected slot completes the challenge and advances to felling
    p.Note("石器を選択中装備枠へ装備する");
    await p.EquipItem("石器", 0);
    var equipDone = await PollUntil(() => challengeStore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == equipStoneTool), 30);
    p.Assert(equipDone, "チャレンジ「石器を装備する」が装備で達成された");
    var fellCurrent = await PollUntil(() => challengeStore.CurrentChallengeInfo.CurrentChallenges
        .Any(c => c.ChallengeMasterElement.ChallengeGuid == fellTree), 30);
    p.Assert(fellCurrent, "チャレンジ「木を伐採して原木を入手する」が現在目標になった");
    await p.Screenshot("04-equip-done");

    // 検証5: 木ピンがearnItem指定で解決し、原木を落とす木に刺さる（旧mapObject直指定からの移行）
    // Verify 5: the tree pin resolves via earnItem and sticks to a tree that drops logs (migrated from a direct mapObject id)
    p.Note("木ピン(earnItem=原木)の解決を待つ");
    var treePinShown = await PollUntil(() => pinStore.GetCurrent().Pins.Any(x => x.PinId == "map-object-pin" && x.TutorialGuid == treePinTutorial), 30);
    p.Assert(treePinShown, "木ピンが伐採チャレンジのtutorialGuidで表示された");
    await p.Screenshot("05-tree-pin");

    // 解決規則はChallengeMasterが唯一の持ち主なので、台本もクライアント実装と同じ入口を通す
    // ChallengeMaster owns the only resolution rule, so the scenario goes through the same entry point as the client
    var treePinParam = new Mooresmaster.Model.ChallengesModule.MapObjectPinTutorialParam(
        Mooresmaster.Model.ChallengesModule.MapObjectPinTutorialParam.PinTargetTypeConst.earnItem,
        new Mooresmaster.Model.ChallengesModule.EarnItemPinTargetParam(new Guid(logItemGuid)),
        "pin");
    var pinTargets = Core.Master.MasterHolder.ChallengeMaster.ResolvePinTargets(treePinParam);
    p.Assert(0 < pinTargets.Count, "earnItem解決で原木を落とすmapObjectが1件以上得られた");
    var mapObjectDatastore = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.Map.MapObject.MapObjectGameObjectDatastore>();
    var nearestTree = mapObjectDatastore.SearchNearestMapObject(pinTargets, p.PlayerPosition);
    p.Assert(nearestTree != null, "最寄りの未破壊の木がクライアントで見つかった");

    // 検証6: 実際に伐採して原木3個→伐採チャレンジ完了。ピン解決のLogErrorが1件も出ていないこと
    // Verify 6: actually fell the tree for 3 logs -> challenge done, and no pin-resolution LogError was raised
    if (nearestTree != null)
    {
        p.Note("最寄りの木を石器で伐採する");
        for (var hit = 0; hit < stoneToolMaxHits && p.CountItem("原木") < 3; hit++)
        {
            Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.AttackMapObject(nearestTree.InstanceId);
            await p.WaitSeconds(stoneToolAttackSpeedSeconds);
        }
        var fellDone = await PollUntil(() => challengeStore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == fellTree), 30);
        p.Assert(3 <= p.CountItem("原木"), "原木が3個以上インベントリにある");
        p.Assert(fellDone, "チャレンジ「木を伐採して原木を入手する」が完了した");
        await p.Screenshot("06-tree-felled");
    }

    // 検証7: 研究画面の説明文がプレースホルダのまま残っていない（上流PR #30の文言が乗っている）
    // Verify 7: the research pane no longer shows the placeholder description (upstream PR #30 text landed)
    p.Note("研究画面の説明文を確認する");
    await p.PressKey(UnityEngine.InputSystem.Key.R);
    await p.WaitSeconds(1.5f);
    var researchPane = await Client.Playtest.WebUi.PlaytestDomQuery.Query("research-detail-pane", 5f);
    p.Assert(researchPane.Found, "research-detail-paneが表示された");
    p.Assert(!researchPane.Text.Contains("New Research Description"), "研究説明文がプレースホルダのままでない");
    await p.Screenshot("07-research-description");
    await p.PressKey(UnityEngine.InputSystem.Key.R);
    await p.WaitUiState(Client.Game.InGame.UI.UIState.UIStateEnum.GameScreen, 10f);

    Application.logMessageReceived -= onLog;
    p.Assert(pinErrors.Count == 0, $"「未破壊のMapObject」のLogErrorが出ていない(実測{pinErrors.Count}件)");
    p.Note("検証完了");

    #region Internal

    // 同時currentな全challengeのsessionを平坦化する（提示は challenge 単位のsessionに分かれて載る）
    // Flatten the sessions of all simultaneously-current challenges (presentation is split per challenge session)
    IEnumerable<Client.Game.InGame.Tutorial.TutorialOverlayElementData> FindElements()
    {
        return tutorialStore.GetCurrent().Sessions.SelectMany(s => s.Elements);
    }

    // 条件成立まで1秒間隔でポーリング（Untilと違い例外中断せず、失敗しても後続の検証を続ける）
    // Poll every 1s until the condition holds (unlike Until, never aborts so later checks still run)
    async UniTask<bool> PollUntil(Func<bool> condition, int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            if (condition()) return true;
            await p.WaitSeconds(1f);
        }
        return condition();
    }

    async UniTask<bool> PollUntilAsync(Func<UniTask<bool>> condition, int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            if (await condition()) return true;
            await p.WaitSeconds(1f);
        }
        return await condition();
    }

    #endregion
});
