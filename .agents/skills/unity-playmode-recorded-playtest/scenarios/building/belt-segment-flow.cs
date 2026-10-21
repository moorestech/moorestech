// segment搬送のベルトを実プレイで通しで確かめる: 直線・分岐・合流・機械との受け渡し・撤去と再設置。
// クライアントは複製をイベントだけで進め、表示物を置く。列車tickが止まらない(ハッシュ照合が通り続ける)ことも見る
// End-to-end check of segment belts in real play: straight, branch, merge, machine handoff, removal and re-placement.
// The client advances its replica from events alone and places display objects; the train tick must keep advancing (hash checks keep passing)
//
// 配置(足場の上面y=32): 供給チェスト(0,3)→北へ3本→分岐器(0,7)。前(0,8..9)と左(-1,7..9→東(-1,10))が(0,10)で合流→(0,11)→受けチェストA(0,12)。右(1,7),(2,7)東→受けチェストB(3,7)
// Layout (ground top y=32): source chest (0,3) -> 3 north belts -> splitter (0,7). Front (0,8..9) and left (-1,7..9 -> east (-1,10)) merge at (0,10) -> (0,11) -> sink chest A (0,12). Right (1,7),(2,7) east -> sink chest B (3,7)
using System.Collections.Generic;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Train.Unit;
using UnityEngine;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("belt-segment-flow", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { SpawnPosition = new Vector3(4f, 34f, 0f) });
    // 生成worldの起動はスキットを再生しない設定なので、再生中のときだけ飛ばす
    // The generated-world boot disables skit playback, so skip only when one is playing
    await p.SkipOpeningSkitIfPlaying();

    p.Note("設置: 供給チェスト→直線3本→分岐器→前と左は合流して受けA、右は受けB");
    var sourcePosition = new Vector3Int(0, 32, 3);
    var sinkAPosition = new Vector3Int(0, 32, 12);
    var sinkBPosition = new Vector3Int(3, 32, 7);
    p.PlaceBlockDirect("鉄のミニコンベアチェスト", sourcePosition, BlockDirection.North);
    for (var z = 4; z <= 6; z++) p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(0, 32, z), BlockDirection.North);
    p.PlaceBlockDirect("ベルトコンベア分岐器", new Vector3Int(0, 32, 7), BlockDirection.North);
    for (var z = 8; z <= 9; z++) p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(0, 32, z), BlockDirection.North);
    for (var z = 7; z <= 9; z++) p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(-1, 32, z), BlockDirection.North);
    p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(-1, 32, 10), BlockDirection.East);
    for (var z = 10; z <= 11; z++) p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(0, 32, z), BlockDirection.North);
    p.PlaceBlockDirect("鉄のミニコンベアチェスト", sinkAPosition, BlockDirection.North);
    for (var x = 1; x <= 2; x++) p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(x, 32, 7), BlockDirection.East);
    p.PlaceBlockDirect("鉄のミニコンベアチェスト", sinkBPosition, BlockDirection.East);
    await p.WaitBlockGameObject(sinkAPosition);
    await p.WaitBlockGameObject(sinkBPosition);

    // サーバーの組とクライアントの複製が同じ本数になるまで待つ(再構築は設置したtickの末尾、途中参加の全量は接続時)
    // Wait until the server assembly and the client replica hold the same segment count (rebuild at the end of the placing tick)
    var datastore = p.ServerService<BeltTransportDatastore>();
    var trainUpdateService = p.ServerService<TrainUpdateService>();
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var cache = resolver.Resolve<BeltTransportClientCache>();
    var tickState = resolver.Resolve<TrainUnitTickState>();
    await p.Until(() => 0 < datastore.Assembly.Segments.Length && cache.Replica != null && cache.Replica.Segments.Length == datastore.Assembly.Segments.Length, 30f, "複製のsegment本数がサーバーと一致");
    p.Note($"segment本数 server={datastore.Assembly.Segments.Length} client={cache.Replica.Segments.Length}");
    await p.Screenshot("01-line-built");

    p.Note("供給チェストに鉄インゴットを入れて流し始める");
    var itemId = PlaytestItemOps.ResolveItemId("鉄インゴット");
    var source = p.GetBlock(sourcePosition).ComponentManager.GetComponent<IOpenableBlockInventoryComponent>();
    source.SetItem(0, ServerContext.ItemStackFactory.Create(itemId, 30));
    var sinkA = p.GetBlock(sinkAPosition).ComponentManager.GetComponent<IOpenableBlockInventoryComponent>();
    var sinkB = p.GetBlock(sinkBPosition).ComponentManager.GetComponent<IOpenableBlockInventoryComponent>();

    await p.Until(() => 0 < DisplayedItemCount(), 30f, "ベルト上のアイテムの表示物が現れた");
    await p.Screenshot("02-items-on-belt");

    p.Note("分岐器が前・左・右へ配り、前と左は合流して受けAへ、右は受けBへ届く");
    await p.Until(() => 6 <= CountOf(sinkA, itemId), 90f, "受けAに6個(前と左の合流)");
    await p.Until(() => 3 <= CountOf(sinkB, itemId), 90f, "受けBに3個(右の分岐)");
    await p.Screenshot("03-both-sinks-receiving");

    p.Note("クライアントの列車tickが進み続けている(ベルトのハッシュ照合で止まっていない)");
    var clientTickBefore = tickState.GetTick();
    await p.WaitSeconds(2f);
    p.Assert(clientTickBefore < tickState.GetTick(), "クライアントtickが進んでいる");
    p.Assert(TickGap() < 40, $"クライアントtickがサーバーtickに追従している(差={TickGap()})");

    p.Note("流れている途中で直線の2本めを撤去する。供給側で詰まり、受けAへの到着が止まる");
    var removedPosition = new Vector3Int(0, 32, 5);
    var segmentsBeforeRemoval = datastore.Assembly.Segments.Length;
    p.Assert(p.RemoveBlock(removedPosition), "ベルトを撤去した");
    await p.Until(() => cache.Replica.Segments.Length != segmentsBeforeRemoval && cache.Replica.Segments.Length == datastore.Assembly.Segments.Length, 30f, "撤去後に複製が組み直された");
    // 撤去直後の1本(0,4)に1個入ったら供給は詰まる。以後は供給チェストが減らない
    // The single belt left at (0,4) takes one item and then the supply stalls; the source chest stops decreasing
    await p.WaitSeconds(2f);
    var sourceAfterStall = CountOf(source, itemId);
    await p.WaitSeconds(3f);
    await p.Screenshot("04-belt-removed");
    p.Assert(CountOf(source, itemId) == sourceAfterStall, $"撤去後は供給が詰まって減らない(残={CountOf(source, itemId)})");
    p.Assert(TickGap() < 40, $"撤去後もクライアントtickが追従している(差={TickGap()})");

    p.Note("撤去した位置へ再設置し、供給を20個足す。流れが戻り、受けAが再び増える");
    p.PlaceBlockDirect("ベルトコンベア", removedPosition, BlockDirection.North);
    await p.WaitBlockGameObject(removedPosition);
    source.SetItem(0, ServerContext.ItemStackFactory.Create(itemId, CountOf(source, itemId) + 20));
    var countAAfterReplace = CountOf(sinkA, itemId);
    await p.Until(() => countAAfterReplace + 3 <= CountOf(sinkA, itemId), 90f, "再設置後に受けAが3個増えた");
    await p.Screenshot("05-belt-replaced");

    p.Note("最後まで列車tickが追従し、供給が尽きたあとベルト上の表示物が消える");
    p.Assert(TickGap() < 40, $"最後までクライアントtickが追従している(差={TickGap()})");
    await p.Until(() => CountOf(source, itemId) == 0, 120f, "供給チェストが空になった");
    await p.Until(() => DisplayedItemCount() == 0, 60f, "ベルト上の表示物が全て消えた");
    // 供給は30+20個。撤去したマスに載っていたアイテムは消滅する仕様なので、合計はそこから撤去時の載り分(高々1個)を引いた数
    // 30+20 items were supplied; items on the removed cell vanish by design, so the total is that minus what sat on the cell at removal (at most one)
    var delivered = CountOf(sinkA, itemId) + CountOf(sinkB, itemId);
    p.Assert(49 <= delivered && delivered <= 50, $"受けAとBの合計が49〜50個(A={CountOf(sinkA, itemId)}, B={CountOf(sinkB, itemId)})");
    await p.Screenshot("06-all-delivered");

    #region Internal

    int CountOf(IOpenableBlockInventoryComponent inventory, Core.Master.ItemId id)
    {
        var count = 0;
        for (var i = 0; i < inventory.GetSlotSize(); i++)
            if (inventory.GetItem(i).Id == id) count += inventory.GetItem(i).Count;
        return count;
    }

    int DisplayedItemCount()
    {
        var parent = GameObject.Find("BeltTransportItems");
        return parent == null ? 0 : parent.transform.childCount;
    }

    long TickGap()
    {
        return (long)trainUpdateService.GetCurrentTick() - tickState.GetTick();
    }

    #endregion
});
