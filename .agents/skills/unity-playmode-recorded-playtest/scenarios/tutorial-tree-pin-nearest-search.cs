// シナリオ: 木チュートリアル(チャレンジ#1)で、k-d tree索引の最寄り探索が
//           (a)総当たり最寄りと一致 (b)伐採後に次の木へ移る ことを実走検証する
// Scenario: on the tree challenge (#1), verify the k-d tree nearest search
//           (a) matches a brute-force nearest and (b) moves to the next tree after felling.
// ADR 0038の序盤圧縮で伐採は#3から#1へ繰り上がったため、小石拾い・石器クラフトの前準備は削除した
// The compression moved felling from #3 to #1, so the pebble / stone-tool preamble was removed.
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

var challenge1 = new Guid("fb529cac-5358-57fa-bd0a-08f3a6bb43c4"); // 木を伐採して原木を入手する
var treePinTutorial = "719845cb-0bdc-5703-b430-759640382fe4"; // チャレンジ#1のmapObjectPin tutorialGuid
var axeAttackSpeedSeconds = 1.1f; // 石の斧のattackSpeed=1。サーバーのクールダウン許容率を越える間隔で打つ
var fellingMaxHits = 8; // 木hp100 / 石の斧damage25。破壊までの上限打撃数

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("tutorial-tree-pin-nearest-search", options, async p =>
{
    p.Note("開幕の木ピン(チャレンジ#1)を待つ");
    var challengeStore = p.ServerService<Game.Challenge.ChallengeDatastore>();
    var pinStore = Client.Game.InGame.Tutorial.WorldPinStateStore.Instance;

    var c1Current = await PollUntil(() => challengeStore.CurrentChallengeInfo.CurrentChallenges
        .Any(c => c.ChallengeMasterElement.ChallengeGuid == challenge1), 30);
    p.Assert(c1Current, "チャレンジ#1(木を伐採して原木を入手する)がカレントに存在する");

    // スキット表示中はピンが仕様上非表示のため、共通のSkip経路で飛ばす
    // Pins are hidden while a skit is playing by design, so skip it through the shared skip route
    await p.SkipOpeningSkit();

    // 検証1: 木ピンが伐採のtutorialGuidで表示される
    // Verify 1: the tree pin appears with the felling tutorialGuid
    var treePinShown = await PollUntil(() => pinStore.GetCurrent().Pins
        .Any(x => x.PinId == "map-object-pin" && x.TutorialGuid == treePinTutorial), 30);
    p.Assert(treePinShown, "木ピン(map-object-pin)が伐採のtutorialGuidで表示された");
    await p.Screenshot("01-tree-pin");

    // 狙い先はマスタのピン指定から解決する。earnItem指定のため木種を台本にベタ書きしない
    // The target set comes from the master's pin param; earnItem targeting keeps tree species out of the scenario
    var pinParam = (MapObjectPinTutorialParam)MasterHolder.ChallengeMaster.GetChallenge(challenge1)
        .Tutorials.First(t => t.TutorialParam is MapObjectPinTutorialParam).TutorialParam;
    var pinTargets = MasterHolder.ChallengeMaster.ResolvePinTargets(pinParam);
    p.Assert(0 < pinTargets.Count, "ピン指定から原木を落とすmapObjectが1件以上解決された");

    // 検証2: k-d tree索引の結果が、シーン内の全候補を総当たりした最寄りと一致する
    // Verify 2: the k-d tree result equals a brute-force nearest over every candidate in the scene
    p.Note("k-d tree索引の最寄り木を総当たり結果と突き合わせる");
    var datastore = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.Map.MapObject.MapObjectGameObjectDatastore>();
    var playerPosition = p.PlayerPosition;
    var indexed = datastore.SearchNearestMapObject(pinTargets, playerPosition);
    var brute = BruteForceNearest(playerPosition);
    p.Assert(indexed != null, "索引が最寄りの木を返した");
    p.Assert(brute != null, "総当たりで最寄りの木が見つかった");
    if (indexed != null && brute != null)
    {
        var indexedDistance = (indexed.transform.position - playerPosition).sqrMagnitude;
        var bruteDistance = (brute.transform.position - playerPosition).sqrMagnitude;
        p.Note($"索引={indexed.InstanceId} d2={indexedDistance:F3} / 総当たり={brute.InstanceId} d2={bruteDistance:F3} / 候補総数={CountAvailableTargets()}");
        p.Assert(Mathf.Abs(indexedDistance - bruteDistance) < 0.001f, "索引の最寄り距離が総当たりと一致する");
    }

    // 検証3: ピンの実座標が最寄りの木に重なっている
    // Verify 3: the pin transform sits on the nearest tree
    var pinComponent = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.Tutorial.MapObjectPin>(FindObjectsInactive.Include);
    p.Assert(pinComponent != null, "MapObjectPinがシーンに存在する");
    var firstPinPosition = pinComponent.transform.position;
    if (indexed != null) p.Assert((firstPinPosition - indexed.transform.position).sqrMagnitude < 0.001f, "ピンが最寄りの木の位置を指している");

    // 検証4: その木を伐採すると、索引とピンが次の木へ移る
    // Verify 4: felling that tree moves both the index result and the pin to the next tree
    // 石の斧は初期装備のため、装備操作なしでそのまま打てる
    // The stone axe is initial equipment, so hitting needs no equip step
    p.Note("最寄りの木を初期装備の石の斧で伐採しきる");
    var felledInstanceId = indexed.InstanceId;
    for (var hit = 0; hit < fellingMaxHits && indexed.IsAvailable; hit++)
    {
        Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.AttackMapObject(felledInstanceId);
        await p.WaitSeconds(axeAttackSpeedSeconds);
    }
    p.Assert(!indexed.IsAvailable, "伐採した木が破壊済みになった");
    await p.Screenshot("02-tree-felled");

    p.Note("伐採後の最寄り探索が次の木へ移ることを確認する");
    var afterPosition = p.PlayerPosition;
    var afterIndexed = await PollUntilResult(() =>
    {
        var candidate = datastore.SearchNearestMapObject(pinTargets, afterPosition);
        return candidate != null && candidate.InstanceId != felledInstanceId ? candidate : null;
    }, 30);
    p.Assert(afterIndexed != null, "伐採後の索引が伐採済みでない別の木を返した");
    if (afterIndexed != null)
    {
        var afterBrute = BruteForceNearest(afterPosition);
        p.Note($"伐採後: 索引={afterIndexed.InstanceId} / 総当たり={afterBrute?.InstanceId}");
        p.Assert(afterBrute != null && Mathf.Abs((afterIndexed.transform.position - afterPosition).sqrMagnitude - (afterBrute.transform.position - afterPosition).sqrMagnitude) < 0.001f,
            "伐採後も索引の最寄り距離が総当たりと一致する");

        var pinMoved = await PollUntil(() => (pinComponent.transform.position - afterIndexed.transform.position).sqrMagnitude < 0.001f, 15);
        p.Assert(pinMoved, "ピンが次の木へ移った");
    }
    await p.Screenshot("03-pin-moved-to-next-tree");

    p.Note("検証完了");

    #region Internal

    // シーン内の生存している候補を総当たりして最寄りを出す。索引から独立した検算用
    // Brute-force the nearest live candidate in the scene as an oracle independent of the index
    Client.Game.InGame.Map.MapObject.MapObjectGameObject BruteForceNearest(Vector3 from)
    {
        Client.Game.InGame.Map.MapObject.MapObjectGameObject best = null;
        var bestDistance = float.MaxValue;
        foreach (var mapObject in UnityEngine.Object.FindObjectsByType<Client.Game.InGame.Map.MapObject.MapObjectGameObject>(FindObjectsSortMode.None))
        {
            if (!pinTargets.Contains(mapObject.MapObjectGuid) || !mapObject.IsAvailable) continue;
            var distance = (mapObject.transform.position - from).sqrMagnitude;
            if (bestDistance <= distance) continue;
            bestDistance = distance;
            best = mapObject;
        }
        return best;
    }

    int CountAvailableTargets()
    {
        return UnityEngine.Object.FindObjectsByType<Client.Game.InGame.Map.MapObject.MapObjectGameObject>(FindObjectsSortMode.None)
            .Count(x => pinTargets.Contains(x.MapObjectGuid) && x.IsAvailable);
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
