# Belt external-outcome replay Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 確定した全状態と外部操作のtick差分から、同じCoreによる搬送を再現する。

**Architecture:** Game.BeltSegmentに構成・全状態・tick差分の値型を置き、BeltSimulationGraphが実ポートまたは再現用ポートで同じ配線を構築する。BeltReplaySimulationは正の外部結果をtickごとに固定して既存Tickを一度呼び、完了後に搬入成功を順序通り適用する。搬送計算の複製、段階別の公開入口、非同期処理は追加しない。

**Tech Stack:** C#、既存Game.BeltSegment、NUnit、.NET 8 linked-source、Unity 6000.3.8f1。

## Requirements

- R1: Normal/Merge/Branchの容量・速度・RR・走行中アイテム・bufferと接続を復元し、Captureした全状態から再構築して同じ搬送を継続できる。全GUID、種類、距離、ItemPosition、buffer、RRを比較する。
- R2: segment間接続→外部出力→外部入力の順に登録し、配列内の順序を維持する。同じsnapshotを実ポートと再現用ポートへ渡すと、合流・分岐の初期優先順位も同じになる。
- R3: 1tick差分は変更速度、供給可の入力index、成功した出力index、順序付きの成功搬入を持つ。全状態の再送なしで、速度変更・供給可否固定→Core.Tick→搬入成功の順を再現する。
- R4: 前tickの供給可否・出力成功は次tickへ持ち越さない。通知がない外部候補は不可として扱い、Branchは元のCoreの順序で次の候補を試す。
- R5: Normal→Normalは既存D11/D12実装を使う。自己接続の二重前進や満杯輪の停止を再現器が変えない。buffer→Normalの同tick前進も維持する。
- R6: 搬出成功の未実行、成功搬入の拒否は例外で検出し、無音で成功扱いにしない。搬入長1〜256の契約を境界で検査する。例外後の途中状態はrollbackせず、呼び出し側が全状態から再構築する契約とする。
- R7: 空構成・外部接続0件でも構築・Tick・Capture/復元ができる。Capture結果に実行中のqueue/接続配列への可変参照を残さない。
- R8: 逐次と並列を入れ替えた2つの1000tickシナリオで毎tick一致を確認する。列挙順と周期的なCapture/Restoreを含め、アイテム保存則も確認する。
- R9: 逐次再現tickは準備済み差分を受け取ってから一時配列・List・delegateを生成しない。Captureやシリアライズは測定区間外とし、ウォームアップ後の割当を.NET診断で測定する。
- R10: この段階は確定済み構成・完全な1tick差分を入力とする共通計算ライブラリ。World構築・実機械在庫・セーブ移行・通信/購読・tick/seqの採番・GPU統合は別の実装単位として全体ゴールに残す。Q1/Q3/Q4/Q7を仮定して埋めない。

## Global Constraints

- 正本: `E:/Dropbox/seg/mock/8/Core` と [ADR0069](../../adr/0069-belt-segment-simulation.md) のD9〜D13。段階を公開せずBeltSimulation.Tick(bool)を使う。
- 作業先: `C:/Users/5080/Documents/GitHub/moorestech`、branch `codex/belt-segment-replay`。ユーザーがこの本体checkoutでの作業を明示許可済み。baseはNormal接続PRの`8d002eabf4c1e0221e67f105b1e73b746b329f3a`で、origin/master `9f22975545adb3e835d755ae0223bb24dd3b1c46`を含む。
- このCoreアセンブリはUnity/World/Game.Item/MessagePackに依存しない。ゲームpayloadの同一性とmetadataはWorld側の責務で、BeltItemのGUID/ItemIdと混同しない。
- `.cs`は各200行以下、ディレクトリ10コードファイル以下。新Replayディレクトリ4ファイル、既存Coreテストディレクトリへ2ファイル追加。partial、Func、デフォルト引数、public setter、イベント用Actionを追加しない。
- コメントは意図のある主要区間に日本語・英語の2行。単一呼出の局所helperは呼出元末尾の#region Internalへ置く。
- Unity YAML/.metaを手で作らない。Unity生成metaのみcommit。無関係dirty5pathはstage/revertしない。

