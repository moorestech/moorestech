// シナリオ: 先頭チャレンジの木ピンで k-d tree 索引の最寄り探索を確かめる。(a) 総当たりと一致 (b) ピンがその木に重なる (c) 壊すと索引とピンが次の木へ移る
// Scenario: check the k-d tree nearest search through the first challenge's tree pin: (a) matches brute force (b) the pin sits on it (c) destroying it moves index and pin to the next tree
// 木は採掘で倒さずサーバーで壊す。原木を得ると先頭が完了しピンが消えて (c) を見られないため（採掘の完了は tutorial-opening-log-challenge が持つ）
// Trees are destroyed on the server instead of mined: logs would complete the first challenge and remove the pin before (c) (tutorial-opening-log-challenge covers mining)
// SetupDebugEnvironment は呼ばない（自然なマップの木とスポーンを残す）。全体スイッチの GameUi・Web の両方で流す
// Never call SetupDebugEnvironment (keep the natural map's trees and spawn); run under both GameUi and Web
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Tutorial;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Core.Master;
using Game.Context;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 300f };
return PlaytestRunner.Run("tutorial-tree-pin-nearest-search", options, async p =>
{
    // 系の記録と入力の準備（前例 uitk-skit）
    // Record the UI system and prepare input (precedent uitk-skit)
    p.Note($"系={UiInputExclusivity.ActiveUiSystem}");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    // マスタから先頭チャレンジと木ピンの候補を引く（ピンと同じ入口 ResolvePinTargets）
    // Pull the first challenge and the tree pin candidates from the master (same entry as the pin, ResolvePinTargets)
    var category = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements.Single(c => c.InitialUnlocked);
    var first = MasterHolder.ChallengeMaster.GetCategoryInitialChallenges(category.CategoryGuid).Single();
    var pinTutorial = first.Tutorials.Single(t => t.TutorialType == TutorialsElement.TutorialTypeConst.mapObjectPin);
    var pinTutorialGuid = pinTutorial.TutorialGuid.ToString("D");
    var treeCandidates = MasterHolder.ChallengeMaster.ResolvePinTargets((MapObjectPinTutorialParam)pinTutorial.TutorialParam);
    var pinStore = WorldPinStateStore.Instance;

    // 1. 先頭がカレントで、スキットを飛ばすと木ピンが出る
    // 1. The first challenge is current and the tree pin shows once the skit is skipped
    await p.WaitForChallenge(first.ChallengeGuid, false, 30, "先頭チャレンジ（原木）がカレント");
    await p.SkipOpeningSkit();
    await p.Until(() => pinStore.GetCurrent().Pins.Any(x => x.PinId == "map-object-pin" && x.TutorialGuid == pinTutorialGuid), 30f, "木ピンが先頭チャレンジの tutorialGuid で出た");
    await p.Screenshot("01-tree-pin");

    // 2. (a) 索引の最寄りが総当たりの最寄りと同じ距離
    // 2. (a) The index's nearest has the same distance as the brute-force nearest
    var datastore = Object.FindFirstObjectByType<MapObjectGameObjectDatastore>();
    // 後着の生成は 1 フレーム 4ms で数分かかるので待たない。索引と総当たりは同じフレームで同じ生成済みの集合を見る
    // Background instantiation runs at 4ms a frame and takes minutes, so skip it; index and brute force read the same instantiated set in one frame
    await p.Until(() => datastore.SearchNearestMapObject(treeCandidates, p.PlayerPosition) != null, 30f, "索引が最寄りの木を返す");
    p.Note($"全生成済み={datastore.IsAllInstantiated.Value}");
    var position = p.PlayerPosition;
    var indexed = datastore.SearchNearestMapObject(treeCandidates, position);
    var brute = BruteForceNearest(position, -1);
    p.Assert(indexed != null && brute != null, $"索引と総当たりの両方が最寄りの木を返した（候補 {treeCandidates.Count} 種・生存 {LiveTrees().Count()} 本）");
    p.Note($"索引={indexed.InstanceId} d2={Distance2(indexed, position):F3} / 総当たり={brute.InstanceId} d2={Distance2(brute, position):F3}");
    p.Assert(Mathf.Abs(Distance2(indexed, position) - Distance2(brute, position)) < 0.001f, "(a) 索引の最寄り距離が総当たりと一致する");

    // 3. (b) ピンの実座標が索引の木に重なる
    // 3. (b) The pin transform sits on the index's tree
    var pin = Object.FindFirstObjectByType<MapObjectPin>(FindObjectsInactive.Include);
    await p.Until(() => (pin.transform.position - indexed.transform.position).sqrMagnitude < 0.001f, 10f, "(b) ピンが最寄りの木の位置を指している");

    // 4. その木を採掘無しでサーバーから壊す。原木は入らないので先頭はまだカレント
    // 4. Destroy that tree on the server without mining; no logs arrive, so the first challenge stays current
    var felledId = indexed.InstanceId;
    await p.RunOnServer("最寄りの木を採掘無しで壊す", () => ServerContext.MapObjectDatastore.Get(felledId).Destroy());
    await p.Until(() => !indexed.IsAvailable, 10f, "壊した木がクライアントで破壊済み");
    p.Assert(await p.ReadChallenge(first.ChallengeGuid, false) && !await p.ReadChallenge(first.ChallengeGuid, true), "原木を得ていないので先頭チャレンジはまだカレント");
    await p.Screenshot("02-tree-destroyed");

    // 5. (c) 索引が壊した木以外を返して総当たりと一致し、ピンがその木へ移る
    // 5. (c) The index returns a tree other than the destroyed one, matching brute force, and the pin moves there
    var after = p.PlayerPosition;
    await p.Until(() => datastore.SearchNearestMapObject(treeCandidates, after) is { } next && next.InstanceId != felledId, 10f, "壊した後の索引が別の木を返した");
    var afterIndexed = datastore.SearchNearestMapObject(treeCandidates, after);
    var afterBrute = BruteForceNearest(after, felledId);
    p.Note($"壊した後: 索引={afterIndexed.InstanceId} / 総当たり={afterBrute.InstanceId}");
    p.Assert(Mathf.Abs(Distance2(afterIndexed, after) - Distance2(afterBrute, after)) < 0.001f, "(c) 壊した後も索引の最寄り距離が総当たりと一致する");
    await p.Until(() => (pin.transform.position - afterIndexed.transform.position).sqrMagnitude < 0.001f, 15f, "(c) ピンが次の木へ移った");
    await p.Screenshot("03-pin-moved-to-next-tree");

    #region Internal

    // 候補に入る生存中の木（非活性も含める。索引は距離で隠した木も持つ）
    // Live candidate trees, inactive ones included (the index also holds trees hidden by distance)
    IEnumerable<MapObjectGameObject> LiveTrees() => Object.FindObjectsByType<MapObjectGameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(m => treeCandidates.Contains(m.MapObjectGuid) && m.IsAvailable);

    // 索引から独立した検算用の総当たり。excludeId は壊した木が配信前に残る窓を除くため
    // Brute-force oracle independent of the index; excludeId drops the destroyed tree during the sync window
    MapObjectGameObject BruteForceNearest(Vector3 from, int excludeId) => LiveTrees().Where(m => m.InstanceId != excludeId).OrderBy(m => Distance2(m, from)).FirstOrDefault();

    float Distance2(MapObjectGameObject mapObject, Vector3 from) => (mapObject.transform.position - from).sqrMagnitude;

    #endregion
});
