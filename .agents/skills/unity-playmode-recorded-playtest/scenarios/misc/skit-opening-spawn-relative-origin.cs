// 開幕スキット(100_start_game)の背景オブジェクト/カメラがスポーン地点基準の相対座標で
// ワールドへ配置されること(ADR 0029)を、生成マップ上で実走検証する
// Verify at runtime, on a generated map, that the opening skit's environment and camera
// are placed relative to the spawn point (ADR 0029)
using Client.Playtest;
using Client.Skit.Skit;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;
using Game.Map.Interface.Json;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };

return PlaytestRunner.Run("skit-opening-spawn-relative-origin", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());

    // WebUiHost(CEF)のVite devサーバーが未接続だと全画面が白く覆われる。
    // 座標の検証(assert)には影響しないが、スクショの見栄えのため軽く待つだけに留める
    // (2026-08-23実測: 混雑worktree環境では100秒待ってもWS接続が確立しないことがあり、これはスキット自体の不具合ではない)
    // A disconnected WebUiHost (CEF) Vite dev server whitewashes the whole screen. Does not affect the
    // coordinate asserts, so this is only a short courtesy wait for screenshot readability
    // (observed 2026-08-23: under heavy concurrent-worktree load the WS connection can fail to establish
    // even after 100s; that is an environment issue, not a skit defect)
    p.Note("WebUiHost(CEF)のVite devサーバー起動待ち");
    var webUiDeadline = Time.realtimeSinceStartup + 15f;
    while (Time.realtimeSinceStartup < webUiDeadline && !(Client.WebUiHost.Boot.WebUiHost.Hub is { HasConnections: true }))
    {
        await UniTask.Delay(500);
    }
    p.Note($"WebUiHost WS接続: {(Client.WebUiHost.Boot.WebUiHost.Hub is { HasConnections: true })}");

    // 検証対象が開幕スキットの座標そのものなのでSkipOpeningSkitは呼ばない
    // The opening skit's own coordinates are under test, so SkipOpeningSkit is deliberately not called
    p.Note("開幕スキットが再生されるのを待つ");

    var skitStore = SkitPresentationStateStore.Instance;
    await p.Until(() => skitStore.GetCurrent().PresentationState.Mode != "none", 60f, "スキット開始待ち");

    // サーバーの生成マップが実際に採用したスポーン地点をground truthとして取得する
    // Fetch the spawn point the server's generated map actually adopted, as ground truth
    var mapInfoJson = p.ServerService<MapInfoJson>();
    var spawn = mapInfoJson.DefaultSpawnPointJson.Position;
    p.Note($"生成マップのスポーン地点: {spawn}");

    // 宇宙外観(100_start_1_SpaceShip)の生成をcontrolSkitBackground(Add)完了まで待つ。
    // 外観は遠景の背景演出であり原作でも基準点から約190m離れて置かれているため、存在確認のみ行う
    // Wait for the exterior 100_start_1_SpaceShip environment; it is a distant backdrop originally
    // authored ~190m from the reference point too, so only presence is asserted (not proximity)
    await p.Until(() => FindShipChild("100_start_1_SpaceShip") != null, 30f, "宇宙外観(SpaceShip)の生成待ち");
    var exteriorShip = FindShipChild("100_start_1_SpaceShip");
    p.Assert(exteriorShip != null, "宇宙外観(SpaceShip)がシーンに存在する");
    p.Note($"宇宙外観位置: {exteriorShip.position} / スポーンからの距離: {Vector3.Distance(exteriorShip.position, spawn):F2}m");
    await p.WaitSeconds(2f);
    await p.Screenshot("01-exterior-spaceship");

    // スキットカメラもスポーン相対のcameraWarpで配置されているはず
    // The skit camera should likewise be placed relative to spawn via cameraWarp
    var skitCamera = UnityEngine.Object.FindFirstObjectByType<SkitCamera>();
    p.Assert(skitCamera != null, "スキットカメラがシーンに存在する");
    var cameraPosition = skitCamera.transform.position;
    var cameraDistanceFromSpawn = Vector3.Distance(cameraPosition, spawn);
    p.Note($"スキットカメラ位置: {cameraPosition} / スポーンからの距離: {cameraDistanceFromSpawn:F2}m");
    p.Assert(cameraDistanceFromSpawn < 60f, $"スキットカメラがスポーンの近傍(60m以内)に現れる: {cameraDistanceFromSpawn:F2}m");

    // オートモードへ切り替えてAdvanceインテントを打ち続け、船内(Interior)カットへ進める
    // Switch to auto mode and keep firing advance intents to progress into the ship-interior cut
    var started = skitStore.GetCurrent();
    skitStore.TrySetAuto(started.SessionId, started.SceneRevision, true);
    await p.Until(() =>
    {
        var current = skitStore.GetCurrent();
        if (current.PresentationState.Mode != "none") skitStore.TryAdvance(current.SessionId, current.SceneRevision);
        return FindShipChild("100_start_2_SpaceShip_Interior") != null;
    }, 120f, "船内(SpaceShip_Interior)カットへの到達待ち");

    var interiorShip = FindShipChild("100_start_2_SpaceShip_Interior");
    p.Assert(interiorShip != null, "船内(SpaceShip_Interior)がシーンに存在する");
    var interiorDistanceFromSpawn = Vector3.Distance(interiorShip.position, spawn);
    p.Note($"船内(SpaceShip_Interior)位置: {interiorShip.position} / スポーンからの距離: {interiorDistanceFromSpawn:F2}m");
    // ADR 0029の受け入れ基準: 船内(スポーン脇の演出)がスポーンの近傍(数十m以内)に現れる。
    // 相対化前の絶対座標付近に固定されていたら不合格（このマップではスポーンが動いているため必ずズレる）
    // ADR 0029 acceptance: the ship-interior cut (staged right beside spawn) appears near spawn (within a few dozen meters).
    // Staying fixed at the pre-fix absolute coordinates would fail here, since this map's spawn differs from the old testbed's
    p.Assert(interiorDistanceFromSpawn < 60f, $"船内(Interior)がスポーンの近傍(60m以内)に現れる: {interiorDistanceFromSpawn:F2}m");
    await p.WaitSeconds(2f);
    await p.Screenshot("02-spaceship-interior-near-spawn");

    // スキットを完走させGameScreenへ戻す
    // Run the skit to completion, back to GameScreen
    await p.Until(() =>
    {
        var current = skitStore.GetCurrent();
        if (current.PresentationState.Mode != "none") skitStore.TryAdvance(current.SessionId, current.SceneRevision);
        return current.PresentationState.Mode == "none";
    }, 180f, "スキット完走待ち");

    await p.WaitUiState(Client.Game.InGame.UI.UIState.UIStateEnum.GameScreen, 30f);
    await p.WaitSeconds(2f);
    await p.Screenshot("03-after-skit-gamescreen");

    #region Internal

    Transform FindShipChild(string nameContains)
    {
        var skitManager = UnityEngine.Object.FindFirstObjectByType<Client.Game.Skit.SkitManager>();
        if (skitManager == null) return null;
        foreach (Transform child in skitManager.transform)
        {
            if (child.name.Contains(nameContains)) return child;
        }
        return null;
    }

    #endregion
});
