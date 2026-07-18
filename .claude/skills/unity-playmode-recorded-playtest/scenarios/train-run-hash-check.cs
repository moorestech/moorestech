// イベントpush化の統合検証: 機関車をauto-runで25秒以上走らせ、hash mismatch警告0件を確認する
// レール橋脚2本(間隔26)をUI設置→ノード直結→機関車を本番プロトコルで設置→diagram直組み+TurnOnAutoRun。
// 走行中はtick diffイベントのpush配信のみでクライアントが追従するため、mismatch 0件=push経路の実動作証明になる。
// Integration probe for the push-based event stream: run a locomotive on auto-run for 25+ seconds
// and assert zero "Hash mismatch detected" warnings. Client tracking relies solely on pushed tick
// diff events while running, so zero mismatches proves the push path works end to end.
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.UnlockState;
using MessagePack;
using UnityEngine;
using VContainer;
using TrainCarEntityObject = Client.Game.InGame.Train.View.Object.Core.TrainCarEntityObject;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("train-run-hash-check", options, async p =>
{
    // hash mismatch警告をログフックで数える（WarningはErrorLogsに載らないため）
    // Count hash mismatch warnings via a log hook (warnings never surface in ErrorLogs)
    var mismatchCount = 0;
    void CountMismatch(string condition, string stackTrace, LogType type)
    {
        if (condition.Contains("Hash mismatch detected")) mismatchCount++;
    }
    Application.logMessageReceived += CountMismatch;

    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(16f, 33.5f, 17f));
    await p.WaitSeconds(0.5f);

    // ===== レール敷設: 橋脚2本をサーバー直設置してノードを直結する =====
    // ===== Lay rails: place two piers directly server-side and connect their nodes =====
    p.Note("レール橋脚2本を直設置しノードを結線する");

    // 機関車(長さ20=20480units)の助走が取れるよう間隔60ブロックで配置する
    // Space piers 60 blocks apart so the locomotive (length 20 = 20480 units) has a long run
    var pierAPos = new Vector3Int(10, 32, 4);
    var pierBPos = new Vector3Int(10, 32, 64);

    // TrainRailは生成時create param必須のため、サーバーテストTrainTestHelperと同経路で直設置する
    // TrainRail blocks need a spawn-time create param, so place them like the TrainTestHelper server test
    void PlacePierDirect(Vector3Int pos)
    {
        var blockId = PlaytestBlockOps.ResolveBlockId("レール橋脚");
        var railVector = RailComponent.ToVector3(BlockDirection.North);
        if (railVector == Vector3.zero) railVector = Vector3.forward;
        var stateDetail = new RailBridgePierComponentStateDetail(railVector);
        var createParams = new[] { new BlockCreateParam(RailBridgePierComponentStateDetail.StateDetailKey, MessagePackSerializer.Serialize(stateDetail)) };
        var created = ServerContext.WorldBlockDatastore.TryAddBlock(blockId, pos, BlockDirection.North, createParams, out _);
        p.Assert(created, $"橋脚サーバー直設置 {pos}");
    }

    PlacePierDirect(pierAPos);
    PlacePierDirect(pierBPos);

    // クライアント側のブロック生成はPlaceBlockイベントpushのみで届く（同期検証を兼ねる）
    // Client-side block objects arrive only via pushed PlaceBlock events (doubles as a sync check)
    await p.WaitBlockGameObject(pierAPos);
    await p.WaitBlockGameObject(pierBPos);

    var railA = p.GetBlock(pierAPos).GetComponent<RailComponent>();
    var railB = p.GetBlock(pierBPos).GetComponent<RailComponent>();
    p.Assert(railA != null && railB != null, "両橋脚にRailComponentが生成されている");
    railA.FrontNode.ConnectNode(railB.FrontNode);
    railB.BackNode.ConnectNode(railA.BackNode);

    // ===== 機関車の設置: アンロック→建設コスト付与→本番プロトコル =====
    // ===== Spawn locomotive: unlock, grant cost, then use the production protocol =====
    p.Note("機関車をアンロックしレール上へ設置する");
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var clientUnlockState = resolver.Resolve<IGameUnlockStateData>();

    // 自走にはtractionForce>0の機関車が必要（貨車は0で走れない）
    // Self-propulsion needs a locomotive with tractionForce > 0 (cargo cars have zero)
    var carMaster = MasterHolder.TrainUnitMaster.Train.TrainCars.First(c => c.TractionForce > 0);
    var trainCarGuid = carMaster.TrainCarGuid;
    p.ServerService<IGameUnlockStateDataController>().UnlockTrainCar(trainCarGuid);
    await p.Until(() => clientUnlockState.TrainCarUnlockStateInfos.TryGetValue(trainCarGuid, out var info) && info.IsUnlocked, 10f, "車両アンロックのクライアント同期");

    foreach (var required in carMaster.RequiredItems)
    {
        p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(required.ItemGuid).Name, required.Count);
    }

    // A.Front→B.Front辺上に、B.Frontへ長い助走を残して配置する（先頭がlist[0]へ向かう）
    // Sit the train on the A.Front→B.Front edge with a long approach to B.Front (head moves toward list[0])
    var trainLength = TrainLengthConverter.ToRailUnits(carMaster.Length);
    var runDistance = 38 * 1024;
    var railPosition = new RailPosition(new List<IRailNode> { railB.FrontNode, railA.FrontNode }, trainLength, runDistance);
    var placeResponse = await ClientContext.VanillaApi.Response.PlaceTrainOnRail(railPosition, trainCarGuid, CancellationToken.None);
    p.Assert(placeResponse != null && placeResponse.Success, $"車両設置プロトコル成功 (failure={placeResponse?.FailureType})");

    TrainCarEntityObject SpawnedCar() => UnityEngine.Object.FindObjectsByType<TrainCarEntityObject>(FindObjectsSortMode.None).FirstOrDefault();
    await p.Until(() => SpawnedCar() != null, 15f, "TrainCarEntityObjectのクライアント出現");
    await p.Screenshot("01-train-spawned");

    // ===== 走行: 燃料投入→目的地B.Frontをdiagram登録しauto-runで単走させる =====
    // ===== Run: add fuel, register B.Front as the destination, run one-way via auto-run =====
    p.Note("燃料を投入しdiagramを直組みしてauto-run開始");

    // 燃料切れの機関車は牽引力0でmasconが強制0になるため、デバッグコマンドで燃料を積む
    // A fuel-less locomotive has zero traction and mascon is forced to zero, so add fuel via debug command
    p.SendCommand("addFuelToAllTrainCarsCommand");
    await p.WaitSeconds(1f);

    var train = p.ServerService<ITrainUnitLookupDatastore>().GetRegisteredTrains().First();
    train.trainDiagram.AddEntry(railB.FrontNode);
    train.TurnOnAutoRun();

    await p.Until(() => train.CurrentSpeed > 0.0, 15f, "列車が加速開始");
    var carBefore = SpawnedCar().transform.position;
    await p.Screenshot("02-train-running");

    // 到着（速度0へ戻る）か30秒経過まで走行させ、その間hash mismatchを監視する
    // Run until arrival (speed returns to zero) or 30 seconds, watching for hash mismatches
    p.Note("走行監視中（到着または30秒でhash検証へ）");
    var elapsed = 0f;
    while (train.CurrentSpeed > 0.0 && elapsed < 30f)
    {
        await p.WaitSeconds(1f);
        elapsed += 1f;
    }
    await p.WaitSeconds(2f);

    // ===== 検証: クライアント側追従・mismatch 0件 =====
    // ===== Verify: client entity followed and zero hash mismatches =====
    var carAfter = SpawnedCar()?.transform.position;
    var movedDistance = carAfter.HasValue ? (carAfter.Value - carBefore).magnitude : 0f;
    p.Assert(movedDistance > 5f, $"クライアント側の車両entityが走行に追従 (moved={movedDistance:F1})");
    p.Assert(mismatchCount == 0, $"hash mismatch警告が0件 (count={mismatchCount})");

    Application.logMessageReceived -= CountMismatch;
    await p.Screenshot("final");
});
