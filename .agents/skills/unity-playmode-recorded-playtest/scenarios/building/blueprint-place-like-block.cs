// BPを外接箱1個のブロックとして置く: 地面/側面の置き位置・E高さ・配線ゴースト・素材不足・電線復元・列の素材上限・ビルドメニュー素材・静止表示
// Places a blueprint as one extent block: ground/side origins, E height, wire ghosts, shortage, wire restore, run material cap, menu cost, steady display
// 環境変数なしの起動は OutcropSurfacePlacement の初期化失敗で ready に届かないため、PLAYTEST_WORLD_DIRECTORY/PLAYTEST_MAP_MODE=generated/PLAYTEST_SEED を付けて実行する
// The default boot never reaches ready (OutcropSurfacePlacement init failure), so run with PLAYTEST_WORLD_DIRECTORY/PLAYTEST_MAP_MODE=generated/PLAYTEST_SEED
// 固定worldは終了時に save.json が書かれ前回の設置が残るため、PLAYTEST_WORLD_DIRECTORY は毎回未作成のパスを渡す
// The fixed world writes save.json on exit and keeps earlier placements, so pass a not-yet-created PLAYTEST_WORLD_DIRECTORY every run
using System.Linq;
using UniRx;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Common.Debug;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using Game.EnergySystem;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("blueprint-place-like-block", options, async p =>
{
    // 背景EditorではInputSystemがデバイスを無効化し注入が届かないため、焦点無視へ切り替え再有効化する
    // A background Editor disables keyboard/mouse so injection never lands; switch to ignore-focus and re-enable
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    p.Hotbar.UnlockBlueprint();
    p.Hotbar.UnlockConnectTool("電線");
    p.UnlockBlock("電柱");
    p.UnlockBlock("石窯");

    // 電柱2本と石窯を置き、電柱同士と電柱→石窯を電線でつなぐ（外接箱は x6・y5・z5）
    // Place two poles and an oven, wiring pole-pole and pole-oven (extent is x6, y5, z5)
    var poleA = new Vector3Int(2, 32, 2);
    var poleB = new Vector3Int(7, 32, 2);
    var oven = new Vector3Int(4, 32, 4);
    var sideChest = new Vector3Int(-12, 32, 22);
    p.PlaceBlockDirect("電柱", poleA, BlockDirection.North);
    p.PlaceBlockDirect("電柱", poleB, BlockDirection.North);
    p.PlaceBlockDirect("石窯", oven, BlockDirection.North);
    p.PlaceBlockDirect("木のチェスト", sideChest, BlockDirection.North);
    await p.WaitBlockGameObject(oven);
    await p.WaitBlockGameObject(sideChest);
    var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
    var wireTool = MasterHolder.ConnectToolMaster.All.First(tool => tool.Name == "電線").ConnectToolGuid;
    p.Assert(ElectricWireSystemUtil.TryConnect(poleA, poleB, playerId, wireTool, true, out var reasonAb), $"電柱同士を結線 理由:{reasonAb}");
    p.Assert(ElectricWireSystemUtil.TryConnect(poleB, oven, playerId, wireTool, true, out var reasonBo), $"電柱→石窯を結線 理由:{reasonBo}");
    p.WarpPlayer(new Vector3(5f, 33.5f, -6f));

    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var nameState = resolver.Resolve<BlueprintNameInputState>();
    var tooltip = resolver.Resolve<MouseCursorTooltipState>();
    var checks = new BlueprintCopyPasteScenarioChecks(p, tooltip);
    var probe = new BlueprintPasteGhostProbe(tooltip);
    var datastore = p.ServerService<IBlueprintDatastore>();
    var worldBlocks = p.ServerService<Game.World.Interface.DataStore.IWorldBlockDatastore>();
    var size = new Vector3Int(6, 5, 5);
    const string ToolCategory = "build-menu-category-d1000000-0000-4000-8000-000000000009";
    const string CopyToolEntry = "build-menu-entry-blueprintCopy-88dd687d-aceb-4aeb-94e8-44be1e1f5a0d";
    const string BlueprintCategory = "build-menu-category-d1000000-0000-4000-8000-000000000010";

    p.Note("1. 空気の余白を含む (0,32,0)-(10,34,8) をコピー。オフセット最小(0,0,0)・電線2本を保存");
    await checks.SelectEntry(ToolCategory, CopyToolEntry);
    await p.AimAt(new Vector3(0.5f, 32f, 0.5f));
    await p.ClickPlace();
    await p.PressKey(Key.E);
    await p.PressKey(Key.E);
    await p.AimAt(new Vector3(10.5f, 32f, 8.5f));
    await p.ClickPlace();
    await p.UntilWebUiElement("modal-input", 10f);
    nameState.Confirm("wired-bp");
    await p.Until(() => datastore.Blueprints.Any(b => b.Name == "wired-bp"), 15f, "BP『wired-bp』が登録される");
    var bp = datastore.Blueprints.First(b => b.Name == "wired-bp");
    var minOffset = bp.Blocks.Aggregate(new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue), (m, b) => Vector3Int.Min(m, b.Offset));
    p.Assert(bp.Blocks.Count == 3 && minOffset == Vector3Int.zero, $"BPは3ブロック・オフセット最小(0,0,0) 実際:{bp.Blocks.Count}/{minOffset}");
    p.Assert(bp.Wires.Count == 2, $"BPに電線2本 実際:{bp.Wires.Count}");
    await p.PressKey(Key.Escape);
    var bpEntry = $"build-menu-entry-blueprint-{bp.BlueprintGuid:D}";

    p.Note("2. 地面(-10,32,8.5)へ向ける: XZ中心がカーソルセル、底は地表 → 最小(-13,32,6)");
    p.WarpPlayer(new Vector3(-10f, 33.5f, -4f));
    await checks.SelectEntry(BlueprintCategory, bpEntry);
    await AimAndSettle(new Vector3(-10f, 32f, 8.5f));
    AssertGhostMin(new Vector3(-13, 32, 6), "地面の最小座標");
    p.Assert(probe.AreAllGhostsColored(3, true), "無料設置中のゴーストは青");
    p.Note("5. 配線ゴースト（PreviewWireLine）が2本見える");
    p.Assert(probe.ActiveWireLineCount() == 2, $"配線ゴースト2本 実際:{probe.ActiveWireLineCount()}");
    await p.Screenshot("01-ground-ghost-with-wires");

    p.Note("3. 既存チェスト(-12,32,22)の+X面へ向ける: 最小x=面(-11)・最小y=カーソル段(32)・z中心");
    p.WarpPlayer(new Vector3(-4f, 33.5f, 12f));
    await AimAndSettle(new Vector3(-11.0f, 32.5f, 22.5f));
    AssertGhostMin(new Vector3(-11, 32, 20), "側面の最小座標");
    await p.Screenshot("02-side-face-ghost");
    p.Note("4. E を2回: 最小yが34へ");
    await p.PressKey(Key.E);
    await p.PressKey(Key.E);
    await UniTask.DelayFrame(3);
    AssertGhostMin(new Vector3(-11, 34, 20), "E×2後の最小座標");
    await p.Screenshot("03-side-face-height2");
    await p.PressKey(Key.Q);
    await p.PressKey(Key.Q);

    p.Note("6. 無料設置OFF・素材なし: ゴースト赤・tooltipに不足行・クリックしても置かれない");
    DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, false);
    p.WarpPlayer(new Vector3(-10f, 33.5f, -4f));
    await AimAndSettle(new Vector3(-10f, 32f, 8.5f));
    var groundOrigin = new Vector3Int(-13, 32, 6);
    await p.Until(() => probe.AreAllGhostsColored(3, false), 10f, "素材不足でゴースト3個とも赤");
    p.Assert(0 < probe.MaterialShortageLines().Count, $"tooltipに不足行 実際:{string.Join(" | ", probe.MaterialShortageLines().Select(s => string.Join(" ", s)))}");
    await p.Screenshot("04-shortage-red");
    await p.ClickPlace();
    await p.WaitSeconds(1f);
    p.Assert(p.GetBlock(groundOrigin) == null, "素材不足ではクリックしても置かれない");

    p.Note("9. ビルドメニューのBPエントリにホバー: 詳細に必要素材（所持0/必要）が出る");
    var required = BlueprintPasteGhostProbe.RequiredItems(bp, 1);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.BuildMenu, 10f);
    await p.ClickWebUi(BlueprintCategory);
    await p.HoverWebUi(bpEntry);
    foreach (var (itemId, count) in required)
        await PlaytestWebUiOps.WaitWebUiTextContains("build-menu-detail", $"{p.CountItem(ItemName(itemId))}/{count}", 10f);
    p.Assert(0 < required.Count, $"詳細サイドバーに必要素材 {string.Join(", ", required.Select(r => $"{ItemName(r.itemId)}x{r.count}"))}");
    await p.Screenshot("05-build-menu-required-materials");

    p.Note("7. 素材を1個分持たせて貼る: 3ブロックと電線2本がサーバーに出る");
    await p.ClickWebUi(bpEntry);
    await p.WaitUiState(UIStateEnum.PlaceBlock, 10f);
    var heldBefore = required.ToDictionary(r => r.itemId, r => p.CountItem(ItemName(r.itemId)));
    foreach (var (itemId, count) in required) p.GiveItemDirect(ItemName(itemId), count);
    await AimAndSettle(new Vector3(-10f, 32f, 8.5f));
    await p.Until(() => probe.AreAllGhostsColored(3, true), 10f, "素材が揃うとゴーストが青");
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(groundOrigin) != null && p.GetBlock(groundOrigin + new Vector3Int(5, 0, 0)) != null && p.GetBlock(groundOrigin + new Vector3Int(2, 0, 2)) != null, 15f, "電柱2本と石窯が置かれる");
    var pastedA = p.GetBlock(groundOrigin).GetComponent<IElectricWireConnector>();
    var pastedB = p.GetBlock(groundOrigin + new Vector3Int(5, 0, 0)).GetComponent<IElectricWireConnector>();
    var pastedOven = p.GetBlock(groundOrigin + new Vector3Int(2, 0, 2));
    p.Assert(pastedA.ContainsWireConnection(pastedB.BlockInstanceId) && pastedA.WireConnections.Count == 1, $"貼った電柱同士が接続済み・余計な線なし 実際:{pastedA.WireConnections.Count}");
    p.Assert(pastedB.ContainsWireConnection(pastedOven.BlockInstanceId) && pastedB.WireConnections.Count == 2, $"電柱→石窯も復元 実際:{pastedB.WireConnections.Count}");
    p.Assert(required.All(r => p.CountItem(ItemName(r.itemId)) == heldBefore[r.itemId]), "1個分の素材をちょうど消費");
    await p.WaitBlockGameObject(groundOrigin + new Vector3Int(2, 0, 2));
    await p.Screenshot("06-pasted-with-wires");

    p.Note("8. ドラッグで3個並べる・素材は2個分 → 始点側の2個だけ置かれる");
    foreach (var (itemId, count) in BlueprintPasteGhostProbe.RequiredItems(bp, 2)) p.GiveItemDirect(ItemName(itemId), count);
    p.WarpPlayer(new Vector3(8f, 33.5f, 12f));
    await AimAndSettle(new Vector3(3f, 32f, 22.5f));
    await p.Until(() => probe.AreAllGhostsColored(3, true), 10f, "列の始点で2個分の素材が同期されゴーストが青");
    // 押下したまま終点へ動かし、解放前に「始点側2個が青・3個目が赤」を確かめてから離す
    // Hold the button to the end, check two blue copies then one red before releasing
    SemanticInput.MouseButtonDown(0);
    await UniTask.DelayFrame(3);
    await SemanticInput.MouseGlideTo(Camera.main.WorldToScreenPoint(new Vector3(15f, 32f, 22.5f)), 1f);
    await UniTask.DelayFrame(5);
    var runColors = probe.ActiveGhosts().OrderBy(g => g.Position.x).Select(g => BlueprintPasteGhostProbe.IsPlaceableColor(g.Color)).ToList();
    p.Assert(runColors.Count == 9 && runColors.Take(6).All(c => c) && runColors.Skip(6).All(c => !c), $"列の始点側6ゴースト青・残り3赤 実際:{string.Join(",", runColors)}");
    await p.Screenshot("07a-drag-run-before-release");
    SemanticInput.MouseButtonUp(0);
    await p.Until(() => p.GetBlock(new Vector3Int(0, 32, 20)) != null && p.GetBlock(new Vector3Int(6, 32, 20)) != null, 15f, "始点側の2個が置かれる");
    await p.WaitSeconds(1f);
    p.Assert(p.GetBlock(new Vector3Int(12, 32, 20)) == null, "3個目は素材不足で置かれない");
    await p.WaitBlockGameObject(new Vector3Int(6, 32, 20));
    await p.Screenshot("07-drag-run-two-of-three");

    p.Note("10. 無料設置ONで60フレーム静止: ゴースト3個と配線2本が毎フレーム出続ける");
    DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);
    p.WarpPlayer(new Vector3(-4f, 33.5f, 12f));
    await AimAndSettle(new Vector3(-4f, 32f, 20.5f));
    var brokenFrames = await probe.CountFramesMissingGhosts(3, 2, 60);
    p.Assert(brokenFrames == 0, $"静止60フレームでゴースト・配線が途切れない 途切れ:{brokenFrames}");
    for (var i = 0; i < 6; i++) { await UniTask.DelayFrame(10); await p.Screenshot($"08-hold-{i:00}"); }

    async UniTask AimAndSettle(Vector3 point) { await p.AimAt(point); await UniTask.DelayFrame(5); }

    void AssertGhostMin(Vector3 expected, string label)
    {
        var ghosts = probe.ActiveGhosts();
        var min = BlueprintPasteGhostProbe.MinPosition(ghosts);
        p.Assert(ghosts.Count == 3 && min == expected, $"{label} 期待:{expected} 実際:{min} 個数:{ghosts.Count}");
    }

    string ItemName(ItemId itemId) => MasterHolder.ItemMaster.GetItemMaster(itemId).Name;
});
