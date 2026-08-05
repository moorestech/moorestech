// 電線ツールの電柱種サイクル・回転E2E(受け入れ4): スクロールで電柱種が切り替わり装備は切り替わらない／Rキーで向きが変わる
// 検証項目: 対照実験(GameScreenのスクロールでは装備が切り替わる)→ツール使用中のスクロールで電柱種のみ変化し装備選択は不変、Rキーで設置向きが回転
// Pole-type cycling and rotation E2E (acceptance 4): the wheel cycles pole types without switching equipment, and R rotates the pole.
// Verifies: control case (wheel on the game screen does switch equipment), then wheel under the tool cycles only the pole type, and R changes the direction.
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
using Client.Playtest.WebUi;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("electric-wire-tool-pole-cycle-rotate-via-ui", options, async p =>
{
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
    p.UnlockBlock("高圧電柱");
    p.UnlockBlock("広範囲電柱");
    p.ServerService<IGameUnlockStateDataController>().UnlockConnectTool(wireToolGuid);
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
    var serverEquipment = p.ServerService<IPlayerInventoryDataStore>()
        .GetInventoryData(ClientContext.PlayerConnectionSetting.PlayerId).EquipmentInventory;

    string SelectedPoleName() => MasterHolder.BlockMaster.GetBlockMaster(toolContext.PoleSelection.SelectedBlockId).Name;
    TextMeshPro PoleNameLabel() => UnityEngine.Object
        .FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .FirstOrDefault(t => t.gameObject.name == "ElectricWirePoleNameLabel");

    // ===== 対照実験: ツール未使用時はホイールで装備が切り替わる =====
    // ===== Control: without the tool, the wheel does switch equipment =====
    p.Note($"対照実験: ゲーム画面でホイールを回すと装備が切り替わることを先に確認する（装備枠数={equipment.Slots.Count}）");
    // 注入ホイールはCEFへポインタ座標込みで転送されるため、画面中央へ移動してから回す
    // The injected wheel is forwarded to CEF with the pointer position, so move to the screen center first
    SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.5f, Screen.height * 0.45f));
    await UniTask.DelayFrame(3);
    var controlBefore = equipment.SelectedIndex;
    await InjectScroll(120f);
    var controlDeadline = Time.realtimeSinceStartup + 6f;
    while (Time.realtimeSinceStartup < controlDeadline && equipment.SelectedIndex == controlBefore) await UniTask.DelayFrame(5);
    p.Assert(equipment.SelectedIndex != controlBefore, $"対照実験: ゲーム画面のホイールで装備選択が変わる (前={controlBefore} 後={equipment.SelectedIndex})");
    p.Note($"対照実験の結果: 装備選択 {controlBefore} → {equipment.SelectedIndex}");
    await p.Screenshot("01-control-equipment-switched-by-wheel");

    // ===== 電線ツールを選択する =====
    // ===== Select the wire tool =====
    p.Note("ビルドメニューのツールカテゴリから電線接続ツールを選ぶ");
    for (var attempt = 0; attempt < 3 && p.CurrentUiState != UIStateEnum.BuildMenu; attempt++)
    {
        await p.PressKey(p.CurrentUiState == UIStateEnum.PlaceBlock ? Key.Tab : Key.B);
        var openDeadline = Time.realtimeSinceStartup + 4f;
        while (Time.realtimeSinceStartup < openDeadline && p.CurrentUiState != UIStateEnum.BuildMenu) await UniTask.DelayFrame(5);
    }
    await p.UntilWebUiElement("build-menu-panel", 15f);
    await p.ClickWebUi("build-menu-category-d1000000-0000-4000-8000-000000000009");
    await p.ClickWebUi($"build-menu-entry-connectTool-{wireToolGuid:D}");
    await p.WaitUiState(UIStateEnum.PlaceBlock, 15f);
    p.Assert((placeController.CurrentTarget as ConnectToolPlacementTarget)?.ConnectToolGuid == wireToolGuid, "電線接続ツールが選択される");

    // 空間へ照準して電柱ゴーストと名前ラベルを出す
    // Aim at empty ground so the pole ghost and its name label appear
    await p.AimAt(new Vector3(8.5f, 32f, 6.5f));
    await UniTask.DelayFrame(5);
    p.Assert(toolContext.PoleSelection.HasSelectablePole, "解放済み電柱が選択候補にある");
    var poleNameBefore = SelectedPoleName();
    var equipmentBefore = equipment.SelectedIndex;
    var serverEquipmentBefore = serverEquipment.SelectedEquipmentIndex;
    p.Note($"ツール使用中の初期状態: 電柱種={poleNameBefore} / 装備選択={equipmentBefore}");
    await p.Screenshot("02-pole-type-1");

    // ===== スクロール1回目: 電柱種が次へサイクルし、装備は動かない =====
    // ===== First wheel notch: the pole type cycles and equipment stays put =====
    p.Note("スクロールを1ノッチ回す（電柱種は次へ、装備は不変のはず）");
    await InjectScroll(120f);
    await p.Until(() => SelectedPoleName() != poleNameBefore, 10f, "受け入れ4: スクロールで電柱種が切り替わる");
    var poleNameSecond = SelectedPoleName();
    p.Note($"電柱種: {poleNameBefore} → {poleNameSecond} / 装備選択: {equipmentBefore} → {equipment.SelectedIndex}");
    p.Assert(equipment.SelectedIndex == equipmentBefore, $"受け入れ4: ツール使用中のスクロールで装備は切り替わらない (前={equipmentBefore} 後={equipment.SelectedIndex})");
    p.Assert(serverEquipment.SelectedEquipmentIndex == serverEquipmentBefore, $"受け入れ4: サーバー側の装備選択も不変 (前={serverEquipmentBefore} 後={serverEquipment.SelectedEquipmentIndex})");
    var label = PoleNameLabel();
    p.Assert(label != null && label.text == poleNameSecond, $"ゴーストの名前ラベルが選択中の電柱種を示す 実際:'{label?.text}'");
    await p.Screenshot("03-pole-type-2");

    // ===== スクロール2回目: さらに次の電柱種へ =====
    // ===== Second notch: cycle once more =====
    p.Note("さらにスクロールし3種目の電柱へ切り替える");
    await InjectScroll(120f);
    await p.Until(() => SelectedPoleName() != poleNameSecond, 10f, "受け入れ4: もう1ノッチで次の電柱種へ切り替わる");
    var poleNameThird = SelectedPoleName();
    p.Note($"電柱種: {poleNameSecond} → {poleNameThird} / 装備選択: {equipment.SelectedIndex}");
    p.Assert(equipment.SelectedIndex == equipmentBefore, "受け入れ4: 2ノッチ目でも装備は切り替わらない");
    p.Assert(new[] { poleNameBefore, poleNameSecond, poleNameThird }.Distinct().Count() == 3, $"3種の電柱を順にサイクルした ({poleNameBefore}/{poleNameSecond}/{poleNameThird})");
    await p.Screenshot("04-pole-type-3");

    // ===== 逆スクロールで前の電柱種へ戻る =====
    // ===== Reverse wheel returns to the previous pole type =====
    p.Note("逆方向にスクロールして前の電柱種へ戻す");
    await InjectScroll(-120f);
    await p.Until(() => SelectedPoleName() == poleNameSecond, 10f, "逆スクロールで前の電柱種へ戻る");
    p.Assert(equipment.SelectedIndex == equipmentBefore, "受け入れ4: 逆スクロールでも装備は切り替わらない");

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
    p.Assert(equipment.SelectedIndex == equipmentBefore, "回転操作でも装備は切り替わらない");
    await p.Screenshot("06-rotated-twice");

    // ===== 後対照: ツールを抜ければ同じホイール注入で装備が再び切り替わる =====
    // ===== Post-control: leaving the tool makes the same wheel injection switch equipment again =====
    p.Note("後対照: ツールを抜けてゲーム画面へ戻り、同じホイール注入で装備が切り替わることを確認する");
    await p.ExitToGameScreen();
    SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.5f, Screen.height * 0.45f));
    await UniTask.DelayFrame(3);
    var postControlBefore = equipment.SelectedIndex;
    await InjectScroll(120f);
    var postDeadline = Time.realtimeSinceStartup + 6f;
    while (Time.realtimeSinceStartup < postDeadline && equipment.SelectedIndex == postControlBefore) await UniTask.DelayFrame(5);
    p.Assert(equipment.SelectedIndex != postControlBefore, $"後対照: ツール解除後は同じホイールで装備が切り替わる (前={postControlBefore} 後={equipment.SelectedIndex})");
    p.Note($"後対照の結果: 装備選択 {postControlBefore} → {equipment.SelectedIndex}（抑止はツール使用中に限定されている）");
    await p.Screenshot("07-post-control-equipment-switched");

    #region Internal

    // InputSystemのMouseStateへscrollを直接積む（SemanticInputはscrollを持たないため）
    // Queue a MouseState carrying scroll directly (SemanticInput has no scroll API)
    async UniTask InjectScroll(float scrollY)
    {
        var mouse = Mouse.current;
        InputSystem.QueueStateEvent(mouse, new MouseState
        {
            position = mouse.position.ReadValue(),
            delta = Vector2.zero,
            scroll = new Vector2(0f, scrollY),
        });
        await UniTask.DelayFrame(4);
        await p.WaitSeconds(0.5f);
    }

    #endregion
});
