// スポイト拡張E2E検証: ミドルクリックで電線と列車車両を設置ターゲットへピックする
// 検証項目: GameScreen中の電線ピック→PlaceBlock遷移+電線接続ツール選択、PlaceBlock中の列車ピック→TrainCarPlacementTargetへの持ち替え
// Placement-pick extension E2E: middle-click eyedrops an electric wire and a train car into placement targets.
// Verifies: wire pick in GameScreen transits to PlaceBlock with the wire connect tool, and train pick in PlaceBlock swaps to TrainCarPlacementTarget.
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.UnlockState;
using Mooresmaster.Model.PlaceSystemModule;
using UnityEngine;
using VContainer;
using TrainCarEntityObject = Client.Game.InGame.Train.View.Object.Core.TrainCarEntityObject;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("placement-pick-wire-and-train-via-ui", options, async p =>
{
    await p.SetupFlatGround();

    // クライアントDIはClientDIContext経由で解決する
    // Resolve client-side DI singletons via ClientDIContext
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var placeController = resolver.Resolve<PlaceSystemStateController>();
    var clientUnlockState = resolver.Resolve<IGameUnlockStateData>();

    // 両モード共通のミドルクリックをSemanticInputで注入する
    // Inject shared middle-click input for both normal play and placement mode via SemanticInput
    async UniTask MiddleClickAsync()
    {
        SemanticInput.MouseButtonDown(2);
        await UniTask.DelayFrame(2);
        SemanticInput.MouseButtonUp(2);
        await UniTask.DelayFrame(2);
    }

    // ===== 準備: レールを敷いて貨車を出現させる =====
    // ===== Setup: lay rails and spawn the cargo car =====
    p.Note("レール橋脚をUI設置しノードを結線する");
    p.WarpPlayer(new Vector3(11f, 33.5f, 10f));
    await p.WaitSeconds(0.5f);

    // 橋脚は生成時create paramが必須のため直設置でなくUI経路で置く
    // Piers require a create param at spawn, so place them via the UI route instead of direct insertion
    var pierAPos = new Vector3Int(10, 32, 4);
    var pierBPos = new Vector3Int(10, 32, 16);
    await p.PrepareBlockForUiPlacement("レール橋脚", 2);
    await p.PlaceBlockViaUi("レール橋脚", pierAPos, BlockDirection.North);
    await p.PlaceBlockViaUi("レール橋脚", pierBPos, BlockDirection.North);
    await p.WaitBlockGameObject(pierAPos);
    await p.WaitBlockGameObject(pierBPos);
    await p.ExitToGameScreen();

    // ノードを直結（サーバーテストPlaceTrainCarOnRailProtocolTestと同一経路）
    // Connect the nodes directly (same path as the PlaceTrainCarOnRailProtocolTest server test)
    var railA = p.GetBlock(pierAPos).GetComponent<RailComponent>();
    var railB = p.GetBlock(pierBPos).GetComponent<RailComponent>();
    railA.FrontNode.ConnectNode(railB.FrontNode);
    railB.BackNode.ConnectNode(railA.BackNode);

    p.Note("貨車をアンロックしレール上へ設置する");

    // 貨車（length=7、橋脚間隔12に収まる）をサーバー側アンロック→クライアントミラー同期待ち
    // Unlock the cargo car (length 7, fits the 12-block span) server-side, then wait for the client mirror
    var carMaster = MasterHolder.TrainUnitMaster.Train.TrainCars.First(c => c.Length <= 7);
    var trainCarGuid = carMaster.TrainCarGuid;
    p.ServerService<IGameUnlockStateDataController>().UnlockTrainCar(trainCarGuid);
    await p.Until(() => clientUnlockState.TrainCarUnlockStateInfos.TryGetValue(trainCarGuid, out var info) && info.IsUnlocked, 10f, "車両アンロックのクライアント同期");

    // 建設コスト（RequiredItems）をサーバー在庫へ付与する
    // Grant the car's construction cost (RequiredItems) into the server inventory
    foreach (var required in carMaster.RequiredItems)
    {
        p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(required.ItemGuid).Name, required.Count);
    }

    // 本番プロトコルで車両を設置しentity生成を待つ
    // Place the car via the production protocol and wait for the client-side entity
    var trainLength = TrainLengthConverter.ToRailUnits(carMaster.Length);
    var railPosition = new RailPosition(new List<IRailNode> { railA.BackNode, railB.BackNode }, trainLength, 0);
    var placeResponse = await ClientContext.VanillaApi.Response.PlaceTrainOnRail(railPosition, trainCarGuid, CancellationToken.None);
    p.Assert(placeResponse != null && placeResponse.Success, $"車両設置プロトコル成功 (failure={placeResponse?.FailureType})");
    TrainCarEntityObject SpawnedCar() => UnityEngine.Object.FindObjectsByType<TrainCarEntityObject>(FindObjectsSortMode.None).FirstOrDefault();
    await p.Until(() => SpawnedCar() != null, 15f, "TrainCarEntityObjectのクライアント出現");
    await p.Screenshot("01-train-car-spawned");

    // ===== 確認1: GameScreen中の電線ピック =====
    // ===== Check 1: wire pick during GameScreen =====
    p.Note("電柱2本を設置し電線を結線する");
    p.WarpPlayer(new Vector3(0f, 33.5f, 0f));
    await p.WaitSeconds(0.5f);

    // カメラの現在向きからXZ平面上の設置座標を決め、シーン依存の向きを吸収する
    // Derive XZ placement coordinates from the current camera facing to absorb scene-dependent orientation
    var cam = Camera.main;
    var forward = cam.transform.forward;
    forward.y = 0f;
    forward = forward.normalized;
    var right = cam.transform.right;
    right.y = 0f;
    right = right.normalized;
    Vector3Int GroundPoint(float fwd, float rgt) =>
        new Vector3Int(
            Mathf.RoundToInt(cam.transform.position.x + forward.x * fwd + right.x * rgt),
            32,
            Mathf.RoundToInt(cam.transform.position.z + forward.z * fwd + right.z * rgt));

    var poleAPos = GroundPoint(5f, -3f);
    var poleBPos = GroundPoint(5f, 3f);
    p.GiveItemDirect("電線", 64);
    p.PlaceBlockDirect("電柱", poleAPos, BlockDirection.North);
    p.PlaceBlockDirect("電柱", poleBPos, BlockDirection.North);
    await p.WaitBlockGameObject(poleAPos);
    await p.WaitBlockGameObject(poleBPos);

    // 本番プロトコルで結線しワイヤービュー生成を待つ
    // Connect via the production protocol and wait for the client wire view with colliders
    Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts.ElectricWireExtendRequestSender.Connect(poleAPos, poleBPos, PlaytestItemOps.ResolveItemId("電線"));
    ElectricWireLineViewElement WireWithColliders() =>
        UnityEngine.Object.FindObjectsByType<ElectricWireLineViewElement>(FindObjectsSortMode.None)
            .FirstOrDefault(w => 0 < w.GetComponentsInChildren<Collider>(true).Length);
    await p.Until(() => WireWithColliders() != null, 15f, "電線ビューとクリック判定コライダーの生成");
    await p.Screenshot("02-wire-connected");

    p.Note("GameScreen中に電線をミドルクリックでスポイトする");
    p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, "初期状態はGameScreen");
    var wireColliders = WireWithColliders().GetComponentsInChildren<Collider>(true);
    await p.AimAt(wireColliders[wireColliders.Length / 2].bounds.center);
    await MiddleClickAsync();
    await UniTask.DelayFrame(3);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "項目1: 電線ピックでPlaceBlockへ遷移");
    var wireTarget = placeController.CurrentTarget as ConnectToolPlacementTarget;
    p.Assert(wireTarget != null, "項目1: CurrentTargetがConnectToolPlacementTargetになる");
    p.Assert(wireTarget?.PlaceMode == PlaceSystemMasterElement.PlaceModeConst.ElectricWireConnect, "項目1: PlaceModeが電線接続になる");
    await p.Screenshot("03-pick-wire");

    // ===== 確認2: PlaceBlock中の列車ピック =====
    // ===== Check 2: train car pick during PlaceBlock =====
    p.Note("PlaceBlock中に貨車をミドルクリックでスポイトする");
    var car = SpawnedCar();
    var carCollider = car.GetComponentsInChildren<Collider>(true).FirstOrDefault();
    p.Assert(carCollider != null, "車両にクリック判定コライダーが存在する");

    // 車両近くへワープし視界に収める
    // Warp near the car so the build view camera can see it
    var carCenter = carCollider.bounds.center;
    p.WarpPlayer(new Vector3(carCenter.x - 3f, 33.5f, carCenter.z - 3f));
    await p.WaitSeconds(1f);
    await p.AimAt(carCollider.bounds.center);
    await MiddleClickAsync();
    await UniTask.DelayFrame(3);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "項目2: PlaceBlockのまま");
    var carTarget = placeController.CurrentTarget as TrainCarPlacementTarget;
    p.Assert(carTarget != null, "項目2: CurrentTargetがTrainCarPlacementTargetへ持ち替わる");
    p.Assert(carTarget?.TrainCarGuid == trainCarGuid, "項目2: ピックした車両のGuidと一致");
    await p.Screenshot("04-pick-train-car");
});
