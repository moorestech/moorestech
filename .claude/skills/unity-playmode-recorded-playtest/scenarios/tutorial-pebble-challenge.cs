// シナリオ: v8チュートリアル序盤。開始状態(チャレンジ#1/開幕スキット/小石ピン)を観測し、
//           小石3個入手でチャレンジ#1完了→#2解放までを実走検証する
// Scenario: v8 tutorial opening. Observe start state (challenge#1 / opening skit / pebble pin),
//           then give 3 pebbles to complete challenge#1 and unlock #2, end-to-end.
// 足場生成やSetupDebugEnvironmentは呼ばない（自然なマップ=小石mapObjectとスポーンを残すため）
// Do NOT flatten ground or SetupDebugEnvironment (keep the natural map: pebble mapObjects & spawn)
using System;
using System.Linq;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;

var challenge1 = new Guid("bd5262ed-fbd4-51e0-a75d-2944f366e10a"); // 小石を3個拾う
var challenge2 = new Guid("7bafc2cf-d55c-5141-805f-99e0b78a9945"); // 石器を作る
var challenge3 = new Guid("fb529cac-5358-57fa-bd0a-08f3a6bb43c4"); // 木を伐採して原木を入手する
var stoneToolRecipe = new Guid("9c20aa73-1877-4e0e-adcc-9f725c9377da"); // 石器クラフトレシピ(小石x3)
var treeMapObject = new Guid("6a53fef8-2cf5-41fe-9922-21fd7dd4ab6c"); // mapObject「木」

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("tutorial-pebble-challenge", options, async p =>
{
    p.Note("v8チュートリアル序盤: 自然なゲーム開始状態を観測する");
    var challengeStore = p.ServerService<Game.Challenge.ChallengeDatastore>();

    // 検証2a: チャレンジ#1「小石を3個拾う」がサーバーのカレントに存在する
    // Verify 2a: challenge #1 is the server's current challenge
    var c1Current = await PollUntil(() => challengeStore.CurrentChallengeInfo.CurrentChallenges
        .Any(c => c.ChallengeMasterElement.ChallengeGuid == challenge1), 30);
    p.Assert(c1Current, "チャレンジ#1(小石を3個拾う)がカレントに存在する");

    // 検証2b: 開幕スキット(100_start_game=blocking)がWeb HUDに表示される
    // Verify 2b: opening skit renders in the Web HUD
    p.Note("開幕スキット(blocking-skit)の表示を待つ");
    var skitShown = await PollUntilAsync(async () =>
        (await Client.Playtest.WebUi.PlaytestDomQuery.Query("blocking-skit", 1f)).Found, 30);
    p.Assert(skitShown, "開幕スキット(blocking-skit)がWeb HUDに表示された");
    await p.Screenshot("01-skit-and-challenge");

    // スキット表示中はピンが仕様上非表示のため、Web UIと同じSkipインテントで飛ばす
    // Pins are hidden while a skit is playing by design, so skip it via the same intent path as the web UI
    p.Note("Skipインテントで開幕スキットを飛ばす");
    var skitStore = Client.Skit.UI.SkitPresentationStateStore.Instance;
    var skipAccepted = await PollUntil(() =>
    {
        var s = skitStore.GetCurrent();
        return skitStore.TrySkip(s.SessionId, s.SceneRevision).Ok;
    }, 15);
    p.Assert(skipAccepted, "Skipインテントが受理された");
    var skitGone = await PollUntilAsync(async () =>
        !(await Client.Playtest.WebUi.PlaytestDomQuery.Query("blocking-skit", 1f)).Found, 30);
    p.Assert(skitGone, "開幕スキットが終了した");

    // 検証3: 小石ピン(mapObjectPin=WorldPin "map-object-pin")が登録・表示される
    // Verify 3: pebble mapObject pin is registered and shown as a WorldPin
    p.Note("小石ピン(map-object-pin)の表示を待つ");
    var pinStore = Client.Game.InGame.Tutorial.WorldPinStateStore.Instance;
    var pinShown = await PollUntil(() => pinStore.GetCurrent().Pins.Any(x => x.PinId == "map-object-pin"), 30);
    p.Assert(pinShown, "小石ピン(map-object-pin)がWorldPinStateStoreに登録された");
    if (pinShown)
    {
        var pin = pinStore.GetCurrent().Pins.First(x => x.PinId == "map-object-pin");
        p.Note($"pin text='{pin.Text}' onScreen={pin.OnScreen}");
    }
    var pinOverlay = await PollUntilAsync(async () =>
        (await Client.Playtest.WebUi.PlaytestDomQuery.Query("world-pin-overlay", 1f)).Found, 15);
    p.Assert(pinOverlay, "world-pin-overlayがWeb HUDに表示された");
    await p.Screenshot("02-world-pin");

    // 検証4: 小石3個入手 → チャレンジ#1完了 → #2「石器を作る」解放
    // Verify 4: acquire 3 pebbles -> complete challenge #1 -> unlock #2
    p.Note("小石を3個付与してチャレンジ#1(inInventoryItem)を完了させる");
    p.GiveItemDirect("小石", 3);
    await p.WaitSeconds(0.5f);
    p.Assert(3 <= p.CountItem("小石"), "小石が3個以上インベントリにある");

    var c1Done = await PollUntil(() => challengeStore.CurrentChallengeInfo.CompletedChallenges
        .Any(c => c.ChallengeGuid == challenge1), 30);
    p.Assert(c1Done, "チャレンジ#1(小石を3個拾う)が完了した");

    var c2Unlocked = await PollUntil(() => challengeStore.CurrentChallengeInfo.CurrentChallenges
        .Any(c => c.ChallengeMasterElement.ChallengeGuid == challenge2), 30);
    p.Assert(c2Unlocked, "チャレンジ#2(石器を作る)が解放された");
    await p.Screenshot("03-challenge2-unlocked");

    // 検証5: 石器クラフト→#2完了→#3(伐採)解放。木ピンでmapObject「木」の実行時解決を確認
    // Verify 5: craft stone tool -> #2 done -> #3 unlocked; tree pin proves mapObject 木 resolves at runtime
    p.Note("石器をクラフトしてチャレンジ#2(createItem)を完了させる");
    Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.Craft(stoneToolRecipe);
    var c2Done = await PollUntil(() => challengeStore.CurrentChallengeInfo.CompletedChallenges
        .Any(c => c.ChallengeGuid == challenge2), 30);
    p.Assert(c2Done, "チャレンジ#2(石器を作る)が完了した");
    var c3Unlocked = await PollUntil(() => challengeStore.CurrentChallengeInfo.CurrentChallenges
        .Any(c => c.ChallengeMasterElement.ChallengeGuid == challenge3), 30);
    p.Assert(c3Unlocked, "チャレンジ#3(伐採)が解放された");

    p.Note("木ピンの表示を待つ(mapObject「木」の解決検証)");
    var treePinShown = await PollUntil(() => pinStore.GetCurrent().Pins.Any(x => x.PinId == "map-object-pin"), 30);
    p.Assert(treePinShown, "木ピン(map-object-pin)が表示された");
    await p.Screenshot("04-tree-pin");

    // 検証6: 実際に木を攻撃して原木ドロップ→#3完了（サーバー側VanillaStaticMapObjectの解決検証）
    // Verify 6: attack a real tree for log drops -> #3 done (validates server-side map object resolution)
    p.Note("最寄りの木をAttackMapObjectで伐採して原木を得る");
    var mapObjectDatastore = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.Map.MapObject.MapObjectGameObjectDatastore>();
    var nearestTree = mapObjectDatastore.SearchNearestMapObject(treeMapObject, p.PlayerPosition);
    p.Assert(nearestTree != null, "最寄りの未破壊の木がクライアントで見つかった");
    if (nearestTree != null)
    {
        Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.AttackMapObject(nearestTree.InstanceId, 100);
        var c3Done = await PollUntil(() => challengeStore.CurrentChallengeInfo.CompletedChallenges
            .Any(c => c.ChallengeGuid == challenge3), 30);
        p.Assert(3 <= p.CountItem("原木"), "原木が3個以上インベントリにある");
        p.Assert(c3Done, "チャレンジ#3(伐採)が完了した");
        await p.Screenshot("05-tree-felled");
    }

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
