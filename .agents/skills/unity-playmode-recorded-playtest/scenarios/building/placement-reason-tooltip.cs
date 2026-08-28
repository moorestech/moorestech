// 設置不可理由・設置案内をカーソルツールチップへ集約した実装(ADR0026)の通し検証。
// プレビュー中の各条件を実プレイ経路で作り、MouseCursorTooltip.GetPresentation()の行キーを直接assertする
// End-to-end check of the placement-reason cursor tooltip (ADR0026): each condition is produced through the real
// play route, and the tooltip line keys are asserted directly from MouseCursorTooltip.GetPresentation()
using Client.Game.InGame.UI.Tooltip;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using System.Linq;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("placement-reason-tooltip", options, async p =>
{
    // 素材不足行を検証するため無料設置はオフにする
    // Free placement is off so that the material-shortage line can be produced
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { FreeBlockPlacement = false, SpawnPosition = new Vector3(0, 33.5f, -8f) });
    await p.SkipOpeningSkit();

    // 電線の自動接続は接続ツールの解放状態を見るため、電柱プレビューより前に解放しておく
    // Auto-wiring consults the connect tool's unlock state, so unlock it before any pole preview
    p.Hotbar.UnlockConnectTool("電線");

    // 地形干渉を作るための段差（上面y=32.2でグリッド面と一致しない）と、100m超の照準先になる遠方地面を用意する
    // A bump whose top (y=32.2) misses the grid plane creates terrain overlap; a far ground gives an aim point beyond 100m
    var bump = GameObject.CreatePrimitive(PrimitiveType.Cube);
    bump.name = "PlaytestTerrainBump";
    bump.transform.position = new Vector3(9.5f, 30.9f, 9.5f);
    bump.transform.localScale = new Vector3(3f, 4f, 3f);
    PlaytestSetup.MarkAsGround(bump);

    var farGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
    farGround.name = "PlaytestFarGround";
    farGround.transform.position = new Vector3(0f, 30f, 145f);
    farGround.transform.localScale = new Vector3(60f, 4f, 60f);
    PlaytestSetup.MarkAsGround(farGround);

    string Snapshot()
    {
        var presentation = MouseCursorTooltip.Instance.GetPresentation();
        if (!presentation.Visible) return "(hidden)";
        return string.Join(" / ", presentation.Lines.Select(line =>
            line.TextParams.Count == 0 ? line.TextKey : line.TextKey + "[" + string.Join(",", line.TextParams) + "]"));
    }

    bool HasKey(string key)
    {
        var presentation = MouseCursorTooltip.Instance.GetPresentation();
        return presentation.Visible && presentation.Lines.Any(line => line.TextKey == key);
    }

    bool IsOnScreen(Vector3 worldPosition)
    {
        var screenPoint = Camera.main.WorldToScreenPoint(worldPosition);
        return 0 < screenPoint.z && 0 < screenPoint.x && screenPoint.x < Screen.width && 0 < screenPoint.y && screenPoint.y < Screen.height;
    }

    // 照準→プレビュー安定待ち→スナップショットの定型。プレビューは毎フレーム再計算されるので数フレーム待つ
    // Aim, let the preview settle, then snapshot; the preview is recomputed every frame so wait a few frames
    async UniTask<string> AimAndSnapshot(Vector3 worldPosition, string label)
    {
        await p.AimAt(worldPosition);
        await UniTask.DelayFrame(10);
        var snapshot = Snapshot();
        p.Note($"[{label}] tooltip = {snapshot}");
        return snapshot;
    }

    // ---------------------------------------------------------------- 1. 素材不足（複数セルドラッグ中）
    p.Note("フェーズ1: 素材1セル分だけ持って3セルドラッグ → 「素材名 所持/必要」行を確認");
    p.UnlockBlock("鉄のパイプ");
    await p.GiveItem("鉄板", 1);
    await p.OpenBuildMenuAndSelectBlock("鉄のパイプ");

    var dragFrom = PlaytestUiOps.PlaceAimPoint("鉄のパイプ", new Vector3Int(2, 32, 4), BlockDirection.North);
    var dragTo = PlaytestUiOps.PlaceAimPoint("鉄のパイプ", new Vector3Int(2, 32, 6), BlockDirection.North);
    await p.AimAt(dragFrom);
    await UniTask.DelayFrame(10);
    p.Note($"[1セル目] tooltip = {Snapshot()}");
    p.Assert(!HasKey("ui.tooltip.placeMaterialShortage"), "1セル分の素材があるので素材不足行は出ない");

    SemanticInput.MouseButtonDown(0);
    await UniTask.DelayFrame(5);
    await p.AimAt(dragTo);
    await UniTask.DelayFrame(10);
    var dragSnapshot = Snapshot();
    p.Note($"[3セルドラッグ中] tooltip = {dragSnapshot}");
    p.Assert(HasKey("ui.tooltip.placeMaterialShortage"), "3セルドラッグ中に素材不足行が出る");
    p.Assert(dragSnapshot.Contains(",1,3"), "素材不足行が「所持1/必要3」である");
    await p.Screenshot("01-material-shortage");

    // Unity側の行だけでなくWeb UIまで描画が通っていることを、確実に表示中のこの瞬間に確認する
    // Confirm the lines reach the actual Web UI rendering, checked at this moment when the tooltip is certainly shown
    var tooltipDom = await Client.Playtest.WebUi.PlaytestDomQuery.Query("cursor-tooltip", 3f);
    p.Note($"[Web UI DOM] found={tooltipDom.Found} text={tooltipDom.Text}");
    p.Assert(tooltipDom.Found && !string.IsNullOrWhiteSpace(tooltipDom.Text), "Web UIのcursor-tooltipが実際に描画されている");

    SemanticInput.MouseButtonUp(0);
    await UniTask.DelayFrame(10);

    // ---------------------------------------------------------------- 2. 地形干渉 / 既存ブロック重複
    p.Note("フェーズ2: 地形に埋まる位置 → 「地形に埋まっています」");
    // 直前のドラッグ設置がインベントリを非同期に減らすため、give経路の反映待ちではなく直挿入で足す
    // The preceding drag placement decrements the inventory asynchronously, so top up by direct insert instead of the give route
    p.GiveItemDirect("鉄板", 20);
    await p.WaitSeconds(1f);
    var bumpSnapshot = await AimAndSnapshot(new Vector3(9.5f, 32.9f, 9.5f), "地形干渉");
    // 現状FAILする。地形干渉の判定元 GroundCollisionDetector が現行マスタの参照するブロックプレハブに1つも付いておらず、
    // ランタイムでは blockGroundOverlapList が常にfalseになる（本planより前からの既存ギャップ）
    // This currently FAILS: no block prefab referenced by the live master carries GroundCollisionDetector,
    // so blockGroundOverlapList is always false at runtime (a gap that predates this plan)
    p.Assert(HasKey("ui.tooltip.placeBlockedByTerrain"), "地形に埋まる位置で地形干渉行が出る（既知ギャップ: GroundCollisionDetector未装着）");
    await p.Screenshot("02-blocked-by-terrain");

    p.Note("フェーズ2b: 既存ブロックを踏むフットプリント → 「設置位置が埋まっています」");
    // 既存ブロックへ直接照準するとバウンディングボックス面にヒットして「上に積む」判定になるため、
    // 3x3ブロックの隅セルで既存ブロックを踏み、レイ自体は中央の空きセルの地面へ当てる
    // Aiming straight at a block hits its bounding-box face and stacks on top, so instead let a 3x3 footprint
    // cover the occupied cell at its corner while the ray itself lands on the free center cell's ground
    var occupiedCell = new Vector3Int(-6, 32, 4);
    p.PlaceBlockDirect("鉄のパイプ", occupiedCell, BlockDirection.North);
    await p.WaitBlockGameObject(occupiedCell);
    p.UnlockBlock("石窯");
    await p.GiveConstructionCost("石窯", 2);
    await p.OpenBuildMenuAndSelectBlock("石窯");
    await UniTask.DelayFrame(10);
    var occupiedSnapshot = await AimAndSnapshot(PlaytestUiOps.PlaceAimPoint("石窯", occupiedCell, BlockDirection.North), "既存ブロック重複");
    p.Assert(HasKey("ui.tooltip.placeBlockedByExistingBlock"), "既存ブロックのセルで重複行が出る");
    await p.Screenshot("03-blocked-by-existing-block");

    // ---------------------------------------------------------------- 3. 電線不足 / 電線コスト
    p.Note("フェーズ3: 電柱プレビューの電線行を確認（銅のワイヤー0 → 素材不足＋電線不足の複数行）");
    p.UnlockBlock("電柱");
    var existingPole = new Vector3Int(-10, 32, 10);
    p.PlaceBlockDirect("電柱", existingPole, BlockDirection.North);
    await p.WaitBlockGameObject(existingPole);
    await p.GiveItem("鉄のロッド", 20);
    await p.GiveItem("電子回路", 10);
    await p.OpenBuildMenuAndSelectBlock("電柱");

    var nearPoleCell = new Vector3Int(-10, 32, 4);
    var noWireSnapshot = await AimAndSnapshot(PlaytestUiOps.PlaceAimPoint("電柱", nearPoleCell, BlockDirection.North), "電線なし");
    p.Assert(HasKey("ui.tooltip.placeWireNoWireItem"), "電線素材が無いとき「電線が足りません」行が出る");
    p.Assert(HasKey("ui.tooltip.placeMaterialShortage"), "同時に素材不足行も出る（複数理由が全部並ぶ）");
    await p.Screenshot("04-wire-shortage");

    p.Note("フェーズ3b: 銅のワイヤーを付与 → 「電線 xN」の案内行");
    await p.GiveItem("銅のワイヤー", 100);
    await p.GiveItem("銅のワイヤー", 100);
    await UniTask.DelayFrame(10);
    var wireCostSnapshot = await AimAndSnapshot(PlaytestUiOps.PlaceAimPoint("電柱", nearPoleCell, BlockDirection.North), "電線コスト");
    p.Assert(HasKey("ui.tooltip.placeWireCost"), "電線が足りているとき「電線 xN」案内行が出る");
    await p.Screenshot("05-wire-cost");

    // ---------------------------------------------------------------- 4. 距離超過 / 無ヒット
    p.Note("フェーズ4: 100m超の遠方地面へ照準 → 「遠すぎます」");
    var farAimPoint = new Vector3(0f, 32f, 145f);
    if (IsOnScreen(farAimPoint))
    {
        var farSnapshot = await AimAndSnapshot(farAimPoint, "距離超過");
        p.Assert(HasKey("ui.tooltip.placeTooFar"), "100m超の照準で「遠すぎます」行が出る");
        await p.Screenshot("06-too-far");
    }
    else
    {
        p.Assert(false, "遠方地面が画面外のため距離超過を検証できなかった");
    }

    p.Note("フェーズ4b: 空を向く → ツールチップ無表示");
    SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.5f, Screen.height * 0.96f));
    await UniTask.DelayFrame(15);
    var skySnapshot = Snapshot();
    p.Note($"[空を向く] tooltip = {skySnapshot}");
    p.Assert(skySnapshot == "(hidden)", "何にも当たっていないときツールチップは出ない");
    await p.Screenshot("07-aim-sky");

    // ---------------------------------------------------------------- 5. 接続範囲外の案内
    p.Note("フェーズ5: 既存電柱から接続範囲外の遠方でプレビュー → 「接続範囲外のため配線されません」");
    p.WarpPlayer(new Vector3(0f, 33.5f, 130f));
    await UniTask.DelayFrame(30);
    var outOfRangeCell = new Vector3Int(0, 32, 140);
    var outOfRangeAim = PlaytestUiOps.PlaceAimPoint("電柱", outOfRangeCell, BlockDirection.North);
    if (IsOnScreen(outOfRangeAim))
    {
        var outOfRangeSnapshot = await AimAndSnapshot(outOfRangeAim, "接続範囲外");
        p.Assert(HasKey("ui.tooltip.placeWireOutOfRangeNotice"), "接続範囲外の案内行が出る");
        await p.Screenshot("08-wire-out-of-range");
    }
    else
    {
        p.Assert(false, "遠方セルが画面外のため接続範囲外案内を検証できなかった");
    }
    p.WarpPlayer(new Vector3(0f, 33.5f, -8f));
    await UniTask.DelayFrame(30);

    // ---------------------------------------------------------------- 6. 世界空間ラベルの撤去
    p.Note("フェーズ6: 電線ツールで電柱を結線 → 世界空間ラベル（電線コスト・拒否理由・電柱名）が残っていないことを確認");
    await p.ExitToGameScreen();
    await p.Hotbar.AssignHotbar(0, "電線");
    await p.Hotbar.EnterBuildMode(0);
    await UniTask.DelayFrame(15);
    var secondPole = new Vector3Int(-4, 32, 10);
    p.PlaceBlockDirect("電柱", secondPole, BlockDirection.North);
    await p.WaitBlockGameObject(secondPole);
    var poleAimPoint = PlaytestUiOps.PlaceAimPoint("電柱", existingPole, BlockDirection.North);
    var secondPoleAimPoint = PlaytestUiOps.PlaceAimPoint("電柱", secondPole, BlockDirection.North);
    if (IsOnScreen(poleAimPoint) && IsOnScreen(secondPoleAimPoint))
    {
        await p.AimAt(poleAimPoint);
        await UniTask.DelayFrame(10);
        p.Note($"[電線ツール・起点電柱に照準] tooltip = {Snapshot()}");
        await p.ClickPlace();
        await p.AimAt(secondPoleAimPoint);
        await UniTask.DelayFrame(10);
        p.Note($"[電線ツール・接続先へ照準] tooltip = {Snapshot()}");
        await p.Screenshot("09-wire-tool-aim");
    }
    else
    {
        p.Note("[電線ツール] 電柱が画面外のため照準できず");
    }

    var worldLabels = UnityEngine.Object.FindObjectsByType<TMPro.TextMeshPro>(FindObjectsSortMode.None)
        .Where(text => text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text))
        .Select(text => text.gameObject.name + ":" + text.text)
        .ToArray();
    p.Note($"[世界空間TMPラベル] count={worldLabels.Length} => {string.Join(" , ", worldLabels)}");
    p.Assert(!worldLabels.Any(label => label.Contains("電線 x") || label.Contains("電柱")), "ワイヤー中間点/ゴースト上に文字ラベルが残っていない");

    // ---------------------------------------------------------------- 7. 設置モードを抜ける
    p.Note("フェーズ7: 設置モードを抜ける → ツールチップが消える");
    await p.ExitToGameScreen();
    await UniTask.DelayFrame(20);
    var exitSnapshot = Snapshot();
    p.Note($"[設置モード脱出後] tooltip = {exitSnapshot}");
    p.Assert(exitSnapshot == "(hidden)", "設置モードを抜けるとツールチップが消える");
    await p.Screenshot("10-exit-place-mode");
});
