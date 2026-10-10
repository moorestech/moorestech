// 大きいBP（8x8チェスト64個）の貼り付けゴーストが、カーソル静止中に点滅しないことを確かめる
// Verifies that a large blueprint (8x8 = 64 chests) paste ghost does not flicker while the cursor holds still
// 比較として、通常設置の木のチェストを同じ地点で静止させたときのゴースト表示も記録する
// For comparison, it also records the normal wooden-chest ghost held still at the same point
// 続けて (a) 平地でカーソルを動かす (b) 実地形の起伏の上を動かす (c) 素材不足の青赤の境界をまたぐ、の各フレームでゴーストが欠けないことを見る
// Then checks no ghost frame is missing while (a) gliding on flat ground (b) gliding over real terrain (c) crossing the material-shortage boundary
// 2026-10-09時点、環境変数なしの起動は OutcropSurfacePlacement の初期化失敗で ready に届かないため、PLAYTEST_WORLD_DIRECTORY/PLAYTEST_MAP_MODE=generated/PLAYTEST_SEED を付けて実行する（WORLD は毎回未作成のパス）
// As of 2026-10-09 the default boot never reaches ready (OutcropSurfacePlacement init failure), so run with PLAYTEST_WORLD_DIRECTORY (a fresh path each run)/PLAYTEST_MAP_MODE=generated/PLAYTEST_SEED
using System.Linq;
using UniRx;
using Client.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.Tooltip;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Common.Debug;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Blueprint;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("blueprint-large-paste-flicker", options, async p =>
{
    // 背景EditorではInputSystemがデバイスを無効化し注入が届かないため、焦点無視へ切り替え再有効化する
    // A background Editor disables keyboard/mouse so injection never lands; switch to ignore-focus and re-enable
    var inputSettings = InputSystem.settings;
    inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    p.Hotbar.UnlockBlueprint();
    p.UnlockBlock("木のチェスト");

    // 8x8 のチェスト群（64ブロック）を置く
    // Place an 8x8 chest field (64 blocks)
    for (var x = 0; x < 8; x++)
    for (var z = 0; z < 8; z++)
        p.PlaceBlockDirect("木のチェスト", new Vector3Int(2 + x, 32, 2 + z), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(9, 32, 9));
    p.WarpPlayer(new Vector3(6f, 36f, -8f));

    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var nameState = resolver.Resolve<BlueprintNameInputState>();
    var checks = new BlueprintCopyPasteScenarioChecks(p, resolver.Resolve<MouseCursorTooltipState>());
    var probe = new BlueprintPasteGhostProbe(resolver.Resolve<MouseCursorTooltipState>());
    var isNameOpen = false;
    using var nameSubscription = nameState.OnOpenChanged.Subscribe(value => isNameOpen = value);
    var datastore = p.ServerService<IBlueprintDatastore>();

    var pasteAimPoint = new Vector3(-11.5f, 32f, 8.5f);
    const string ToolCategory = "build-menu-category-d1000000-0000-4000-8000-000000000009";
    const string CopyToolEntry = "build-menu-entry-blueprintCopy-88dd687d-aceb-4aeb-94e8-44be1e1f5a0d";
    const string BlueprintCategory = "build-menu-category-d1000000-0000-4000-8000-000000000010";

    // コピー: 手前の地面(1,32,1)を始点、奥のチェスト(9,32,9)天面を終点にして64個を1つのBPにする
    // Copy: start on the near ground (1,32,1), end on the far chest top (9,32,9) to capture all 64 chests
    // 天面のバウンディングボックスは天面より僅かに高く浅い俯角では手前へずれて当たるため、終点はセル奥寄りを狙う
    // The top bounding box sits slightly above the top face and a shallow ray lands nearer, so aim at the far side of the cell
    p.Note("BPコピー: 始点は地面(1,32,1)、終点は奥のチェスト天面(9,33,9)");
    await checks.SelectEntry(ToolCategory, CopyToolEntry);
    await p.AimAt(new Vector3(1.5f, 32f, 1.5f));
    await p.ClickPlace();
    await p.AimAt(new Vector3(9.5f, 33f, 9.95f));
    await UniTask.DelayFrame(3);
    await p.ClickPlace();
    await p.UntilWebUiElement("modal-input", 10f);
    p.Assert(isNameOpen, "名前入力が開く");
    nameState.Confirm("large-chest-bp");
    await p.Until(() => datastore.Blueprints.Any(b => b.Name == "large-chest-bp"), 15f, "BP『large-chest-bp』が登録される");
    var largeBp = datastore.Blueprints.First(b => b.Name == "large-chest-bp");
    p.Assert(largeBp.Blocks.Count == 64, $"BPはチェスト64個 実際:{largeBp.Blocks.Count}");
    await p.PressKey(Key.Escape);

    // 貼り付け: 既設チェスト群が照準レイを遮らない西側の地面へ移り、カーソルを向けて60フレーム静止する
    // Paste: move west so the placed chests cannot block the aim ray, then hold the cursor on the ground for 60 frames
    p.WarpPlayer(new Vector3(-12f, 36f, -8f));
    p.Note("貼り付け: 地面(-11.5,32,8.5)へ照準して60フレーム静止。ゴーストが毎フレーム出続けるか");
    await checks.SelectEntry(BlueprintCategory, $"build-menu-entry-blueprint-{largeBp.BlueprintGuid:D}");
    await p.AimAt(pasteAimPoint);
    await UniTask.DelayFrame(5);
    var blueprintHiddenFrames = await CountHiddenFrames(true, "blueprint");
    p.Assert(blueprintHiddenFrames == 0, $"BPゴーストは静止中60フレームすべて表示 消えたフレーム数:{blueprintHiddenFrames}");

    // 比較: 木のチェストを持ち、同じ地点で60フレーム静止する。ビルドメニューのチェスト欄はスクロール外でDSLが押せないためホットバーで持つ（選択は同じTarget差し替え）
    // Compare: hold a wooden chest at the same point for 60 frames; the menu's chest row is off-scroll for the DSL, so use the hotbar (same Target swap)
    p.Note("比較: 通常設置の木のチェストで同じ地点に60フレーム静止");
    await p.ExitToGameScreen();
    await p.Hotbar.AssignHotbar(1, "木のチェスト");
    await p.Hotbar.EnterBuildMode(1);
    await p.AimAt(pasteAimPoint);
    await UniTask.DelayFrame(5);
    var normalHiddenFrames = await CountHiddenFrames(false, "normal");
    p.Assert(normalHiddenFrames == 0, $"通常ゴーストは静止中60フレームすべて表示 消えたフレーム数:{normalHiddenFrames}");

    // 動かしながらの計測: BPを持ち直し、平地・実地形・素材不足の境界でゆっくりカーソルを動かす
    // Moving checks: re-select the blueprint and glide slowly on flat ground, real terrain and the shortage boundary
    await checks.SelectEntry(BlueprintCategory, $"build-menu-entry-blueprint-{largeBp.BlueprintGuid:D}");
    await GlideAndCountMissing("a-flat-moving", new Vector3(-20f, 32f, 8.5f), new Vector3(-4f, 32f, 8.5f));
    var (terrainStart, terrainEnd) = FindTerrainSlope();
    p.Assert(terrainStart != terrainEnd, $"平地の外に起伏区間がある {terrainStart}->{terrainEnd}");
    var terrainMid = (terrainStart + terrainEnd) / 2f;
    Physics.Raycast(new Vector3(terrainMid.x, 400f, terrainMid.z - 12f), Vector3.down, out var standHit, 800f, 1 << LayerConst.GroundLayer);
    p.WarpPlayer(new Vector3(terrainMid.x, standHit.point.y + 3f, terrainMid.z - 12f));
    await p.WaitSeconds(1.5f);
    await GlideAndCountMissing("b-terrain-moving", terrainStart, terrainEnd);

    // 60個分の素材だけ持ち、既存チェストとの重なりで必要数が 64個分↔56個分 をまたぐように往復する
    // Hold materials for 60 chests and glide so overlaps swing the requirement between 64 and 56 chests
    DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, false);
    for (var i = 0; i < 2; i++) { p.GiveItemDirect("木の棒", 60); p.GiveItemDirect("木の板", 60); }
    p.WarpPlayer(new Vector3(-3f, 36f, -8f));
    await p.WaitSeconds(1f);
    await GlideAndCountMissing("c-shortage-boundary", new Vector3(-8f, 32f, 6.5f), new Vector3(1.5f, 32f, 6.5f));
    await GlideAndCountMissing("c-shortage-boundary-back", new Vector3(1.5f, 32f, 6.5f), new Vector3(-8f, 32f, 6.5f));
    DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);

    async UniTask GlideAndCountMissing(string label, Vector3 from, Vector3 to)
    {
        // グライド中の毎フレーム描画直前に64個そろっているかを数える
        // Count, right before rendering each frame of the glide, whether all 64 ghosts are present
        p.Note($"{label}: {from}→{to} をゆっくり動かす");
        await p.AimAt(from);
        await UniTask.DelayFrame(5);
        var done = false;
        var glide = UniTask.Create(async () => { await SemanticInput.MouseGlideTo(Camera.main.WorldToScreenPoint(to), 5f); done = true; });
        var frames = 0; var missing = 0; var blueFrames = 0;
        while (!done)
        {
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            var ghosts = probe.ActiveGhosts();
            frames++;
            if (ghosts.Count != 64) missing++;
            if (ghosts.Any(g => BlueprintPasteGhostProbe.IsPlaceableColor(g.Color))) blueFrames++;
        }
        await glide;
        p.Assert(missing == 0, $"{label}: 64個そろわないフレーム {missing}/{frames}（青を含むフレーム {blueFrames}）");
        await p.Screenshot($"{label}-end");
    }

    (Vector3, Vector3) FindTerrainSlope()
    {
        // 平地の外で、16セルの間に地表が2〜10上下する一番近い区間を探す
        // Find the nearest 16-cell run outside the flat scaffold whose surface rises 2-10 cells
        var best = (Vector3.zero, Vector3.zero); var bestScore = 0f;
        for (var z = -80; z <= 80; z += 8)
        for (var x0 = -100; x0 <= 84; x0 += 8)
        {
            if (-40 < x0 && x0 < 30 && -40 < z && z < 30) continue;
            var heights = new float[17];
            var ok = true;
            for (var i = 0; i <= 16 && ok; i++)
            {
                ok = Physics.Raycast(new Vector3(x0 + i, 400f, z), Vector3.down, out var hit, 800f, 1 << LayerConst.GroundLayer);
                heights[i] = hit.point.y;
            }
            var range = ok ? heights.Max() - heights.Min() : 0f;
            var score = range - (Mathf.Abs(x0) + Mathf.Abs(z)) * 0.01f;
            if (!ok || range < 2f || 10f < range || score <= bestScore) continue;
            bestScore = score;
            best = (new Vector3(x0, heights[0], z), new Vector3(x0 + 16, heights[16], z));
        }
        return best;
    }

    // 連続60フレームでゴーストが1つも見えなかったフレーム数を数え、その後10フレームおきにスクショを6枚撮る
    // Count frames out of 60 consecutive ones with no visible ghost, then take 6 screenshots 10 frames apart
    async UniTask<int> CountHiddenFrames(bool isBlueprint, string label)
    {
        var hidden = 0;
        for (var frame = 0; frame < 60; frame++)
        {
            await UniTask.DelayFrame(1);
            if (VisibleGhostCount(isBlueprint) == 0) hidden++;
        }
        for (var i = 0; i < 6; i++)
        {
            await UniTask.DelayFrame(10);
            await p.Screenshot($"{label}-hold-{i:00}");
        }
        return hidden;
    }

    int VisibleGhostCount(bool isBlueprint)
    {
        if (isBlueprint) return checks.ActiveGhostPositions().Count;
        var root = Object.FindObjectOfType<Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController.PlacementPreviewBlockGameObjectController>(true);
        return root != null && root.IsActive ? root.transform.Cast<Transform>().Count(child => child.gameObject.activeSelf) : 0;
    }
});
