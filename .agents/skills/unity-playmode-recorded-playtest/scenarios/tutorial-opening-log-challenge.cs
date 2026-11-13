// シナリオ: 序盤の先頭チャレンジ（原木）を自然なマップで通す。開幕スキット → 木ピン → 目標 HUD の枠 → 伐採で完了 → 次のキー操作ヒントまで
// Scenario: run the opening challenge (logs) on the natural map: opening skit -> tree pin -> objective HUD outline -> chop to complete -> next key hint
// チャレンジ・ピン・初期装備はマスタから引く。マスタの形が変わったら最初の Single で止める（意図）
// Challenges, the pin and the starting tool come from the master; a reshaped master stops at the first Single (intended)
// SetupDebugEnvironment は呼ばない（自然なマップの木とスポーンを残す）。全体スイッチの GameUi・Web の両方で流す
// Never call SetupDebugEnvironment (keep the natural map's trees and spawn); run under both GameUi and Web
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.PlayerInventory.Interface;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 300f };
return PlaytestRunner.Run("tutorial-opening-log-challenge", options, async p =>
{
    // 系の記録と入力の準備（前例 uitk-skit）
    // Record the UI system and prepare input (precedent uitk-skit)
    p.Note($"系={UiInputExclusivity.ActiveUiSystem}");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    // ピンの解決失敗は LogError だけで Success を落とさないので自前で拾う
    // A failed pin resolution only logs an error and never fails the run, so collect it here
    var pinErrors = new List<string>();
    Application.LogCallback onLog = (condition, _, type) =>
    {
        if (type == LogType.Error && condition.Contains("未破壊のMapObject")) pinErrors.Add(condition);
    };
    Application.logMessageReceived += onLog;

    // マスタから先頭・2 番目のチャレンジ、木ピン、初期装備を引く
    // Pull the first and second challenges, the tree pin and the starting tool from the master
    var category = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements.Single(c => c.InitialUnlocked);
    var first = MasterHolder.ChallengeMaster.GetCategoryInitialChallenges(category.CategoryGuid).Single();
    var firstTask = (InInventoryItemTaskParam)first.TaskParam;
    var logName = MasterHolder.ItemMaster.GetItemMaster(firstTask.ItemGuid).Name;
    var pinTutorial = first.Tutorials.Single(t => t.TutorialType == TutorialsElement.TutorialTypeConst.mapObjectPin);
    var pinTutorialGuid = pinTutorial.TutorialGuid.ToString("D");
    var treeCandidates = MasterHolder.ChallengeMaster.ResolvePinTargets((MapObjectPinTutorialParam)pinTutorial.TutorialParam);
    var second = MasterHolder.ChallengeMaster.GetNextChallenges(first.ChallengeGuid).Single();
    var initialTool = MasterHolder.ItemMaster.Items.InitialEquipmentItems[0].ItemGuid;
    var pinStore = WorldPinStateStore.Instance;

    // 1. 先頭チャレンジがカレントで、初期装備（石の斧）を選んだ状態で始まる（ADR 0038）
    // 1. The first challenge is current and the starting tool (stone axe) is selected (ADR 0038)
    await p.WaitForChallenge(first.ChallengeGuid, false, 30, "先頭チャレンジ（原木）がカレント");
    var selectedId = 0;
    await p.RunOnServer("選択中の装備を読む", () => selectedId = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(ClientContext.PlayerConnectionSetting.PlayerId).EquipmentInventory.GetSelectedItem().Id.AsPrimitive());
    var initialToolId = MasterHolder.ItemMaster.GetItemId(initialTool).AsPrimitive();
    p.Assert(selectedId == initialToolId, $"石の斧を選んで装備した状態で始まる（ADR 0038。期待 {initialToolId} 実測 {selectedId}）");

    // 2. 開幕スキットが出て、Skip インテントで消える
    // 2. The opening skit shows and the skip intent removes it
    await UntilUiPresence("blocking-skit", true, 60f, "開幕スキット（blocking-skit）が出た");
    await p.Screenshot("01-opening-skit");
    await p.SkipOpeningSkit();
    await UntilUiPresence("blocking-skit", false, 30f, "開幕スキットが消えた");

    // 3. 木ピンが先頭チャレンジの tutorialGuid で出る（ピン ID は共通なので tutorialGuid も照らす）
    // 3. The tree pin appears under the first challenge's tutorialGuid (the pin id is shared, so match the guid too)
    await p.Until(() => HasTreePin(), 30f, "木ピンが先頭チャレンジの tutorialGuid で store に入った");
    await UntilUiPresence("world-pin-overlay", true, 15f, "world-pin-overlay が出た");
    await p.Screenshot("02-tree-pin");

    // 4. 目標 HUD（challenge.current-hud）の枠とラベル
    // 4. The outline and label on the objective HUD (challenge.current-hud)
    await p.Until(() => Elements().OfType<TutorialOutlineElementData>().Any(o => o.AnchorId == AnchorIds.ChallengeCurrentHud), 15f, "store に challenge.current-hud の枠がある");
    await UntilUiPresence("tutorial-highlight-label", true, 15f, "枠のラベル（tutorial-highlight-label）が出た");
    await p.Screenshot("03-challenge-hud-highlight");

    // 5. 最寄りの木を打って原木を集める。hp10 の木は 1 打で倒れるので毎打探し直す
    // 5. Hit the nearest tree for logs; hp10 trees fall in one hit, so search again before every hit
    var datastore = Object.FindFirstObjectByType<MapObjectGameObjectDatastore>();
    await p.Until(() => datastore.SearchNearestMapObject(treeCandidates, p.PlayerPosition) != null, 30f, $"ピン先（{treeCandidates.Count} 種）の最寄りの木が見つかった");
    for (var hit = 0; hit < 6 && p.CountItem(logName) < firstTask.ItemCount; hit++)
    {
        var tree = datastore.SearchNearestMapObject(treeCandidates, p.PlayerPosition);
        p.Note($"{hit + 1} 打目: 木 {tree.InstanceId}（所持 {logName} {p.CountItem(logName)}）");
        ClientContext.VanillaApi.SendOnly.AttackMapObject(tree.InstanceId);
        // 石の斧の attackSpeed 1 秒をクールダウンの許容より長く待つ
        // Wait past the stone axe's 1s attack speed so the cooldown accepts the next hit
        await p.WaitSeconds(1.1f);
    }
    await p.WaitForChallenge(first.ChallengeGuid, true, 30, $"{logName}を {firstTask.ItemCount} 個集めて先頭チャレンジが完了した");
    await p.Screenshot("04-first-done");

    // 6. 2 番目がカレントになり、木ピンが store から消える
    // 6. The second challenge becomes current and the tree pin leaves the store
    await p.WaitForChallenge(second.ChallengeGuid, false, 30, "2 番目のチャレンジがカレント");
    await p.Until(() => !HasTreePin(), 10f, "完了で木ピンが store から消えた");

    // 7. 2 番目の keyControl（Tab）のヒントがマスタどおり出る
    // 7. The second challenge's keyControl (Tab) hint shows as the master says
    var keyTutorial = second.Tutorials.Single(t => t.TutorialType == TutorialsElement.TutorialTypeConst.keyControl);
    var keyParam = (KeyControlTutorialParam)keyTutorial.TutorialParam;
    var keyTutorialGuid = keyTutorial.TutorialGuid.ToString("D");
    await p.Until(() => Elements().OfType<TutorialKeyControlElementData>().Any(k => k.TutorialGuid == keyTutorialGuid && k.KeyName == keyParam.KeyName), 15f, $"store に 2 番目の keyControl（{keyParam.KeyName}）がある");
    await UntilUiPresence("key-control-hint", true, 15f, "key-control-hint が出た");
    await p.Screenshot("05-next-key-hint");

    Application.logMessageReceived -= onLog;
    p.Assert(pinErrors.Count == 0, $"「未破壊のMapObject」の LogError が出ていない（実測 {pinErrors.Count} 件）");

    #region Internal

    bool HasTreePin() => pinStore.GetCurrent().Pins.Any(x => x.PinId == "map-object-pin" && x.TutorialGuid == pinTutorialGuid);

    IEnumerable<TutorialOverlayElementData> Elements() => TutorialPresentationStateStore.Instance.GetCurrent().Sessions.SelectMany(s => s.Elements);

    // FindUi を await して出る（消える）まで回す。HUD は当たり判定を持たないので UntilUiElement は使えない
    // Await FindUi until it appears (or vanishes); HUDs ignore picking, so UntilUiElement cannot be used
    async UniTask UntilUiPresence(string testid, bool present, float timeoutSeconds, string label)
    {
        var deadline = Time.realtimeSinceStartup + timeoutSeconds;
        var found = (await PlaytestUiElementOps.FindUi(testid)).IsFound;
        while (found != present && Time.realtimeSinceStartup < deadline)
        {
            await UniTask.DelayFrame(1);
            found = (await PlaytestUiElementOps.FindUi(testid)).IsFound;
        }
        p.Assert(found == present, label);
    }

    #endregion
});
