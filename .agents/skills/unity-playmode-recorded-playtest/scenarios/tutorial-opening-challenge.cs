// シナリオ: v8チュートリアル序盤。開始状態(チャレンジ#1/開幕スキット/木ピン)を観測し、
//           伐採でチャレンジ#1完了→#2(木の板)→#3(木の棒)の解放までを実走検証する
// Scenario: v8 tutorial opening. Observe start state (challenge#1 / opening skit / tree pin),
//           then fell a tree to complete #1 and unlock #2 (planks) and #3 (sticks), end-to-end.
// ADR 0038の序盤圧縮で石器ライン(小石を3個拾う/石器を作る・装備する)は削除されたため、
// 旧「小石→石器」ステップは等価物が無く削除した。初期装備の石の斧でそのまま伐採へ入る
// The stone-tool line was deleted by the ADR 0038 early-game compression, so the old pebble steps
// have no equivalent and were removed; the run now starts felling with the initial stone axe.
// 足場生成やSetupDebugEnvironmentは呼ばない（自然なマップ=木mapObjectとスポーンを残すため）
// Do NOT flatten ground or SetupDebugEnvironment (keep the natural map: tree mapObjects & spawn)
using System;
using System.Linq;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using Core.Master;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

var challenge1 = new Guid("fb529cac-5358-57fa-bd0a-08f3a6bb43c4"); // 木を伐採して原木を入手する
var challenge2 = new Guid("90a98c1f-2eda-5e7a-8fee-099c40f639e0"); // 木の板を3枚作る
var challenge3 = new Guid("31bcd3f5-14cb-5091-8bb5-2e5a00e30fe4"); // 木の棒を3本作る
var plankRecipe = new Guid("37623c6d-e1cf-4985-abfb-8239e6e33981"); // 木の板クラフトレシピ(原木x1)
var treePinTutorial = "719845cb-0bdc-5703-b430-759640382fe4"; // チャレンジ#1のmapObjectPin tutorialGuid
var axeAttackSpeedSeconds = 1.1f; // 石の斧のattackSpeed=1。サーバーのクールダウン許容率を越える間隔で打つ
var axeMaxHits = 25; // 1打あたりの原木は乱数のため、原木6個に届くまでの上限打撃数
var requiredLogs = 6; // 伐採チャレンジの3個＋木の板3枚のクラフト分3個

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("tutorial-opening-challenge", options, async p =>
{
    p.Note("v8チュートリアル序盤: 自然なゲーム開始状態を観測する");
    var challengeStore = p.ServerService<Game.Challenge.ChallengeDatastore>();

    // 検証1: チャレンジ#1「木を伐採して原木を入手する」がサーバーのカレントに存在する
    // Verify 1: challenge #1 is the server's current challenge
    var c1Current = await PollUntil(() => challengeStore.CurrentChallengeInfo.CurrentChallenges
        .Any(c => c.ChallengeMasterElement.ChallengeGuid == challenge1), 30);
    p.Assert(c1Current, "チャレンジ#1(木を伐採して原木を入手する)がカレントに存在する");

    // 検証2: 開幕スキット(100_start_game=blocking)がWeb HUDに表示される
    // Verify 2: opening skit renders in the Web HUD
    p.Note("開幕スキット(blocking-skit)の表示を待つ");
    var skitShown = await PollUntilAsync(async () =>
        (await Client.Playtest.WebUi.PlaytestDomQuery.Query("blocking-skit", 1f)).Found, 30);
    p.Assert(skitShown, "開幕スキット(blocking-skit)がWeb HUDに表示された");
    await p.Screenshot("01-skit-and-challenge");

    // スキット表示中はピンが仕様上非表示のため、共通のSkip経路で飛ばす
    // Pins are hidden while a skit is playing by design, so skip it through the shared skip route
    await p.SkipOpeningSkit();

    // 検証3: 木ピン(mapObjectPin=WorldPin "map-object-pin")が登録・表示される
    // Verify 3: the tree mapObject pin is registered and shown as a WorldPin
    p.Note("木ピン(map-object-pin)の表示を待つ");
    var pinStore = Client.Game.InGame.Tutorial.WorldPinStateStore.Instance;
    var pinShown = await PollUntil(() => pinStore.GetCurrent().Pins
        .Any(x => x.PinId == "map-object-pin" && x.TutorialGuid == treePinTutorial), 30);
    p.Assert(pinShown, "木ピン(map-object-pin)が伐採のtutorialGuidで表示された");
    var pinOverlay = await PollUntilAsync(async () =>
        (await Client.Playtest.WebUi.PlaytestDomQuery.Query("world-pin-overlay", 1f)).Found, 15);
    p.Assert(pinOverlay, "world-pin-overlayがWeb HUDに表示された");
    await p.Screenshot("02-world-pin");

    // 検証4: 実際に木を伐採して原木を得る（サーバー側VanillaStaticMapObjectの解決検証）
    // Verify 4: actually fell a tree for logs (validates server-side map object resolution)
    // 狙い先はマスタのピン指定から解決する。earnItem指定のため木種を台本にベタ書きしない
    // The target set comes from the master's pin param; earnItem targeting keeps tree species out of the scenario
    var pinParam = (MapObjectPinTutorialParam)MasterHolder.ChallengeMaster.GetChallenge(challenge1)
        .Tutorials.First(t => t.TutorialParam is MapObjectPinTutorialParam).TutorialParam;
    var pinTargets = MasterHolder.ChallengeMaster.ResolvePinTargets(pinParam);
    p.Assert(0 < pinTargets.Count, "ピン指定から原木を落とすmapObjectが1件以上解決された");

    p.Note("最寄りの木をAttackMapObjectで伐採して原木を得る");
    var mapObjectDatastore = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.Map.MapObject.MapObjectGameObjectDatastore>();
    p.Assert(mapObjectDatastore.SearchNearestMapObject(pinTargets, p.PlayerPosition) != null, "最寄りの未破壊の木がクライアントで見つかった");

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

    var c1Done = await PollUntil(() => challengeStore.CurrentChallengeInfo.CompletedChallenges
        .Any(c => c.ChallengeGuid == challenge1), 30);
    p.Assert(3 <= p.CountItem("原木"), "原木が3個以上インベントリにある");
    p.Assert(c1Done, "チャレンジ#1(伐採)が完了した");

    var c2Unlocked = await PollUntil(() => challengeStore.CurrentChallengeInfo.CurrentChallenges
        .Any(c => c.ChallengeMasterElement.ChallengeGuid == challenge2), 30);
    p.Assert(c2Unlocked, "チャレンジ#2(木の板を3枚作る)が解放された");
    await p.Screenshot("03-challenge2-unlocked");

    // 検証5: 木の板3枚クラフト→#2完了→#3(木の棒)解放
    // Verify 5: craft 3 planks -> #2 done -> #3 (sticks) unlocked
    p.Note("木の板を3枚クラフトしてチャレンジ#2(inInventoryItem)を完了させる");
    for (var craft = 0; craft < 3; craft++)
    {
        Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.Craft(plankRecipe);
        await p.WaitSeconds(1f);
    }

    var c2Done = await PollUntil(() => challengeStore.CurrentChallengeInfo.CompletedChallenges
        .Any(c => c.ChallengeGuid == challenge2), 30);
    p.Assert(3 <= p.CountItem("木の板"), "木の板が3枚以上インベントリにある");
    p.Assert(c2Done, "チャレンジ#2(木の板を3枚作る)が完了した");

    var c3Unlocked = await PollUntil(() => challengeStore.CurrentChallengeInfo.CurrentChallenges
        .Any(c => c.ChallengeMasterElement.ChallengeGuid == challenge3), 30);
    p.Assert(c3Unlocked, "チャレンジ#3(木の棒を3本作る)が解放された");
    await p.Screenshot("04-challenge3-unlocked");

    p.Note("検証完了");

    #region Internal

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
