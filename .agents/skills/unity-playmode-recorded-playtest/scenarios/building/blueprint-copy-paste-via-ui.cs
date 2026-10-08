// ⚠ 旧uGUI版（BuildMenuView等）を置き換えたWeb UI版。名前入力の文字打ちだけは状態へ直接書く（Webモーダルの文字入力はDSL未対応）
// Web-UI rewrite replacing the old uGUI scenario; only the name text is written to the state directly (DSL cannot type into the web modal)
// 検証: 1クリック始点/終点・E高さ・右短押しで終点選択へ戻る・側面ヒットの始点セル・範囲内0の拒否・貼り付けE・ドラッグ列5個・サムネイル
// Checks one-click bounds, height, cancellation, side hits, empty ranges, drag paste, and thumbnails
using System.Linq;
using UniRx;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Blueprint;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("blueprint-copy-paste-via-ui", options, async p =>
{
    // 背景EditorではInputSystemがデバイスを無効化し注入が届かないため、焦点無視へ切り替え再有効化する（beads moorestech-xsd18.4 (b)）
    // A background Editor disables keyboard/mouse so injection never lands; switch to ignore-focus and re-enable (beads moorestech-xsd18.4 (b))
    var inputSettings = InputSystem.settings;
    inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    p.Hotbar.UnlockBlueprint();

    p.PlaceBlockDirect("木のチェスト", new Vector3Int(2, 32, 2), BlockDirection.North);
    p.PlaceBlockDirect("石窯", new Vector3Int(10, 32, 2), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(2, 32, 2));
    await p.WaitBlockGameObject(new Vector3Int(10, 32, 2));
    p.WarpPlayer(new Vector3(6f, 33.5f, -5f));

    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var nameState = resolver.Resolve<BlueprintNameInputState>();
    var heightOffset = resolver.Resolve<PlacementHeightOffset>();
    var checks = new BlueprintCopyPasteScenarioChecks(p, resolver.Resolve<MouseCursorTooltipState>());
    var isNameOpen = false;
    using var nameSubscription = nameState.OnOpenChanged.Subscribe(value => isNameOpen = value);
    var datastore = p.ServerService<IBlueprintDatastore>();

    const string ToolCategory = "build-menu-category-d1000000-0000-4000-8000-000000000009";
    const string CopyToolEntry = "build-menu-entry-blueprintCopy-88dd687d-aceb-4aeb-94e8-44be1e1f5a0d";
    const string BlueprintCategory = "build-menu-category-d1000000-0000-4000-8000-000000000010";

    p.Note("BPコピーツールを選択");
    await checks.SelectEntry(ToolCategory, CopyToolEntry);

    p.Note("始点: 地面(0.5,32,0.5)を1クリック。赤マーカーがセルに乗る");
    await p.AimAt(new Vector3(0.5f, 32f, 0.5f));
    await p.ClickPlace();
    checks.AssertMarker("BlueprintCopyStartMarker", new Vector3Int(0, 32, 0), "始点マーカー(0,32,0)");

    p.Note("Eで終点を1段上げ、地面(4.5,32,4.5)へホバー。範囲は(0,32,0)-(4,33,4)、範囲内1ブロック");
    await p.PressKey(Key.E);
    await p.AimAt(new Vector3(4.5f, 32f, 4.5f));
    await UniTask.DelayFrame(3);
    checks.AssertMarker("BlueprintCopyEndMarker", new Vector3Int(4, 33, 4), "終点マーカー(4,33,4)");
    checks.AssertRangeBox(new Vector3Int(0, 32, 0), new Vector3Int(4, 33, 4), "範囲ボックス");
    p.Assert(checks.TooltipHasParam("1"), "ツールチップに範囲内1ブロック");
    await p.Screenshot("01-selecting-end");

    p.Note("終点クリックで名前入力が開く。Web側キャンセル（nameState.Cancel＝モーダルの閉じる/ESC相当）で終点選択へ戻る（始点は残る）");
    await p.ClickPlace();
    await p.UntilWebUiElement("modal-input", 10f);
    p.Assert(isNameOpen, "名前入力が開く");
    nameState.Cancel();
    await UniTask.DelayFrame(5);
    p.Assert(!isNameOpen, "Webキャンセルで名前入力が閉じる");
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, $"Webキャンセル後もPlaceBlock 実際:{p.CurrentUiState}");
    checks.AssertMarker("BlueprintCopyStartMarker", new Vector3Int(0, 32, 0), "Webキャンセル後も始点マーカーが残る");

    p.Note("もう一度終点クリックし、今度はゲーム側の右短押しで終点選択へ戻る");
    await p.ClickPlace();
    await p.UntilWebUiElement("modal-input", 10f);
    await p.RightShortClick();
    await UniTask.DelayFrame(5);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, $"右短押し後もPlaceBlock 実際:{p.CurrentUiState}");
    p.Assert(!isNameOpen, "右短押しで名前入力が閉じる");
    checks.AssertMarker("BlueprintCopyStartMarker", new Vector3Int(0, 32, 0), "右短押し後も始点マーカーが残る");
    await p.Screenshot("02-after-cancel");

    p.Note("もう一度終点を確定し名前を入れて作成。チェストだけが写りオフセット(0,0,0)");
    await p.PressKey(Key.Q);
    await p.AimAt(new Vector3(4.5f, 32f, 4.5f));
    await p.ClickPlace();
    await p.UntilWebUiElement("modal-input", 10f);
    nameState.Confirm("chest-bp");
    await p.Until(() => datastore.Blueprints.Any(b => b.Name == "chest-bp"), 15f, "BP『chest-bp』が登録される");
    await UniTask.DelayFrame(10);
    var chestBp = datastore.Blueprints.First(b => b.Name == "chest-bp");
    p.Assert(chestBp.Blocks.Count == 1 && chestBp.Blocks[0].Offset == Vector3Int.zero, $"BPはチェスト1個・オフセット(0,0,0) 実際:{chestBp.Blocks.Count}/{(chestBp.Blocks.Count > 0 ? chestBp.Blocks[0].Offset.ToString() : "-")}");

    p.Note("側面ヒット: チェスト東面(3.0,32.5,2.5)の始点セルは(3,32,2)（旧実装は(3,33,2)）。範囲内0なら確定を拒む");
    await p.AimAt(new Vector3(3.0f, 32.5f, 2.5f));
    await p.ClickPlace();
    checks.AssertMarker("BlueprintCopyStartMarker", new Vector3Int(3, 32, 2), "東面ヒットの始点セル(3,32,2)");
    await p.AimAt(new Vector3(4.5f, 32f, 4.5f));
    await UniTask.DelayFrame(3);
    p.Assert(checks.TooltipHasParam("0"), "範囲内0ブロックの表示");
    await p.ClickPlace();
    await UniTask.DelayFrame(5);
    p.Assert(!isNameOpen, "範囲内0では名前入力が開かない");
    await p.Screenshot("03-empty-range-refused");
    await p.PressKey(Key.Escape);

    p.Note("貼り付け: Eでゴーストが1段上がる");
    await checks.SelectEntry(BlueprintCategory, $"build-menu-entry-blueprint-{chestBp.BlueprintGuid:D}");
    await p.AimAt(new Vector3(6.5f, 32f, 6.5f));
    await p.PressKey(Key.E);
    await UniTask.DelayFrame(3);
    p.Assert(heightOffset.Value == 1, $"貼り付け中のEで高さ1 実際:{heightOffset.Value}");
    p.Assert(checks.ActiveGhostPositions().Any(pos => pos == new Vector3(6f, 33f, 6f)), $"ゴーストが(6,33,6) 実際:{string.Join(";", checks.ActiveGhostPositions())}");
    await p.Screenshot("04-paste-height");
    await p.PressKey(Key.Q);

    p.Note("ドラッグ列: (6.5,32,6.5)→(10.5,32,6.5)で5個");
    await PlaytestUiOps.DragPlace(new Vector3(6.5f, 32f, 6.5f), new Vector3(10.5f, 32f, 6.5f));
    await p.Until(() => Enumerable.Range(6, 5).All(x => p.GetBlock(new Vector3Int(x, 32, 6)) != null), 20f, "x=6..10 に5個置かれる");
    await p.WaitBlockGameObject(new Vector3Int(10, 32, 6));
    await p.Screenshot("05-drag-run-pasted");

    p.Note("サムネイル: コンテナに撮影済みでビルドメニューに画像が出る");
    await p.Until(() => ClientDIContext.BlueprintThumbnailLookup.Contains(chestBp.BlueprintGuid), 20f, "サムネイル撮影完了");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.BuildMenu, 10f);
    await p.ClickWebUi(BlueprintCategory);
    await p.HoverWebUi($"build-menu-entry-blueprint-{chestBp.BlueprintGuid:D}");
    await p.Screenshot("06-thumbnail-in-build-menu");
    await p.CloseWebUiPanel();
    await p.ExitToGameScreen();
    await p.Hotbar.AssignHotbar(0, "chest-bp");
    await p.UntilWebUiElement("hotbar-slot-0", 10f);
    await UniTask.DelayFrame(5);
    await p.Screenshot("07-thumbnail-in-hotbar");

});
