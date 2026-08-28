// シナリオ: 開幕チャレンジの誘導提示（木ピンのearnItem指定・キーヒント・クラフトUIハイライト）を実走検証する
// Scenario: end-to-end check of the opening challenge guidance (earnItem tree pin, key hint, craft UI highlights)
// 元はADR 0029の「石器を装備する」チャレンジ検証だったが、ADR 0038の序盤圧縮で石器ラインごと削除された。
// 装備チャレンジとドラッグ矢印の検証は等価物が無いため削除し、クラフトUI誘導（木の板）の検証へ置き換えた
// This was the ADR 0029 equip-challenge check; ADR 0038 deleted the whole stone-tool line, so the equip
// and drag-guide steps have no equivalent and were replaced by the plank craft-UI guidance checks.
// 足場生成やSetupDebugEnvironmentは呼ばない（自然なマップ=木mapObjectとスポーンを残すため）
// Do NOT flatten ground or SetupDebugEnvironment (keep the natural map: tree mapObjects & spawn)
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Playtest;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

var fellTree = new Guid("fb529cac-5358-57fa-bd0a-08f3a6bb43c4"); // 木を伐採して原木を入手する
var craftPlank = new Guid("90a98c1f-2eda-5e7a-8fee-099c40f639e0"); // 木の板を3枚作る
var craftStick = new Guid("31bcd3f5-14cb-5091-8bb5-2e5a00e30fe4"); // 木の棒を3本作る
var plankRecipe = new Guid("37623c6d-e1cf-4985-abfb-8239e6e33981"); // 木の板クラフトレシピ(原木x1)
var treePinTutorial = "719845cb-0bdc-5703-b430-759640382fe4"; // 伐採チャレンジのmapObjectPin(earnItem指定)
var inventoryKeyTutorial = "bc8a72aa-032e-5784-95f7-432004fcbb0f"; // 「インベントリを開く」keyControl
var craftButtonTutorial = "28f3773d-27f0-56e3-9959-781997154f70"; // 「クラフトボタンを長押し」uiHighLight
var plankItemTutorial = "41e0a6e4-2839-5ff8-bf62-b082a9e07021"; // 「木の板を選択」itemViewHighLight
var axeAttackSpeedSeconds = 1.1f; // 石の斧のattackSpeed=1。サーバーのクールダウン許容率を越える間隔で打つ
var axeMaxHits = 25; // 1打あたりの原木は乱数のため、原木6個に届くまでの上限打撃数
var requiredLogs = 6; // 伐採チャレンジの3個＋木の板3枚のクラフト分3個

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("tutorial-craft-guidance-challenge", options, async p =>
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

    // 検証1: 開幕スキットを飛ばすと、木ピンがearnItem指定で解決して表示される
    // Verify 1: after skipping the opening skit, the tree pin resolves via earnItem and shows up
    p.Note("開幕スキットを飛ばして木ピンを待つ");
    await p.SkipOpeningSkit();
    var treePinShown = await PollUntil(() => pinStore.GetCurrent().Pins
        .Any(x => x.PinId == "map-object-pin" && x.TutorialGuid == treePinTutorial), 30);
    p.Assert(treePinShown, "木ピンが伐採チャレンジのtutorialGuidで表示された");
    await p.Screenshot("01-tree-pin");

    // 解決規則はChallengeMasterが唯一の持ち主なので、台本もクライアント実装と同じ入口を通す
    // ChallengeMaster owns the only resolution rule, so the scenario goes through the same entry point as the client
    var pinParam = (MapObjectPinTutorialParam)MasterHolder.ChallengeMaster.GetChallenge(fellTree)
        .Tutorials.First(t => t.TutorialParam is MapObjectPinTutorialParam).TutorialParam;
    var pinTargets = MasterHolder.ChallengeMaster.ResolvePinTargets(pinParam);
    p.Assert(0 < pinTargets.Count, "earnItem解決で原木を落とすmapObjectが1件以上得られた");
    var mapObjectDatastore = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.Map.MapObject.MapObjectGameObjectDatastore>();
    var nearestTree = mapObjectDatastore.SearchNearestMapObject(pinTargets, p.PlayerPosition);
    p.Assert(nearestTree != null, "最寄りの未破壊の木がクライアントで見つかった");

    // 検証2: 初期装備の石の斧で伐採すると伐採チャレンジが完了し、クラフトチャレンジがカレントになる
    // Verify 2: felling with the initial stone axe completes the challenge and makes the craft challenge current
    if (nearestTree != null)
    {
        p.Note("最寄りの木を初期装備の石の斧で伐採する");
        // 原木を落とすmapObjectには草木のような1打で尽きる小物も含まれるため、毎回最寄りを引き直す
        // The log-dropping set includes small plants that die in one hit, so re-pick the nearest target every swing
        for (var hit = 0; hit < axeMaxHits && p.CountItem("原木") < requiredLogs; hit++)
        {
            var target = mapObjectDatastore.SearchNearestMapObject(pinTargets, p.PlayerPosition);
            if (target == null) break;
            Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.AttackMapObject(target.InstanceId);
            await p.WaitSeconds(axeAttackSpeedSeconds);
        }
        p.Note($"伐採後の原木={p.CountItem("原木")}個");
        var fellDone = await PollUntil(() => challengeStore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == fellTree), 30);
        p.Assert(3 <= p.CountItem("原木"), "原木が3個以上インベントリにある");
        p.Assert(fellDone, "チャレンジ「木を伐採して原木を入手する」が完了した");
        await p.Screenshot("02-tree-felled");
    }

    var craftCurrent = await PollUntil(() => challengeStore.CurrentChallengeInfo.CurrentChallenges
        .Any(c => c.ChallengeMasterElement.ChallengeGuid == craftPlank), 30);
    p.Assert(craftCurrent, "チャレンジ「木の板を3枚作る」が現在目標になった");

    // 検証3: クラフト誘導のkeyControlヒントとUIハイライトがサーバー指定どおり提示される
    // Verify 3: the craft guidance publishes the key hint and both UI highlights exactly as the master specifies
    p.Note("キーヒントとクラフトUIハイライトの提示を待つ");
    var keyHintShown = await PollUntil(() => FindElements()
        .OfType<Client.Game.InGame.Tutorial.TutorialKeyControlElementData>()
        .Any(x => x.TutorialGuid == inventoryKeyTutorial && x.KeyName == "Tab"), 30);
    p.Assert(keyHintShown, "keyControlヒント(Tab・インベントリを開く)が提示された");
    var outlines = FindElements().OfType<Client.Game.InGame.Tutorial.TutorialOutlineElementData>().ToList();
    p.Assert(outlines.Any(x => x.LabelTutorialGuid == craftButtonTutorial), "クラフトボタンのuiHighLightが提示された");
    p.Assert(outlines.Any(x => x.LabelTutorialGuid == plankItemTutorial), "木の板のitemViewHighLightが提示された");
    var keyHintDom = await PollUntilAsync(async () => (await Client.Playtest.WebUi.PlaytestDomQuery.Query("key-control-hint", 1f)).Found, 20);
    p.Assert(keyHintDom, "key-control-hintがWeb HUDに描画された");
    // 赤字と枠線の見た目はcomputed styleがブリッジを越えないため、スクリーンショットの目視でのみ確認できる
    // The red text and outline styling cannot cross the bridge as computed style, so screenshots are the only check
    await p.Screenshot("03-key-hint-red-and-highlights");

    // インベントリはTabのトグルで開閉する（ExitToGameScreenはBキー実装のためTab起点では閉じない）
    // The inventory toggles with Tab; ExitToGameScreen taps B and cannot close a Tab-opened inventory
    p.Note("Tabでインベントリを開いてクラフトUIハイライトを撮る");
    await p.PressKey(UnityEngine.InputSystem.Key.Tab);
    await p.WaitSeconds(1f);
    await p.Screenshot("04-inventory-craft-highlight");
    await p.PressKey(UnityEngine.InputSystem.Key.Tab);
    await p.WaitUiState(Client.Game.InGame.UI.UIState.UIStateEnum.GameScreen, 10f);

    // 検証4: 木の板を3枚クラフトするとチャレンジが達成され、木の棒へ進む
    // Verify 4: crafting 3 planks completes the challenge and advances to sticks
    p.Note("木の板を3枚クラフトする");
    for (var craft = 0; craft < 3; craft++)
    {
        Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.Craft(plankRecipe);
        await p.WaitSeconds(1f);
    }
    var craftDone = await PollUntil(() => challengeStore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == craftPlank), 30);
    p.Assert(craftDone, "チャレンジ「木の板を3枚作る」が完了した");
    var stickCurrent = await PollUntil(() => challengeStore.CurrentChallengeInfo.CurrentChallenges
        .Any(c => c.ChallengeMasterElement.ChallengeGuid == craftStick), 30);
    p.Assert(stickCurrent, "チャレンジ「木の棒を3本作る」が現在目標になった");
    await p.Screenshot("05-craft-done");

    // 検証5: 研究画面の説明文がプレースホルダのまま残っていない（上流PR #30の文言が乗っている）
    // Verify 5: the research pane no longer shows the placeholder description (upstream PR #30 text landed)
    p.Note("研究画面の説明文を確認する");
    await p.PressKey(UnityEngine.InputSystem.Key.R);
    await p.WaitSeconds(1.5f);
    var researchPane = await Client.Playtest.WebUi.PlaytestDomQuery.Query("research-detail-pane", 5f);
    p.Assert(researchPane.Found, "research-detail-paneが表示された");
    p.Assert(!researchPane.Text.Contains("New Research Description"), "研究説明文がプレースホルダのままでない");
    await p.Screenshot("06-research-description");
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
