using Game.Context;
// 電線ツールの電柱種サイクル・回転E2E(受け入れ4): スクロールで電柱種が切り替わり装備は切り替わらない／Rキーで向きが変わる
// 検証項目: 対照実験(装備枠2以上ならGameScreenのホイールで装備が切り替わる)→ツール使用中のスクロールで電柱種のみ変化し装備選択は不変、Rキーで設置向きが回転→後対照でホイールの占有が外れる
// 占有の判定はツールを選ぶ前は初期値falseを読むだけで空振りするため、真になった後に外れるのを後対照で見る。装備の不変は枠1では切替先が無く常に真なので枠2以上だけ評価する
// 電柱名のゴーストラベルは削除され（437dee26f3）、後継のカーソルツールチップにも電柱名の行が無いため、名前は SelectedPoleName() だけで見る
// Pole-type cycling and rotation E2E (acceptance 4): the wheel cycles pole types without switching equipment, and R rotates the pole.
// Verifies: control case (with 2+ equipment slots the game-screen wheel switches equipment), then wheel under the tool cycles only the pole type, R changes the direction, and the post-control sees the wheel released.
// Before the tool is picked the ownership flag only reads its initial false, so the post-control checks it dropping after being true. Equipment staying put is always true with one slot, so it is evaluated only with 2+ slots.
// The pole-name ghost label was removed (437dee26f3) and the cursor tooltip carries no pole-name line, so the name is read only through SelectedPoleName().
using System;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("electric-wire-tool-pole-cycle-rotate-via-ui", options, async p =>
{
    // 背景EditorではInputSystemがデバイスを無効化し注入が届かないため、焦点無視へ切り替え再有効化する
    // A background Editor disables keyboard/mouse so injection never lands; switch to ignore-focus and re-enable
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(8f, 34f, -6f));
    await p.WaitSeconds(0.5f);
    p.Note("開幕スキットをSkipインテントで飛ばす");
    var skitStore = Client.Skit.UI.SkitPresentationStateStore.Instance;
    await p.Until(() =>
    {
        var s = skitStore.GetCurrent();
        return s != null && skitStore.TrySkip(s.SessionId, s.SceneRevision).Ok;
    }, 30f, "開幕スキットのSkipインテントが受理される");
    await p.WaitUiState(UIStateEnum.GameScreen, 15f);
    // 3種の電柱を解放する（サイクル対象が2種以上ないと検証が空振りする）
    // Unlock all three pole types; with fewer than two the cycle assert would be vacuous
    var wireToolGuid = Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2");
    await p.PrepareBlockForUiPlacement("電柱", 2);
    await p.UnlockBlock("高圧電柱");
    await p.UnlockBlock("広範囲電柱");
    await p.RunOnServer("電線ツールを解放する", () => ServerContext.GetService<IGameUnlockStateDataController>().UnlockConnectTool(wireToolGuid));
    await p.GiveItem("銅のワイヤー", 64);
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var clientUnlockState = resolver.Resolve<IGameUnlockStateData>();
    var poleGuids = new[] { "電柱", "高圧電柱", "広範囲電柱" }
        .Select(name => MasterHolder.BlockMaster.GetBlockMaster(Client.Playtest.Operations.PlaytestBlockOps.ResolveBlockId(name)).BlockGuid)
        .ToArray();
    await p.Until(() => poleGuids.All(guid => clientUnlockState.BlockUnlockStateInfos.TryGetValue(guid, out var info) && info.IsUnlocked), 15f, "3種の電柱アンロックがクライアントへ同期される");
    var wireSystem = resolver.Resolve<ElectricWireConnectSystem>();
    var placeController = resolver.Resolve<PlaceSystemStateController>();
    var equipment = resolver.Resolve<LocalPlayerEquipment>();
    var contextField = typeof(ElectricWireConnectSystem).GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);
    var toolContext = (ElectricWireToolContext)contextField.GetValue(wireSystem);
    async UniTask<int> ReadEquipmentIndex()
    {
        var index = 0;
        await p.RunOnServer("サーバーの装備選択を読む", () => index = ServerContext.GetService<IPlayerInventoryDataStore>()
            .GetInventoryData(ClientContext.PlayerConnectionSetting.PlayerId).EquipmentInventory.SelectedEquipmentIndex);
        return index;
    }
    // 選択中の電柱名はプロダクションと同じ公開APIから引く（検証のために内部状態を開けない）
    // Read the selected pole name through the same public API production uses; never open internals just to observe
    string SelectedPoleName() => toolContext.PoleSelection.TryGetSelectedPole(out _, out var selectedMaster) ? selectedMaster.Name : null;
    // ===== 対照実験: ツール未使用時のホイールは装備を切り替える（装備枠2以上のとき） =====
    // ===== Control: without the tool the wheel switches equipment (with 2+ slots) =====
    await AssertGameScreenWheelSwitchesEquipment("対照実験", "01-control-equipment-wheel");
    // ===== 電線ツールを選択する =====
    // ===== Select the wire tool =====
    p.Note("ビルドメニューのツールカテゴリから電線接続ツールを選ぶ");
    for (var attempt = 0; attempt < 3 && p.CurrentUiState != UIStateEnum.BuildMenu; attempt++)
    {
        await p.PressKey(p.CurrentUiState == UIStateEnum.PlaceBlock ? Key.Tab : Key.B);
        var openDeadline = Time.realtimeSinceStartup + 4f;
        while (Time.realtimeSinceStartup < openDeadline && p.CurrentUiState != UIStateEnum.BuildMenu) await UniTask.DelayFrame(5);
    }
    await p.UntilUiElement("build-menu-panel", 15f);
    await p.ClickUi("build-menu-category-d1000000-0000-4000-8000-000000000009");
    await p.ClickUi($"build-menu-entry-connectTool-{wireToolGuid:D}");
    await p.WaitUiState(UIStateEnum.PlaceBlock, 15f);
    p.Assert((placeController.CurrentTarget as ConnectToolPlacementTarget)?.ConnectToolGuid == wireToolGuid, "電線接続ツールが選択される");
    // 空間へ照準して電柱ゴーストを出す
    // Aim at empty ground so the pole ghost appears
    await p.AimAt(new Vector3(8.5f, 32f, 6.5f));
    await UniTask.DelayFrame(5);
    p.Assert(toolContext.PoleSelection.HasSelectablePole, "解放済み電柱が選択候補にある");
    var poleNameBefore = SelectedPoleName();
    var equipmentBefore = equipment.SelectedIndex;
    var serverEquipmentBefore = await ReadEquipmentIndex();
    p.Assert(placeController.IsWheelOwnedByTool, "受け入れ4: 電柱が2種以上の電線ツールはホイールを占有する");
    p.Note($"ツール使用中の初期状態: 電柱種={poleNameBefore} / 装備選択={equipmentBefore}");
    await p.Screenshot("02-pole-type-1");
    // ===== スクロール1回目: 電柱種が次へサイクルし、装備は動かない =====
    // ===== First wheel notch: the pole type cycles and equipment stays put =====
    p.Note("スクロールを1ノッチ回す（電柱種は次へ、装備は不変のはず）");
    await InjectScroll(1f);
    await p.Until(() => SelectedPoleName() != poleNameBefore, 10f, "受け入れ4: スクロールで電柱種が切り替わる");
    var poleNameSecond = SelectedPoleName();
    p.Note($"電柱種: {poleNameBefore} → {poleNameSecond} / 装備選択: {equipmentBefore} → {equipment.SelectedIndex}");
    AssertEquipmentUnchanged($"受け入れ4: ツール使用中のスクロールで装備は切り替わらない (前={equipmentBefore} 後={equipment.SelectedIndex})");
    var serverEquipmentAfter = await ReadEquipmentIndex();
    if (1 < equipment.Slots.Count) p.Assert(serverEquipmentAfter == serverEquipmentBefore, $"受け入れ4: サーバー側の装備選択も不変 (前={serverEquipmentBefore} 後={serverEquipmentAfter})");
    await p.Screenshot("03-pole-type-2");
    // ===== スクロール2回目: さらに次の電柱種へ =====
    // ===== Second notch: cycle once more =====
    p.Note("さらにスクロールし3種目の電柱へ切り替える");
    await InjectScroll(1f);
    await p.Until(() => SelectedPoleName() != poleNameSecond, 10f, "受け入れ4: もう1ノッチで次の電柱種へ切り替わる");
    var poleNameThird = SelectedPoleName();
    p.Note($"電柱種: {poleNameSecond} → {poleNameThird} / 装備選択: {equipment.SelectedIndex}");
    AssertEquipmentUnchanged("受け入れ4: 2ノッチ目でも装備は切り替わらない");
    p.Assert(new[] { poleNameBefore, poleNameSecond, poleNameThird }.Distinct().Count() == 3, $"3種の電柱を順にサイクルした ({poleNameBefore}/{poleNameSecond}/{poleNameThird})");
    await p.Screenshot("04-pole-type-3");
    // ===== 逆スクロールで前の電柱種へ戻る =====
    // ===== Reverse wheel returns to the previous pole type =====
    p.Note("逆方向にスクロールして前の電柱種へ戻す");
    await InjectScroll(-1f);
    await p.Until(() => SelectedPoleName() == poleNameSecond, 10f, "逆スクロールで前の電柱種へ戻る");
    AssertEquipmentUnchanged("受け入れ4: 逆スクロールでも装備は切り替わらない");
    // ===== Rキーで向きが変わる =====
    // ===== R key rotates the pole =====
    p.Note("Rキーを押して設置向きを回転させる");
    var directionBefore = toolContext.PoleSelection.CurrentDirection;
    await p.PressKey(Key.R);
    await p.Until(() => toolContext.PoleSelection.CurrentDirection != directionBefore, 10f, "受け入れ4: 回転キーで向きが変わる");
    var directionAfter = toolContext.PoleSelection.CurrentDirection;
    p.Note($"設置向き: {directionBefore} → {directionAfter}");
    await p.Screenshot("05-rotated-once");
    await p.PressKey(Key.R);
    await p.Until(() => toolContext.PoleSelection.CurrentDirection != directionAfter, 10f, "回転キーをもう一度押すとさらに向きが変わる");
    p.Note($"設置向き: {directionAfter} → {toolContext.PoleSelection.CurrentDirection}");
    AssertEquipmentUnchanged("回転操作でも装備は切り替わらない");
    await p.Screenshot("06-rotated-twice");
    // ===== 後対照: ツールを抜ければホイールの占有が外れる =====
    // ===== Post-control: leaving the tool releases the wheel =====
    p.Note("後対照: ツールを抜けてゲーム画面へ戻り、ホイールの占有が外れる（装備枠2以上なら装備が切り替わる）ことを確認する");
    await p.ExitToGameScreen();
    await UniTask.DelayFrame(3);
    p.Assert(!placeController.IsWheelOwnedByTool, "後対照: ツールを抜けるとホイールの占有が外れる（ツール使用中は真だった）");
    await AssertGameScreenWheelSwitchesEquipment("後対照", "07-post-control-equipment-wheel");
    #region Internal
    // 装備枠2以上なら、ゲーム画面の2ノッチで装備が切り替わるのを見る
    // With 2+ equipment slots, two notches on the game screen switch the equipment
    async UniTask AssertGameScreenWheelSwitchesEquipment(string label, string screenshotName)
    {
        // 注入ホイールはCEFへポインタ座標込みで転送されるため、画面中央へ移動してから回す
        // The injected wheel is forwarded to CEF with the pointer position, so move to the screen center first
        SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.5f, Screen.height * 0.45f));
        await UniTask.DelayFrame(3);
        // 装備枠1のマスタは切替先が無く EquipmentWheelSelector が何もしないので、装備の変化は見ない
        // With a single equipment slot EquipmentWheelSelector has nowhere to switch, so the equipment change is not checked
        if (equipment.Slots.Count <= 1)
        {
            p.Note($"{label}: 装備枠数={equipment.Slots.Count} のため装備選択の変化は未検査");
            await p.Screenshot(screenshotName);
            return;
        }
        var before = equipment.SelectedIndex;
        // Web の装備切替は 100px で 1 段（equipmentLogic.ts）、1 ノッチは 60px なので 2 ノッチ回す
        // The Web equipment switch steps per 100px (equipmentLogic.ts) and one notch is 60px, so turn 2 notches
        await InjectScroll(2f);
        var deadline = Time.realtimeSinceStartup + 6f;
        while (Time.realtimeSinceStartup < deadline && equipment.SelectedIndex == before) await UniTask.DelayFrame(5);
        p.Assert(equipment.SelectedIndex != before, $"{label}: ゲーム画面のホイールで装備選択が変わる (前={before} 後={equipment.SelectedIndex})");
        await p.Screenshot(screenshotName);
    }
    // 装備枠1では切替先が無く常に真になるため、枠2以上だけ評価し、枠1は未検査と記す
    // With one slot there is nowhere to switch so it is always true; evaluate only with 2+ slots and note it as unchecked otherwise
    void AssertEquipmentUnchanged(string message)
    {
        if (1 < equipment.Slots.Count) p.Assert(equipment.SelectedIndex == equipmentBefore, message);
        else p.Note($"装備枠1のため未検査: {message}");
    }
    // ホイールをノッチ数（正で上・負で下）で注入する。SemanticInput.MouseWheel は実マウスと同じ 1 ノッチ＝1 の値を入れる
    // Inject wheel notches (positive up, negative down); SemanticInput.MouseWheel queues the same 1-per-notch value a real mouse gives
    async UniTask InjectScroll(float notches)
    {
        SemanticInput.MouseWheel(notches);
        await UniTask.DelayFrame(4);
        await p.WaitSeconds(0.5f);
    }
    #endregion
});