## 配置と前例

| 成果物 | 配置・責務 | 前例 |
|---|---|---|
| snapshot/tickの値 | Game.BeltSegment/Replay/BeltReplayState.cs | 同CoreのBeltItemState、Game.Train/Unit/TrainSnapshots.cs |
| 配線と全状態構築 | Game.BeltSegment/Replay/BeltSimulationGraph.cs | 既存CoreのConnectTo/CaptureItems/RestoreItems、Game.Train/Unit/TrainUnitSnapshotFactory.cs |
| 外部可否の固定 | Game.BeltSegment/Replay/BeltReplayPorts.cs | IBeltSource/IBeltReceiverとCore READMEの外部結果再現 |
| 差分適用順序 | Game.BeltSegment/Replay/BeltReplaySimulation.cs | 既存BeltSimulationが段階の呼出順を所有する役割 |

計算の矢印は `snapshot→共通構築器→Core`、`完全なtick差分→再現器→Core.Tick→境界搬入`。既存機械のUpdateを抑止して在庫を引き抜く案に対し、Coreが許すTick後の供給を使う案は、機械側の同期InsertItemと実成功分の在庫更新・通知・ハッチ搬出量集計を維持できる。後者を採る（agent実装判断）。現段階では実機械をまだ配線しない。

## Task 1: 全状態から構築し、外部確定差分を再現する

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.BeltSegment/Replay/BeltReplayState.cs`
- Create: `moorestech_server/Assets/Scripts/Game.BeltSegment/Replay/BeltSimulationGraph.cs`
- Create: `moorestech_server/Assets/Scripts/Game.BeltSegment/Replay/BeltReplayPorts.cs`
- Create: `moorestech_server/Assets/Scripts/Game.BeltSegment/Replay/BeltReplaySimulation.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltSegment/BeltExternalReplayTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltSegment/BeltExternalReplayScenario.cs`
- Unity生成: 上記6csのmetaとReplayディレクトリmeta。

**Interfaces:** namespaceは `Game.BeltSegment`。既存のBeltItem、BeltItemState、BeltDirection、BeltSegmentKind、IBeltSource、IBeltReceiverを再利用する。

- Consumes: `BeltSimulation.Tick(bool)`、`BeltConveyorSegment.ConnectTo(IBeltReceiver,BeltDirection)`、`AttachInput(IBeltSource,BeltDirection)`、`SetSpeed(int)`、`TryReceive(BeltDirection,int,in BeltItem)`、`CaptureItems()`、`RestoreItems(BeltItemState[])`、`BeltBuffer.ConnectTo/TryGetItem/RestoreItem`。
- Produces: 以下の値型と、`BeltSimulationGraph(BeltReplaySnapshot,IReadOnlyList<IBeltSource>,IReadOnlyList<IBeltReceiver>)`、snapshotの接続表を所有するgraph、`BeltSimulationGraph.CaptureSnapshot()`、`BeltReplaySimulation(BeltReplaySnapshot)`、`ApplyTick(BeltReplayTick,bool)`、`CaptureSnapshot()`。
- Graphの公開読取面は `IReadOnlyList<BeltConveyorSegment> Segments`、`BeltSimulation Simulation`。接続表はprivateでclone保持し、全状態Captureで新配列へ複製する。Replayの搬入先解決用に元snapshotのInputsをReplay自身もclone保持する。
- 接続表は独立した3型で表現し、外部の有無をnullや無効IDで表す共用体を作らない。Inputs/Outputsの配列indexが、そのsnapshot内で有効な外部接続ID。

- [ ] **Step 1: 確定状態と差分の値型を実装する**

`BeltReplayState.cs`へ以下の契約を実装する。全フィールドはreadonly、引数の省略/既定値なし。配列は値の運搬であり、ctorで渡した配列を後から変更しない契約。実行状態との分離はgraphの構築/Captureで行う。

```csharp
public sealed class BeltReplaySnapshot
{
    public readonly BeltReplaySegmentState[] Segments;
    public readonly BeltReplayLink[] Links;
    public readonly BeltReplayInput[] Inputs;
    public readonly BeltReplayOutput[] Outputs;
    public BeltReplaySnapshot(BeltReplaySegmentState[] segments, BeltReplayLink[] links,
        BeltReplayInput[] inputs, BeltReplayOutput[] outputs)
    { Segments = segments; Links = links; Inputs = inputs; Outputs = outputs; }
}
public readonly struct BeltReplaySegmentState
{
    public readonly int Capacity, Speed, PriorityIndex;
    public readonly BeltSegmentKind Kind;
    public readonly BeltItemState[] Items;
    public readonly BeltItem? BufferedItem;
    private BeltReplaySegmentState(int capacity, int speed, BeltSegmentKind kind,
        int priorityIndex, BeltItemState[] items, BeltItem? bufferedItem)
    { Capacity = capacity; Speed = speed; Kind = kind; PriorityIndex = priorityIndex;
      Items = items; BufferedItem = bufferedItem; }
    public static BeltReplaySegmentState Normal(int capacity, int speed, BeltItemState[] items)
        => new BeltReplaySegmentState(capacity, speed, BeltSegmentKind.Normal, 0, items, null);
    public static BeltReplaySegmentState Merge(int speed, int priorityIndex,
        BeltItemState[] items, BeltItem? bufferedItem)
        => new BeltReplaySegmentState(1, speed, BeltSegmentKind.Merge, priorityIndex, items, bufferedItem);
    public static BeltReplaySegmentState Branch(int capacity, int speed, int priorityIndex,
        BeltItemState[] items, BeltItem? bufferedItem)
        => new BeltReplaySegmentState(capacity, speed, BeltSegmentKind.Branch, priorityIndex, items, bufferedItem);
}
public readonly struct BeltReplayLink
{
    public readonly int SourceSegmentId, TargetSegmentId;
    public readonly BeltDirection OutputDirection;
    public BeltReplayLink(int sourceSegmentId, int targetSegmentId, BeltDirection outputDirection)
    { SourceSegmentId = sourceSegmentId; TargetSegmentId = targetSegmentId; OutputDirection = outputDirection; }
}
public readonly struct BeltReplayInput
{
    public readonly int TargetSegmentId;
    public readonly BeltDirection InputDirection;
    public BeltReplayInput(int targetSegmentId, BeltDirection inputDirection)
    { TargetSegmentId = targetSegmentId; InputDirection = inputDirection; }
}
public readonly struct BeltReplayOutput
{
    public readonly int SourceSegmentId;
    public readonly BeltDirection OutputDirection;
    public BeltReplayOutput(int sourceSegmentId, BeltDirection outputDirection)
    { SourceSegmentId = sourceSegmentId; OutputDirection = outputDirection; }
}
public sealed class BeltReplayTick
{
    public readonly BeltReplaySpeedChange[] SpeedChanges;
    public readonly int[] ReadyInputs, SuccessfulOutputs;
    public readonly BeltReplayInsertion[] Insertions;
    public BeltReplayTick(BeltReplaySpeedChange[] speedChanges, int[] readyInputs,
        int[] successfulOutputs, BeltReplayInsertion[] insertions)
    { SpeedChanges = speedChanges; ReadyInputs = readyInputs;
      SuccessfulOutputs = successfulOutputs; Insertions = insertions; }
}
public readonly struct BeltReplaySpeedChange
{
    public readonly int SegmentId, Speed;
    public BeltReplaySpeedChange(int segmentId, int speed) { SegmentId = segmentId; Speed = speed; }
}
public readonly struct BeltReplayInsertion
{
    public readonly int InputId, Length;
    public readonly BeltItem Item;
    public BeltReplayInsertion(int inputId, int length, BeltItem item)
    { InputId = inputId; Length = length; Item = item; }
}
```

BufferedItemのnullはアイテム不在を表す。生成は種別ごとのfactoryへ限定し、Normalにbuffer itemを渡せる入口を作らない。Merge容量はCoreと同じ1、NormalのRRは未使用の0。種別を別のboolへ重複保持しない。BeltItem.Positionも値として維持し、ここでWorld経路から座標を推測しない。これらは保存JSONでもMessagePack封筒でもない。

- [ ] **Step 2: 共通graph構築とCaptureを実装する**

`BeltSimulationGraph`のprivate fieldsはsegment配列、Links/Inputs/Outputsのclone。コンストラクタで外部adapter数の一致を検査し、不一致はArgumentException。segment生成時は既存Core ctorへ容量/速度/種別/RRをそのまま渡す。接続の登録後にアイテムを復元し、最後にBeltSimulationを生成する。配列の列挙順を変えない。

```csharp
public BeltSimulationGraph(BeltReplaySnapshot snapshot,
    IReadOnlyList<IBeltSource> inputSources, IReadOnlyList<IBeltReceiver> outputReceivers)
{
    if (inputSources.Count != snapshot.Inputs.Length || outputReceivers.Count != snapshot.Outputs.Length)
        throw new ArgumentException("External port counts must match the snapshot.");
    links = (BeltReplayLink[])snapshot.Links.Clone();
    inputs = (BeltReplayInput[])snapshot.Inputs.Clone();
    outputs = (BeltReplayOutput[])snapshot.Outputs.Clone();
    segments = new BeltConveyorSegment[snapshot.Segments.Length];
    for (int i = 0; i < segments.Length; i++)
    {
        var state = snapshot.Segments[i];
        segments[i] = new BeltConveyorSegment(state.Capacity, state.Speed, state.Kind, state.PriorityIndex);
    }
    foreach (var link in links) Connect(link.SourceSegmentId, segments[link.TargetSegmentId], link.OutputDirection);
    for (int i = 0; i < outputs.Length; i++) Connect(outputs[i].SourceSegmentId, outputReceivers[i], outputs[i].OutputDirection);
    for (int i = 0; i < inputs.Length; i++) segments[inputs[i].TargetSegmentId].AttachInput(inputSources[i], inputs[i].InputDirection);
    for (int i = 0; i < segments.Length; i++)
    {
        var state = snapshot.Segments[i];
        segments[i].RestoreItems(state.Items);
        if (!state.BufferedItem.HasValue) continue;
        segments[i].Buffer.RestoreItem(state.BufferedItem.Value);
    }
    Simulation = new BeltSimulation(segments);
    #region Internal
    void Connect(int sourceId, IBeltReceiver target, BeltDirection direction)
    {
        var source = segments[sourceId];
        if (source.Kind == BeltSegmentKind.Normal) source.ConnectTo(target, direction);
        else source.Buffer.ConnectTo(target, direction);
    }
    #endregion
}
public BeltReplaySnapshot CaptureSnapshot()
{
    var states = new BeltReplaySegmentState[segments.Length];
    for (int i = 0; i < states.Length; i++)
    {
        var segment = segments[i];
        BeltItem? bufferedItem = segment.Buffer != null && segment.Buffer.TryGetItem(out var item) ? item : (BeltItem?)null;
        var items = segment.CaptureItems();
        states[i] = segment.Kind switch
        {
            BeltSegmentKind.Normal => BeltReplaySegmentState.Normal(segment.Capacity, segment.Speed, items),
            BeltSegmentKind.Merge => BeltReplaySegmentState.Merge(segment.Speed, segment.PriorityIndex, items, bufferedItem),
            BeltSegmentKind.Branch => BeltReplaySegmentState.Branch(segment.Capacity, segment.Speed, segment.PriorityIndex, items, bufferedItem),
            _ => throw new InvalidOperationException("Unknown segment kind.")
        };
    }
    return new BeltReplaySnapshot(states, (BeltReplayLink[])links.Clone(),
        (BeltReplayInput[])inputs.Clone(), (BeltReplayOutput[])outputs.Clone());
}
```

ID範囲・方向・配線の本数・Items間隔/容量/順序は既存Coreと同じく確定済み入力の契約。外部wireの不正値を受け入れる責務はこのAPIへ混ぜず、後続の通信decoderで検証する。現段階のテストは有効なgraphと、ここで追加した数不一致を検査する。各factoryの種別・容量とNormalのbuffer不在も確認する。図からgraphを生成するWorld側がこの契約を満たすことは後続の受入基準に残す。

- [ ] **Step 3: 再現用ポートと差分runnerを実装する**

`BeltReplayPorts`はinternal sealed。構築時だけ各外部接続に1つのSource/Receiverを割り当て、privateな具体配列sourcePorts/receiverPortsに保持する。同じ配列を共変のIReadOnlyListへ代入したreadonly fields `IReadOnlyList<IBeltSource> Sources` / `IReadOnlyList<IBeltReceiver> Receivers` としてgraphへ渡し、interface用の重複配列は作らない。Sourceはbool Ready、Receiverはbool Expected/Consumedを持つが書換えはSetReady/Prepare/Enableのメソッド経由。両型はPorts内のprivate sealed class。

```csharp
// Source implements IBeltSource:
public bool TryGetOutput(BeltDirection direction) => ready;
// Receiver implements IBeltReceiver:
public void AttachInput(IBeltSource source, BeltDirection direction) { }
public int GetOffer(BeltDirection direction) => expected && !consumed ? BeltConstants.ItemWidth : 0;
public bool TryReceive(BeltDirection direction, int length, in BeltItem item)
{
    if (!expected) return false;
    if (consumed) throw new InvalidOperationException("An external output was consumed twice in one tick.");
    consumed = true;
    return true;
}
// BeltReplayPorts owns these methods (arrays are allocated only in its constructor):
public void Prepare(BeltReplayTick tick)
{
    foreach (var source in sourcePorts) source.SetReady(false);
    foreach (var receiver in receiverPorts) receiver.Prepare();
    foreach (int id in tick.ReadyInputs) sourcePorts[id].SetReady(true);
    foreach (int id in tick.SuccessfulOutputs) receiverPorts[id].Enable();
}
public void VerifyOutputs(BeltReplayTick tick)
{
    foreach (int id in tick.SuccessfulOutputs)
        if (!receiverPorts[id].Consumed)
            throw new InvalidOperationException($"Recorded external output {id} was not reproduced.");
}
```

ReadyInputs/SuccessfulOutputsは正の集合を配列で運ぶ。重複indexは同じtrue指定として冪等。Insertionsは順序付き操作であり、集合として重複除去しない。Receiverは1つの搬送edge専用で、Coreの並列実行でも同じportを複数sourceに共有しない。

`BeltReplaySimulation`はgraph、ports、cloneしたInputsをprivate readonlyで所有する。

```csharp
public BeltReplaySimulation(BeltReplaySnapshot snapshot)
{
    ports = new BeltReplayPorts(snapshot.Inputs.Length, snapshot.Outputs.Length);
    graph = new BeltSimulationGraph(snapshot, ports.Sources, ports.Receivers);
    inputs = (BeltReplayInput[])snapshot.Inputs.Clone();
}
public BeltReplaySnapshot CaptureSnapshot() => graph.CaptureSnapshot();
public void ApplyTick(BeltReplayTick tick, bool parallel)
{
    ports.Prepare(tick);
    foreach (var change in tick.SpeedChanges) graph.Segments[change.SegmentId].SetSpeed(change.Speed);
    graph.Simulation.Tick(parallel);
    ports.VerifyOutputs(tick);
    foreach (var insertion in tick.Insertions)
    {
        if (insertion.Length <= 0 || BeltConstants.ItemWidth < insertion.Length)
            throw new ArgumentOutOfRangeException(nameof(tick), "Insertion length must be 1..256.");
        var input = inputs[insertion.InputId];
        if (!graph.Segments[input.TargetSegmentId].TryReceive(input.InputDirection, insertion.Length, insertion.Item))
            throw new InvalidOperationException($"Recorded input {insertion.InputId} was rejected.");
    }
}
```

再現不能な例外をcatchして無音で継続しない。完全なtickが揃っていることは呼出元の契約で、このrunnerはサーバーtickやseqを自分で採番しない。後続のネットワーク境界は例外をログへ出し、full snapshotの再取得と待機解除を所有する。

- [ ] **Step 4: 固定期待値と連続再現テストを実装する**

`BeltExternalReplayTest.cs`に固定値・境界・差分テスト、`BeltExternalReplayScenario.cs`に実ポートと連続運転fixtureを置く。テスト補助はinternal、publicテスト型の追加メンバーはNUnit入口のみ。scenarioの再現元は実Receiver.GetOffer=256、TryReceiveは現在のCanAcceptを見て、成功時だけSent/Itemを記録する。成功結果だけでGetOfferを再現する方式との差を同じテストで比較する。

最小例のコード（実型を使い、補助のState/EmptyTickは唯一のcallerならlocal関数にする）:

```csharp
[Test]
public void AfterTickInsertionDoesNotAdvanceUntilNextTick()
{
    var item = new BeltItem { Guid = new Guid(1, 0, 0, new byte[8]), ItemId = 7 };
    var snapshot = new BeltReplaySnapshot(
        new[] { BeltReplaySegmentState.Normal(2, 64, Array.Empty<BeltItemState>()) },
        Array.Empty<BeltReplayLink>(), new[] { new BeltReplayInput(0, BeltDirection.Back) },
        Array.Empty<BeltReplayOutput>());
    var replay = new BeltReplaySimulation(snapshot);
    replay.ApplyTick(new BeltReplayTick(Array.Empty<BeltReplaySpeedChange>(), Array.Empty<int>(),
        Array.Empty<int>(), new[] { new BeltReplayInsertion(0, 64, item) }), false);
    Assert.That(replay.CaptureSnapshot().Segments[0].Items[0].DistanceToExit, Is.EqualTo(448));
    replay.ApplyTick(new BeltReplayTick(Array.Empty<BeltReplaySpeedChange>(), Array.Empty<int>(),
        Array.Empty<int>(), Array.Empty<BeltReplayInsertion>()), false);
    Assert.That(replay.CaptureSnapshot().Segments[0].Items[0].DistanceToExit, Is.EqualTo(384));
}
```

| fixture / 操作 | 検査する実結果 |
|---|---|
| 空snapshot、外部0件、empty tickを逐次/並列 | 空のまま、Capture/再構築も成功 |
| Normal cap2 speed64空、境界でitem長64挿入 | 当tick448、次tick384、GUIDとPosition不変 |
| cap1 Normal item@0、外部output成功と同じitemの再投入(length256)→通知なしtick | 最初だけ搬出、次tickはitem@0を保持し、成功のtrueを持ち越さない |
| 空Merge cap1、外部input2つ、ready入力0+搬入0(length256) → 次tick ready入力1+搬入1(length256) | 次tickの回収でMergeが空き、予約先が入力1へ切り替わる。buffer/RRと実Core結果が一致 |
| 空Merge外部入力2つへready入力0のみで搬入なし→次tick readyなしで搬入0を成功結果として指定 | 2tick目の受入拒否をInvalidOperationExceptionで検出し、前tickのreadyが持ち越されないことを検査 |
| Branch cap1 buffer itemあり、外部出力3つ、index0拒否/index1成功 | buffer空、PriorityIndexは成功方向+1でなく開始index+1になる |
| Branchで全候補通知なし | bufferを保持しRRを進めない。後続tickに成功通知を与えると搬出してRRを進める |
| segment間接続と外部を混ぜたMerge/Branch | 接続登録の順序とCapture/再構築後の継続が一致 |
| cap4 Normal自己接続item@32 speed64 | 1tick後@992。cap2@0/@256の満杯自己接続は停止 |
| Capture後のItems/Links/Inputs/Outputs配列を変更 | graphの計算/次回Captureへ影響しない（構築時の元snapshot変更も同様） |
| 外部adapter数不一致、各種別factory | 数不一致はArgumentException。factoryは指定kind、Merge容量1、Normal buffer不在を保持 |
| 搬出成功を通知したが空のNormal / speed0のBranch | InvalidOperationException |
| 境界搬入の長0/257、満杯へ成功搬入 | 長さはArgumentOutOfRangeException、実受入拒否はInvalidOperationException |

連続fixtureは2Normals→Merge→Branch→外部出力2つ、Mergeの追加外部入力、独立Normal→外部出力、item入りNormal自己接続を含む。容量は3/2/1/4/3/4、基本速度64、速度はtickに応じて0/64/128。Mergeの3入力目は外部。各tickで実sourceのReadyを固定→実graph.Tick(serverParallel)→成功外部出力を集計→実機械の境界挿入を順番に実行し成功だけ集計→replay.ApplyTick(frame,!serverParallel)。新規GUIDは実成功した時だけ所有集合へ追加、搬出成功のGUIDを集合から削除する。

初期自己接続itemはGUID9999、distance32とする。その他の新規GUIDは1から単調増加、ItemIdは1+id%7。Readyは(tick+inputId)%3!=0、実際の供給は(tick+inputId)%4!=0、機械受入は(tick+outputId*3)%7<4。入力長はmin(128,actual.GetOffer(direction))で正数だけTryReceive、成功結果を記録する。外部inputのターゲットは0/1/2/4。Linksは0→2 Front、1→2 Right、2→3 Front、5→5 Front。Branch3の外部出力はRight/Left、Normal4の外部出力はFront。入力方向は0/1/4がBack、2がFront。毎73tickでreplicaだけCapture/再生成し、合計1000tickの全segment状態を毎tick比較する。serverParallel false/trueの2ケース。異なる登録順を使うfixtureを比較で誤魔化さず、双方同じsnapshotを共通graphへ渡す。

expectedのsourceとactualのreplayが同じ失敗を隠さないよう、表の448/384/992/満杯停止/Branch RRの固定期待値を別テストに残す。

- [ ] **Step 5: .NETとUnityで検証し、割当を診断する**

```powershell
dotnet test tools/BeltSegment/Tests/BeltSegment.Tests.csproj -c Release
& 'C:/Users/5080/AppData/Local/Programs/uloop/bin/uloop.exe' compile --project-path ./moorestech_client
& 'C:/Users/5080/AppData/Local/Programs/uloop/bin/uloop.exe' run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value 'Tests.UnitTest.Game.BeltSegment'
git diff --check
```

期待値: 既存63件と追加全件が成功、compile ErrorCount0。UI/Worldからまだ到達しないため、録画付きプレイテストやEditModeInPlayingTestをこのPRに新設しない。全体の実ゲーム検証はWorld切替で行う。

割当診断は.NETの外部scratchプロジェクトからproductionソースをリンクする。cap1 Normal speed128/item@0、外部input1/output1で、各tickのoutput成功1件と同じGUIDの境界再投入(length256)を含む準備済みframeを使う。1000warmup後10000tick ApplyTick(false)を測り、GC.GetAllocatedBytesForCurrentThreadの差0を確認する。全状態CaptureとGUID検査は測定の後、Stopwatch生成も測定区間外。これは差分適用+Coreの割当であり通信/GPUの性能とはしない。Timingは参考値として残し、性能改善の主張をしない。

- [ ] **Step 6: 対象ファイルとUnity生成metadataをcommitする**

```powershell
git add -- moorestech_server/Assets/Scripts/Game.BeltSegment/Replay moorestech_server/Assets/Scripts/Game.BeltSegment/Replay.meta moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltSegment/BeltExternalReplayTest.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltSegment/BeltExternalReplayTest.cs.meta moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltSegment/BeltExternalReplayScenario.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltSegment/BeltExternalReplayScenario.cs.meta
git commit -m 'Task 1: segmentの全状態復元と外部確定差分の再現を追加'
```

## Task 2: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること

- [ ] base8d002eabfに対する全差分をレビューする。R1〜R10、ADR0069、外部入力が完全な1tick分揃っている契約、未完了のWorld/通信/GPUをcontextへ渡す。
- [ ] 指摘を反映して対応するCoreテスト/compileを行い、sourceのRefixを完了する。実ゲーム到達性を未検証のまま成功扱いにしない。

## Task 3: セッション終了可能状態にすること

- [ ] pr-createで`codex/belt-segment-normal-links`をbaseとするstacked Draft PRを作成し、全対象変更をcommit/push、mergeabilityを確認する。コンフリクトはbaseを取り込んで解消し、C#変更があれば再compileする。full migrationを完了扱いにせず次のWorld/通信統合へ進む。

## 既存操作の死活表

| 操作 | 変更後 | 根拠 |
|---|---|---|
| 既存CoreのTick/搬送/復元 | 維持 | 既存型・アルゴリズムへ委譲 |
| 既存ゲーム機械の在庫更新 | 本PRでは既存経路 | 実World配線は次の統合単位 |
| trainのtick/seqと同期 | 維持 | 既存trainファイルに変更なし |
| ベルト全移植の完成 | 未完了 | World・セーブ・通信3点セット・GPU・実プレイが残る |

## 判断記録（ADR）

- [ADR0069](../../adr/0069-belt-segment-simulation.md)「外部操作の再現を独立して実装する」に従う。外部供給をTick後に置くのはCore契約と既存機械の同期在庫所有に基づくagent実装判断。
- 共通構築器は配線とstateの復元に限定する。blockからの分類・速度混在・接続選択を追加しない。Q1/Q3/Q4/Q7は未決のまま。
- 接続登録順を3配列の順で固定し、それを同じsnapshotで実側と再現側へ渡す。間接推測やCore private reflectionを使わない。
- 1tickの外部出力はedgeごとに最大1回というCoreの現行処理に対応する。1つのmachineを複数edgeの共有Receiver状態として再現しない。
- snapshot/tickの値は計算ライブラリの契約であり、保存形式/ネットワーク封筒を兼用しない。wireの完全性・順序と再同期は通信の責務として後続へ残す。
- 例外で確定差分との不一致を可視化し、Coreの例外契約と同じくrollbackをしない。後続client境界のログとfull snapshot再取得までが全体完成の条件。
- Replayは所有するCoreを進める計算オブジェクトであり、グローバルDataStoreではない。公開CaptureとApplyは既存CoreのCapture/Restore/Tickと同じ役割。将来用途だけのLookup/Mutation interfaceを追加しない。
- 現段階は複数asmdefから使う共通計算入口。独立Coreのpublic構築/再現契約として公開し、テスト専用の状態getterを追加しない。実World利用の未完了を隠さない。
- 入力validityは既存Coreと同じ契約を引き継ぐ。構築数不一致と、新しい境界搬入長/再現不能の検査は本PRのテストで閉じる。種別factoryでNormalのbufferを表現できなくする。wire受信を開始する前にdecoderの検証を別計画へ含める。
- 構造レビューの5-A強指摘を受け、segment stateのctorをprivateとし種別factoryを採用した（agent実装判断）。5-C弱指摘は既存IBeltReceiverの通常拒否falseと不可能な二重実行の例外を既に区別しているため維持する。新しい結果型へCore契約を変更しない。
- unityプレイ録画テスト/EditModeInPlayingTestを省く理由: 今回のライブラリへゲームWorldがまだ接続されず、ゲーム操作から到達しない。Unity EditModeで実コードを検証し、到達可能にする後続World切替で実プレイを検証する。

## 事前レビュー

- 本体自己点検: R1/R2/R7はStep1/2/4、R3/R4/R6はStep3/4、R5/R8はStep4、R9はStep5、R10は境界とADRへ対応。公開Coreのシグネチャ・linked-source再帰収集を実ソースで照合済み。
- 最小構成: 空graph/外部0、単一Normal/満杯自己接続、Branch全拒否→次tick成功、ready無しMerge→拒否をテスト表へ含めた。保留の解除は次の完全なtickで成功情報を与える既存Core処理であり、外部ACKや永続ロックを新設しない。
- 接続順はIDから推測せずsnapshotの配列順で統一。実側/再現側とCapture/再生成を同じ構築器で検証し、固定期待値448/384/992/RRも独立に置く。
- 構造レビュー: 強1（種別ctorへのnull）をfactoryで解消、弱1（TryReceiveの拒否と不整合）はfalse/例外の既存区別を維持。第三バケツ0。
- user-simulator review: 新規Critical/Warning/要裁定0。外部GetOffer=256+TryReceive拒否と、再現側GetOffer=0のCore状態への等価性、fixtureの方向/接続数を斥候で照合済み。Fable未提供のためgpt-6-astra high、斥候gpt-6-sol highで代替実行。World/通信/GPUの実行検証と未回答Qの予測裁定は行っていない。ユーザーによる本planの評価は未受領であり、的中/承認扱いにしない。
