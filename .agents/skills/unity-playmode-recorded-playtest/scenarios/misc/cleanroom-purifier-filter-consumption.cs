// シナリオ: 密閉部屋に満電の清浄機を置き、クラスが付く・フィルターが出荷マスタの寿命ごとに1個減る・壁を外すとクラスが外れて減らなくなり部屋欄からクラスと台数が消える、を確かめる（密閉時の清浄機の画面は cleanroom/cleanroom-purifier-units-to-next-class.cs）
// Scenario: a fully powered purifier in a sealed room gets a class, uses one filter per lifetime in the shipped master, and once a wall is removed stops using filters with class and count gone from the room section (the sealed purifier screen is covered by cleanroom/cleanroom-purifier-units-to-next-class.cs)
using System;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Core;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Core.Master;
using Core.Update;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using Game.CleanRoom.Util;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;
const string WallName = "クリーンルームブロック";
var purifierPos = new Vector3Int(4, 33, 7); // 室内の北の壁際中央。1x2x1 で y=33,34 を占める
var breachPos = new Vector3Int(4, 33, 0); // 外す南の壁（室外の開始位置から正面に見える）
var outsidePosition = new Vector3(4.5f, 33.5f, -4f); // 南の壁の正面の室外
var gasolineGuid = new Guid("019eded2-7454-76da-8943-84c4e7a59179");
// 録画は数分かかり途中でEditorが背面に回ると注入キーが落ちる（Fで清浄機が開かなかった）ので、Run前に背景でもGameViewへ届く設定にする（scripts/prepare-editor-input.edc と同じ）
// The recording takes minutes and injected keys drop once the Editor goes to the background (F failed to open the purifier), so route input to GameView regardless of focus before Run, as scripts/prepare-editor-input.edc does
InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
SemanticInput.EnsureDevices();
InputSystem.EnableDevice(Keyboard.current);
InputSystem.EnableDevice(Mouse.current);
return PlaytestRunner.Run("cleanroom-purifier-filter-consumption", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 400f }, async p =>
{
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 15f, "GameScreenに到達");
    await p.SetupFlatGround();
    // 地形オブジェクトが足場を突き抜けて照準と画面を狂わせるので、表示と生成を止める
    // Terrain objects poke through the scaffold and skew aim and view, so stop their display and spawning
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    p.Assert(Keyboard.current.enabled && Mouse.current.enabled && InputSystem.settings.backgroundBehavior == InputSettings.BackgroundBehavior.IgnoreFocus, $"環境: キーボードとマウスが有効で背景でも入力が届く(focus={Application.isFocused})");
    p.WarpPlayer(outsidePosition);
    #region 密閉部屋と清浄機 / Sealed room and purifier
    // 床y=32・天井y=36の全面と側壁y=33〜35の周回で内部7x7x3の部屋を作る。三人称カメラが室内に収まる広さにする（ドア無し・プレイヤーはワープで出入りする）
    // Full floor (y=32), ceiling (y=36) and perimeter walls (y=33-35) make a 7x7x3 interior roomy enough for the third-person camera; no door, the player warps in
    for (var x = 0; x <= 8; x++)
    for (var z = 0; z <= 8; z++)
    {
        await p.PlaceBlockDirect(WallName, new Vector3Int(x, 32, z), BlockDirection.North);
        await p.PlaceBlockDirect(WallName, new Vector3Int(x, 36, z), BlockDirection.North);
        if (x != 0 && x != 8 && z != 0 && z != 8) continue;
        for (var y = 33; y <= 35; y++) await p.PlaceBlockDirect(WallName, new Vector3Int(x, y, z), BlockDirection.North);
    }
    var purifierBlock = await p.PlaceBlockDirect("クリーンルーム空気清浄機", purifierPos, BlockDirection.North);
    var purifier = purifierBlock.GetComponent<ICleanRoomAirFilter>();
    var filterItemId = PlaytestItemOps.ResolveItemId("クリーンルームフィルター");
    Core.Inventory.IOpenableInventory filterInventory = purifierBlock.GetComponent<IOpenableBlockInventoryComponent>();
    await p.RunOnServer("清浄機にフィルター8個を装填する", () => filterInventory.SetItem(0, filterItemId, 8));
    // 室外の発電機（1000電力）から清浄機（要求150）だけへ配線し、満電にする
    // Wire only the purifier (needs 150) to an outdoor generator (1000), so it runs at full power
    var generatorBlock = await p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-15, 32, -15), BlockDirection.North);
    var freeCost = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
    var wired = false;
    await p.RunOnServer("発電機に給油して清浄機へ配線する", () =>
    {
        generatorBlock.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(200, MasterHolder.FluidMaster.GetFluidId(gasolineGuid)), default);
        wired = ElectricWireSystemUtil.TryConnectBothSides(generatorBlock.GetComponent<IElectricWireConnector>(), purifierBlock.GetComponent<IElectricWireConnector>(), freeCost);
    });
    p.Assert(wired, "電線 発電機→清浄機");
    #endregion
    #region (1) 密閉部屋でクラスが付く / Class is assigned in the sealed room
    // 清浄機1台・空きセル145では換気約0.034/秒で、Bの昇格0.036に届かずCになる
    // One purifier over 145 free cells gives ~0.034/s, short of B promotion 0.036, so the room is C
    await p.WaitForCleanRoomClass(purifierBlock, 2, 20f, "密閉部屋がクラスC(閾値行2)になった");
    var (className, volume, airChangeRate) = ("", 0, 0d);
    await p.RunOnServer("部屋のクラス名・空きセル数・換気回数を読む", () =>
    {
        ServerContext.GetService<CleanRoomDatastore>().TryGetCleanRoom(purifierBlock, out var room);
        className = room.TryGetClassRow(out var row) ? row.ClassName : "Out";
        (volume, airChangeRate) = (room.Volume, CleanRoomPurityLogic.AirChangeRate(purifier.RemovalVolumePerSecond, room.Volume));
    });
    p.Note($"部屋: クラス={className} 空きセル={volume} 換気回数={airChangeRate:F4}/s");
    p.Assert(className != "Out", $"密閉部屋のクラスがOutでない(クラス={className})");
    p.WarpPlayer(outsidePosition);
    await p.Screenshot("01-sealed-room");
    #endregion
    #region (2) マスタの寿命で1個減る / One filter per configured lifetime
    await Refuel();
    var start = await ReadPurifier();
    var filterParam = (CleanRoomAirFilterBlockParam)MasterHolder.BlockMaster.GetBlockMaster(purifierBlock.BlockId).BlockParam;
    var lifetimeSeconds = (float)filterParam.FilterLifeSeconds;
    var first = await WaitFilterDrop(start.Count, start.Tick, lifetimeSeconds + 20f);
    var second = await WaitFilterDrop(first.Count, first.Tick, lifetimeSeconds + 20f);
    var gameSeconds = GameUpdater.TicksToSeconds((uint)(second.Tick - first.Tick));
    var realSeconds = second.RealTime - first.RealTime;
    p.Assert(first.Count == start.Count - 1 && second.Count == first.Count - 1, $"フィルターが一度に1個ずつ減る({start.Count}→{first.Count}→{second.Count})");
    p.Assert(Math.Abs(gameSeconds - lifetimeSeconds) <= 0.5 && second.Power == 1d, $"フィルター{first.Count}→{second.Count}個の間隔 ゲーム{gameSeconds:F2}秒 (マスタ{lifetimeSeconds:F2}秒) 実時間{realSeconds:F2}秒 電力{second.Power:P0}");
    #endregion
    #region (3) 壁を外すとクラスが外れフィルターが減らない / Removing a wall drops the class and stops filter use
    await Refuel();
    await p.RemoveBlock(breachPos);
    await WaitRoomGone(10f);
    var breached = await ReadPurifier();
    p.Note($"壁を外した直後: フィルター{breached.Count}個 残量{breached.Remaining:P2} 電力{breached.Power:P0} 在室={breached.Serving}");
    p.Assert(!breached.Serving, "壁を外すと清浄機が在室でなくなった");
    await p.WaitSeconds(15f);
    var after = await ReadPurifier();
    p.Note($"15秒後: フィルター{after.Count}個 残量{after.Remaining:P2} 電力{after.Power:P0} tick差{after.Tick - breached.Tick}");
    p.Assert(after.Power == 1d, $"壁を外した後も満電のまま(電力{after.Power:P0})");
    p.Assert(after.Count == breached.Count && after.Remaining == breached.Remaining, $"壁を外した後15秒でフィルターが減らず残量も進まない({breached.Count}個/{breached.Remaining:P2}→{after.Count}個/{after.Remaining:P2})");
    await OpenPurifierUi();
    await PlaytestUiElementOps.WaitUiTextContains("clean-room-headline", "", 10f);
    var breachedSection = (await PlaytestUiElementOps.FindUi("clean-room-section")).Text;
    p.Note($"部屋欄(壁を外した後): {breachedSection.Replace("\n", " | ")}");
    p.Assert(!(await PlaytestUiElementOps.FindUi("clean-room-air-filter-count")).IsFound && !breachedSection.Contains("クラス") && !breachedSection.Contains("Class"), "壁を外した後の部屋欄にクラスと清浄機の台数が出ない");
    await p.Screenshot("03-purifier-ui-breached");
    await CloseUi();
    p.WarpPlayer(outsidePosition);
    await p.WaitSeconds(1f);
    await p.Screenshot("04-breached-wall");
    #endregion
    async UniTask<(int Count, double Remaining, double Power, bool Serving, ulong Tick, float RealTime)> ReadPurifier()
    {
        // 状態とtickを同じサーバーtick末尾で読み、間隔をゲーム時間で測れるようにする
        // Read the status and tick at the same server tick end so intervals are measured in game time
        (int, double, double, bool, ulong, float) result = default;
        await PlaytestServerThreadAction.RunAsync(p.CancellationToken, "清浄機の状態を読む", () =>
        {
            var status = purifier.GetStatus();
            result = (status.FilterItemCount, status.CurrentFilterRemainingRatio, status.PowerRatio, purifier.IsServingRoom, GameUpdater.CurrentTick, 0f);
        });
        result.Item6 = Time.realtimeSinceStartup;
        return result;
    }
    async UniTask<(int Count, double Remaining, double Power, bool Serving, ulong Tick, float RealTime)> WaitFilterDrop(int fromCount, ulong lastRefuelTick, float timeoutSeconds)
    {
        var wait = p.BeginWait(timeoutSeconds, $"フィルターが{fromCount}個から1個減る");
        while (true)
        {
            var state = await ReadPurifier();
            if (state.Count < fromCount)
            {
                wait.Complete();
                p.Note($"フィルター{fromCount}→{state.Count}個 tick={state.Tick} 実時間={state.RealTime:F2}s 電力{state.Power:P0}");
                return state;
            }
            // 出荷マスタの寿命はタンク1杯より長いので計測中も給油する
            // Top up during measurement because the shipped filter outlasts one tank
            if (GameUpdater.SecondsToTicks(40) <= state.Tick - lastRefuelTick)
            {
                await Refuel();
                lastRefuelTick = state.Tick;
            }
            wait.CheckTimeout($"count={state.Count} remaining={state.Remaining:F3} power={state.Power:F2} serving={state.Serving}");
            await UniTask.Yield();
        }
    }
    async UniTask WaitRoomGone(float timeoutSeconds)
    {
        var wait = p.BeginWait(timeoutSeconds, "壁を外して部屋が消え、清浄機の在室が外れる");
        while (true)
        {
            var (roomExists, serving) = (true, true);
            await PlaytestServerThreadAction.RunAsync(p.CancellationToken, "部屋の有無と在室を読む", () => (roomExists, serving) = (ServerContext.GetService<CleanRoomDatastore>().TryGetCleanRoom(purifierBlock, out _), purifier.IsServingRoom));
            if (!roomExists && !serving) { wait.Complete(); return; }
            wait.CheckTimeout($"roomExists={roomExists} serving={serving}");
            await UniTask.Yield();
        }
    }
    async UniTask Refuel()
    {
        // タンク200で約80秒しか持たないので、計測の区切りごとに満タンへ戻す
        // The 200-unit tank lasts about 80 s, so top it up at each phase
        await p.RunOnServer("発電機を満タンにする", () => generatorBlock.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(200, MasterHolder.FluidMaster.GetFluidId(gasolineGuid)), default));
    }
    async UniTask OpenPurifierUi()
    {
        // Fの射程は2mなので清浄機の南面の1m手前に立ち、Altのカーソル照準で南面を狙ってFで開く
        // F reaches 2 m, so stand 1 m south of the purifier and open it with F by aiming at that face via the Alt cursor
        p.WarpPlayer(new Vector3(4.5f, 33.5f, 6f));
        // 壁ブロックの床は当たり判定の上面がy=32.75なので、ワープ高さ(33.5)から落ちて床の上に止まったことだけを見る
        // The wall-block floor's collider top is y=32.75, so only check the player fell from the warp height (33.5) onto the floor
        await p.Until(() => 32.6f < p.PlayerPosition.y && p.PlayerPosition.y < 33.1f, 5f, "室内の床に着地した");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(4.5f, 33.8f, 7f));
        await p.WaitSeconds(0.5f);
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await PlaytestUiElementOps.WaitUiTextContains("clean-room-section", "", 10f);
    }
    async UniTask CloseUi() { await p.PressKey(Key.Tab); await p.WaitUiState(UIStateEnum.GameScreen, 10f); }
});
