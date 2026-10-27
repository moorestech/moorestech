// 生成ワールドの斜面で、鉄の歯車ベルトコンベア・分岐機を含むBPの貼り付けを確かめる（ADR 0081 R6〜R8・#1499の元症状）
// (a) 高さオフセット0で斜面の上をゆっくり動かしても、ゴーストが1フレームも欠けない（点滅しない）
// (b) Qで1段以上下げて一部が丘に食い込む位置で貼ると、埋まるブロックだけ赤く「地形に埋まっています」が出て、貼付後は残りだけ置かれる
// 起動は PLAYTEST_WORLD_DIRECTORY(毎回未作成のパス)/PLAYTEST_MAP_MODE=generated/PLAYTEST_SEED を付ける
// Checks pasting a blueprint holding iron gear belts and a splitter on a generated slope (ADR 0081 R6-R8, the original #1499 symptom).
// (a) Gliding slowly over the slope at offset 0 never drops a ghost frame (no flicker).
// (b) Lowered with Q until part of it bites into the hill, only the buried blocks turn red with the terrain reason, and only the rest is placed.
// Boot with PLAYTEST_WORLD_DIRECTORY (fresh path)/PLAYTEST_MAP_MODE=generated/PLAYTEST_SEED.
using System.Linq;
using UniRx;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.Tooltip;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Blueprint;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("blueprint-terrain-sink-partial-paste", options, async p =>
{
    // 背景EditorではInputSystemがデバイスを無効化し注入が届かないため、焦点無視へ切り替え再有効化する
    // A background Editor disables keyboard/mouse so injection never lands; switch to ignore-focus and re-enable
    var inputSettings = InputSystem.settings;
    inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    // 生成地形は原点の足場(y=32)を砂丘で覆うことがあるため、BPの元を組む平らな台を地形より上(上面y=160)に作る
    // Generated terrain can bury the origin scaffold (y=32) under dunes, so build the source on a flat deck above the terrain (top y=160)
    var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
    deck.name = "PlaytestBlueprintSourceDeck";
    deck.transform.position = new Vector3(6f, 158f, 4f);
    deck.transform.localScale = new Vector3(24f, 4f, 20f);
    deck.AddComponent<Client.Game.InGame.BlockSystem.GroundGameObject>();
    deck.layer = Client.Common.LayerConst.GroundLayer;
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { CreateFlatGround = false, SpawnPosition = new Vector3(5f, 161.5f, -4f) });
    await p.SkipOpeningSkitIfPlaying();
    await p.Hotbar.UnlockBlueprint();
    const string Belt = "鉄の歯車ベルトコンベア";
    const string Splitter = "鉄の歯車ベルトコンベア分岐機";
    await p.UnlockBlock(Belt);
    await p.UnlockBlock(Splitter);

    // 台の上に、ベルト7個と分岐機1個の8個の列（x=2..9, z=2）を置く
    // On the deck, lay a row of 7 belts and 1 splitter (x=2..9, z=2)
    for (var x = 2; x <= 9; x++) await p.PlaceBlockDirect(x == 5 ? Splitter : Belt, new Vector3Int(x, 160, 2), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(9, 160, 2));

    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var nameState = resolver.Resolve<BlueprintNameInputState>();
    var heightOffset = resolver.Resolve<PlacementHeightOffset>();
    var tooltipState = resolver.Resolve<MouseCursorTooltipState>();
    var checks = new BlueprintCopyPasteScenarioChecks(p, tooltipState);
    var probe = new BlueprintPasteGhostProbe(tooltipState);
    var datastore = p.ServerService<IBlueprintDatastore>();
    const string ToolCategory = "build-menu-category-d1000000-0000-4000-8000-000000000009";
    const string CopyToolEntry = "build-menu-entry-blueprintCopy-88dd687d-aceb-4aeb-94e8-44be1e1f5a0d";
    const string BlueprintCategory = "build-menu-category-d1000000-0000-4000-8000-000000000010";

    // コピー: 台の面(1,160,1)→(11,160,2)の範囲で8個を1つのBPにする。終点を列の奥にするとレイが列の南面に当たり範囲が手前へずれるため、列の東隣を狙う
    // Copy: capture the 8 blocks within deck cells (1,160,1)→(11,160,2); aim east of the row since a far end would hit the row's south face and shift the range
    p.Note("BPコピー: 鉄の歯車ベルトコンベア7個＋分岐機1個の列");
    await checks.SelectEntry(ToolCategory, CopyToolEntry);
    await p.AimAt(new Vector3(1.5f, 160f, 1.5f));
    await p.ClickPlace();
    await p.AimAt(new Vector3(11.5f, 160f, 2.5f));
    await p.ClickPlace();
    await p.UntilWebUiElement("modal-input", 10f);
    nameState.Confirm("terrain-sink-bp");
    await p.Until(() => datastore.Blueprints.Any(b => b.Name == "terrain-sink-bp"), 15f, "BP『terrain-sink-bp』が登録される");
    var blueprint = datastore.Blueprints.First(b => b.Name == "terrain-sink-bp");
    p.Assert(blueprint.Blocks.Count == 8, $"BPは8ブロック 実際:{blueprint.Blocks.Count}");
    await p.PressKey(Key.Escape);

    // 足場の外で、8セルで1.3〜5上る遮蔽物の無い斜面を探し、その南に立つ
    // Find a clear 8-cell slope rising 1.3-5 outside the scaffold and stand south of it
    p.Assert(PlaytestTerrainSurvey.TryFindRisingRunAlongX(new Vector2Int(0, 40), 120, 8, 1.3f, 5f, 32, out var run), "足場の外に上り斜面がある");
    p.Note($"斜面: {run.Describe()}");
    p.WarpPlayer(new Vector3(run.StartCell.x + 4f, run.Heights[4] + 3f, run.StartCell.y - 10f));
    await p.WaitSeconds(1.5f);
    await checks.SelectEntry(BlueprintCategory, $"build-menu-entry-blueprint-{blueprint.BlueprintGuid:D}");

    // (a) オフセット0のまま斜面の手前から奥へ、斜面に沿って左右へゆっくり動かす
    // (a) At offset 0, glide slowly along the slope from one side to the other
    p.Note("(a) 高さオフセット0で斜面の上をゆっくり動かす。8個のゴーストが毎フレームそろうか");
    p.Assert(heightOffset.Value == 0, $"BP選択直後は高さ0 実際:{heightOffset.Value}");
    var left = new Vector3(run.StartCell.x + 1f, run.Heights[1], run.StartCell.y + 0.5f);
    var right = new Vector3(run.StartCell.x + 7f, run.Heights[7], run.StartCell.y + 0.5f);
    await GlideAndCountMissing("a-slope-right", left, right);
    await GlideAndCountMissing("a-slope-left", right, left);

    // (b) 斜面の真ん中へ戻し、赤と青が混ざるまでQで下げる
    // (b) Return to the slope center and lower with Q until red and blue mix
    p.Note("(b) BPを斜面の上に合わせ、一部が丘に食い込むまでQで下げる");
    var center = new Vector3(run.StartCell.x + 4f, run.Heights[4], run.StartCell.y + 0.5f);
    await p.AimAt(center);
    await UniTask.DelayFrame(10);
    for (var press = 0; press < 3 && !IsMixed(); press++)
    {
        await p.PressKey(Key.Q);
        await UniTask.DelayFrame(10);
    }
    var ghosts = probe.ActiveGhosts();
    var redCells = ghosts.Where(g => BlueprintPasteGhostProbe.IsNotPlaceableColor(g.Color)).Select(g => Vector3Int.RoundToInt(g.Position)).ToList();
    var blueCells = ghosts.Where(g => BlueprintPasteGhostProbe.IsPlaceableColor(g.Color)).Select(g => Vector3Int.RoundToInt(g.Position)).ToList();
    p.Note($"高さ{heightOffset.Value}: 赤={string.Join(";", redCells)} 青={string.Join(";", blueCells)} tooltip={TooltipText()}");
    p.Assert(heightOffset.Value <= -1, $"1段以上下げている 実際:{heightOffset.Value}");
    p.Assert(ghosts.Count == 8 && 0 < redCells.Count && 0 < blueCells.Count, $"8個のうち一部だけ赤 赤{redCells.Count}/青{blueCells.Count}");
    p.Assert(redCells.All(IsSunk), "赤いブロックはどれも地形に埋まる位置");
    p.Assert(!blueCells.Any(IsSunk), "青いブロックはどれも地形に埋まらない位置");
    p.Assert(tooltipState.GetPresentation().Lines.Any(line => line.Key.Key == "ui.tooltip.placeBlockedByTerrain"), $"「地形に埋まっています」が出る tooltip={TooltipText()}");
    await p.Screenshot("b-partial-red-ghosts");

    // 貼り付けると青だけ置かれ、赤（埋まり）は置かれない
    // Pasting places only the blue blocks and skips the red (buried) ones
    p.Note("(b) クリックして貼り付ける → 青だけ置かれ赤は置かれない");
    await p.ClickPlace();
    await p.Until(() => blueCells.All(cell => p.GetBlock(cell) != null), 15f, "青いブロックが全部置かれる");
    await p.WaitSeconds(1f);
    p.Assert(redCells.All(cell => p.GetBlock(cell) == null), "赤い（埋まる）ブロックは置かれない");
    p.Note($"置かれた: {string.Join(";", blueCells.Select(cell => $"{cell}:{p.GetBlock(cell).BlockMasterElement.Name}"))}");
    await p.ExitToGameScreen();
    await p.WaitBlockGameObject(blueCells[0]);
    await p.Screenshot("b-after-paste");

    bool IsMixed()
    {
        var current = probe.ActiveGhosts();
        return current.Any(g => BlueprintPasteGhostProbe.IsNotPlaceableColor(g.Color)) && current.Any(g => BlueprintPasteGhostProbe.IsPlaceableColor(g.Color));
    }

    bool IsSunk(Vector3Int cell) => PlacementTerrainSinkQuery.IsSunk(cell, BlockDirection.North, Vector3Int.one);

    async UniTask GlideAndCountMissing(string label, Vector3 from, Vector3 to)
    {
        // グライド中の毎フレーム描画直前に8個そろっているかを数える
        // Count, right before rendering each frame of the glide, whether all 8 ghosts are present
        await p.AimAt(from);
        await UniTask.DelayFrame(5);
        var done = false;
        var glide = UniTask.Create(async () => { await SemanticInput.MouseGlideTo(Camera.main.WorldToScreenPoint(to), 4f); done = true; });
        var frames = 0; var missing = 0; var redFrames = 0;
        while (!done)
        {
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            var current = probe.ActiveGhosts();
            frames++;
            if (current.Count != 8) missing++;
            if (current.Any(g => BlueprintPasteGhostProbe.IsNotPlaceableColor(g.Color))) redFrames++;
        }
        await glide;
        p.Assert(missing == 0, $"{label}: 8個そろわないフレーム {missing}/{frames}");
        p.Assert(redFrames == 0, $"{label}: 高さ0で赤を含むフレーム {redFrames}/{frames}");
        await p.Screenshot($"{label}-end");
    }

    string TooltipText()
    {
        var presentation = tooltipState.GetPresentation();
        return presentation.Visible ? string.Join(" / ", presentation.Lines.Select(line => line.Key.Key)) : "(hidden)";
    }
});
