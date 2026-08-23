// シナリオ: 木チュートリアル(チャレンジ#3)まで進め、k-d tree索引の最寄り探索が
//           (a)総当たり最寄りと一致 (b)伐採後に次の木へ移る ことを実走検証する
// Scenario: advance to the tree challenge (#3) and verify the k-d tree nearest search
//           (a) matches a brute-force nearest and (b) moves to the next tree after felling.
// 足場生成やSetupDebugEnvironmentは呼ばない（自然なマップ=木mapObjectとスポーンを残すため）
// Do NOT flatten ground or SetupDebugEnvironment (keep the natural map: tree mapObjects & spawn)
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
var treePinTutorial = "a0e8917b-83d2-5cf6-84da-f45ea20fb298"; // チャレンジ#3のmapObjectPin tutorialGuid
var stoneToolAttackSpeedSeconds = 2.1f; // 石器のattackSpeed=2。サーバーのクールダウン許容率を越える間隔で打つ
var fellingMaxHits = 14; // 木hp100 / 石器damage10。破壊までの上限打撃数

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("tutorial-tree-pin-nearest-search", options, async p =>
{
    p.Note("チュートリアル序盤を進めて木ピン(チャレンジ#3)まで到達する");
    var challengeStore = p.ServerService<Game.Challenge.ChallengeDatastore>();
    var pinStore = Client.Game.InGame.Tutorial.WorldPinStateStore.Instance;

    var c1Current = await PollUntil(() => challengeStore.CurrentChallengeInfo.CurrentChallenges
        .Any(c => c.ChallengeMasterElement.ChallengeGuid == challenge1), 30);
    p.Assert(c1Current, "チャレンジ#1(小石を3個拾う)がカレントに存在する");

    // スキット表示中はピンが仕様上非表示のため、Web UIと同じSkipインテントで飛ばす
    // Pins are hidden while a skit is playing by design, so skip it via the same intent path as the web UI
    p.Note("Skipインテントで開幕スキットを飛ばす");
    var skitStore = Client.Skit.UI.SkitPresentationStateStore.Instance;
    var skipAccepted = await PollUntil(() =>
    {
        var s = skitStore.GetCurrent();
        return skitStore.TrySkip(s.SessionId, s.SceneRevision).Ok;
    }, 30);
    p.Assert(skipAccepted, "Skipインテントが受理された");

    p.Note("小石3個と石器クラフトでチャレンジ#1・#2を消化する");
    p.GiveItemDirect("小石", 3);
    var c1Done = await PollUntil(() => challengeStore.CurrentChallengeInfo.CompletedChallenges
        .Any(c => c.ChallengeGuid == challenge1), 30);
    p.Assert(c1Done, "チャレンジ#1(小石を3個拾う)が完了した");

    Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.Craft(stoneToolRecipe);
    var c3Unlocked = await PollUntil(() => challengeStore.CurrentChallengeInfo.CurrentChallenges
        .Any(c => c.ChallengeMasterElement.ChallengeGuid == challenge3), 60);
    p.Assert(c3Unlocked, "チャレンジ#3(伐採)が解放された");

    // 検証1: 木ピンが伐採のtutorialGuidで表示される
    // Verify 1: the tree pin appears with the felling tutorialGuid
    var treePinShown = await PollUntil(() => pinStore.GetCurrent().Pins
        .Any(x => x.PinId == "map-object-pin" && x.TutorialGuid == treePinTutorial), 30);
    p.Assert(treePinShown, "木ピン(map-object-pin)が伐採のtutorialGuidで表示された");
    await p.Screenshot("01-tree-pin");

    // 検証2: k-d tree索引の結果が、シーン内の全木を総当たりした最寄りと一致する
    // Verify 2: the k-d tree result equals a brute-force nearest over every tree in the scene
    p.Note("k-d tree索引の最寄り木を総当たり結果と突き合わせる");
    var datastore = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.Map.MapObject.MapObjectGameObjectDatastore>();
    var playerPosition = p.PlayerPosition;
    var indexed = datastore.SearchNearestMapObject(treeMapObject, playerPosition);
    var brute = BruteForceNearest(playerPosition);
    p.Assert(indexed != null, "索引が最寄りの木を返した");
    p.Assert(brute != null, "総当たりで最寄りの木が見つかった");
    if (indexed != null && brute != null)
    {
        var indexedDistance = (indexed.Position - playerPosition).sqrMagnitude;
        var bruteDistance = (brute.Position - playerPosition).sqrMagnitude;
        p.Note($"索引={indexed.InstanceId} d2={indexedDistance:F3} / 総当たり={brute.InstanceId} d2={bruteDistance:F3} / 木の総数={CountAvailableTrees()}");
        p.Assert(Mathf.Abs(indexedDistance - bruteDistance) < 0.001f, "索引の最寄り距離が総当たりと一致する");
    }

    // 検証3: ピンの実座標が最寄りの木に重なっている
    // Verify 3: the pin transform sits on the nearest tree
    var pinComponent = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.Tutorial.MapObjectPin>(FindObjectsInactive.Include);
    p.Assert(pinComponent != null, "MapObjectPinがシーンに存在する");
    var firstPinPosition = pinComponent.transform.position;
    if (indexed != null) p.Assert((firstPinPosition - indexed.Position).sqrMagnitude < 0.001f, "ピンが最寄りの木の位置を指している");

    // 検証4: その木を伐採すると、索引とピンが次の木へ移る
    // Verify 4: felling that tree moves both the index result and the pin to the next tree
    p.Note("石器を装備して最寄りの木を伐採しきる");
    await p.EquipItem("石器", 0);
    var felledInstanceId = indexed.InstanceId;
    for (var hit = 0; hit < fellingMaxHits && indexed.IsAvailable; hit++)
    {
        Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.AttackMapObject(felledInstanceId);
        await p.WaitSeconds(stoneToolAttackSpeedSeconds);
    }
    p.Assert(!indexed.IsAvailable, "伐採した木が破壊済みになった");
    await p.Screenshot("02-tree-felled");

    p.Note("伐採後の最寄り探索が次の木へ移ることを確認する");
    var afterPosition = p.PlayerPosition;
    var afterIndexed = await PollUntilResult(() =>
    {
        var candidate = datastore.SearchNearestMapObject(treeMapObject, afterPosition);
        return candidate != null && candidate.InstanceId != felledInstanceId ? candidate : null;
    }, 30);
    p.Assert(afterIndexed != null, "伐採後の索引が伐採済みでない別の木を返した");
    if (afterIndexed != null)
    {
        var afterBrute = BruteForceNearest(afterPosition);
        p.Note($"伐採後: 索引={afterIndexed.InstanceId} / 総当たり={afterBrute?.InstanceId}");
        p.Assert(afterBrute != null && Mathf.Abs((afterIndexed.Position - afterPosition).sqrMagnitude - (afterBrute.Position - afterPosition).sqrMagnitude) < 0.001f,
            "伐採後も索引の最寄り距離が総当たりと一致する");

        var pinMoved = await PollUntil(() => (pinComponent.transform.position - afterIndexed.Position).sqrMagnitude < 0.001f, 15);
        p.Assert(pinMoved, "ピンが次の木へ移った");
    }
    await p.Screenshot("03-pin-moved-to-next-tree");

    p.Note("検証完了");

    #region Internal

    // シーン内の生存している木を総当たりして最寄りを出す。索引から独立した検算用
    // Brute-force the nearest live tree in the scene as an oracle independent of the index
    Client.Game.InGame.Map.MapObject.MapObjectGameObject BruteForceNearest(Vector3 from)
    {
        Client.Game.InGame.Map.MapObject.MapObjectGameObject best = null;
        var bestDistance = float.MaxValue;
        foreach (var mapObject in UnityEngine.Object.FindObjectsByType<Client.Game.InGame.Map.MapObject.MapObjectGameObject>(FindObjectsSortMode.None))
        {
            if (mapObject.MapObjectGuid != treeMapObject || !mapObject.IsAvailable) continue;
            var distance = (mapObject.Position - from).sqrMagnitude;
            if (bestDistance <= distance) continue;
            bestDistance = distance;
            best = mapObject;
        }
        return best;
    }

    int CountAvailableTrees()
    {
        return UnityEngine.Object.FindObjectsByType<Client.Game.InGame.Map.MapObject.MapObjectGameObject>(FindObjectsSortMode.None)
            .Count(x => x.MapObjectGuid == treeMapObject && x.IsAvailable);
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

    async UniTask<Client.Game.InGame.Map.MapObject.MapObjectGameObject> PollUntilResult(
        Func<Client.Game.InGame.Map.MapObject.MapObjectGameObject> resolve, int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            var result = resolve();
            if (result != null) return result;
            await p.WaitSeconds(1f);
        }
        return resolve();
    }

    #endregion
});
