// 新規駅3基の既定名をサーバーと初期同期で検証し、Fで開いた入力欄を録画する。
// Verify default names of three new stations on the server and initial client sync, then record their name fields opened with F.
using Client.Common;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("train-station-default-name", options, async p =>
{
    var previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
    var previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
    try
    {
        // フォーカス無しでも注入を受け取るよう、今回の実行中だけデバイスを有効化する。
        // Enable devices to receive injected input without focus only during this run.
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        SemanticInput.EnsureDevices();
        InputSystem.EnableDevice(Keyboard.current);
        InputSystem.EnableDevice(Mouse.current);
        // 実地形より上に足場を置き、画面に地面とアバターを残す。
        // Put scaffolds above the terrain while keeping ground and the avatar in the frame.
        CreateScaffold("coal", new Vector3(105f, 98f, -38f), new Vector3(60f, 4f, 65f));
        CreateScaffold("empty", new Vector3(1003.5f, 98f, 1010f), new Vector3(50f, 4f, 50f));
        await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig
        {
            CreateFlatGround = false,
            SpawnPosition = new Vector3(96.5f, 101.5f, -50f),
        });
        // テンプレートワールドではスキットが無い場合もゲーム画面へ進む。
        // Continue to the game screen even when the template world has no opening skit.
        await PlaytestSkitOps.SkipOpeningSkitIfPlaying(10f);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);

        // 半径32の走査は駅のXZ中心から鉱脈セルまで測り、英語CSVのCoalを使う。
        // The radius-32 scan measures from the station's XZ center to vein cells and uses Coal from the English CSV.
        var coalFirst = new Vector3Int(93, 100, -48);
        var empty = new Vector3Int(1000, 100, 1000);
        var coalSecond = new Vector3Int(109, 100, -48);
        p.Note("石炭鉱脈の近くに駅を設置し、Coal Stationを確認する");
        await PlaceAndShow(coalFirst, "Coal Station", "01-coal-station");
        p.Note("鉱脈も採掘地物も無い地点でStationを確認する");
        await PlaceAndShow(empty, "Station", "02-empty-station");
        p.Note("同じ石炭鉱脈の近くに2基目を設置し、Coal Station 2を確認する");
        await PlaceAndShow(coalSecond, "Coal Station 2", "03-coal-station-2");
        await p.Screenshot("final");

    }
    finally
    {
        SemanticInput.ReleaseAllKeys();
        InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
    }

    #region Internal

    void CreateScaffold(string label, Vector3 center, Vector3 size)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = $"StationDefaultNameScaffold-{label}";
        ground.transform.position = center;
        ground.transform.localScale = size;
        ground.AddComponent<GroundGameObject>();
        ground.layer = LayerConst.GroundLayer;
    }

    async UniTask PlaceAndShow(Vector3Int position, string expectedName, string screenshot)
    {
        p.PlaceBlockDirect("蒸気機関車駅", position, BlockDirection.North);
        await p.WaitSeconds(0.5f);
        await p.WaitBlockGameObject(position);
        var station = p.GetBlock(position).GetComponent<TrainStationComponent>();
        p.Assert(station.StationName == expectedName, $"サーバー駅名: {expectedName} (actual={station.StationName})");

        // UIの正本であるクライアント状態へ既定名が届いたことも確認する。
        // Also verify that the default name reached the client state used as the UI authority.
        var view = ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(position);
        await p.Until(() => view.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey)?.StationName == expectedName,
            15f, $"駅名の初期同期: {expectedName}");
        p.Assert(view.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey).StationName == expectedName,
            $"クライアント駅名: {expectedName}");

        // 高い駅屋根は画面外になるため、南端のプレイヤーに近い高さを狙う。
        // Aim near player height at the south end because the tall station roof projects off screen.
        var bounds = new Bounds();
        var firstCollider = true;
        foreach (var collider in view.GetComponentsInChildren<Collider>(true))
        {
            if (!collider.enabled || collider.isTrigger) continue;
            if (firstCollider) bounds = collider.bounds;
            else bounds.Encapsulate(collider.bounds);
            firstCollider = false;
        }
        p.Assert(!firstCollider, "駅のインタラクト用Colliderがある");
        p.WarpPlayer(new Vector3(bounds.center.x, position.y + 1.5f, bounds.min.z - 1f));
        await UniTask.DelayFrame(10);
        SemanticInput.KeyDown(Key.LeftAlt);
        await UniTask.DelayFrame(2);
        var aimPoint = new Vector3(bounds.center.x, position.y + 0.4f, bounds.min.z + 0.1f);
        await p.AimAt(aimPoint);
        p.Note($"駅照準: target={aimPoint}, screen={Camera.main.WorldToScreenPoint(aimPoint)}, size={Screen.width}x{Screen.height}");
        await p.PressInteract();
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        await p.UntilWebUiElement("train-station-name-input", 10f);
        await p.Screenshot(screenshot);
        p.Note($"Fで駅名欄を開いた: {expectedName}");
        await p.ClickWebUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    #endregion
});
