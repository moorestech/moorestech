# Belt Normal Connections Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モードは同スキルの規模ゲートに従う。接続branchの確定HEAD `5ab92612ebb483f9c56cb25676ebba2d4ee9fef4` をbaseとする `codex/belt-segment-normal-links` で実行する。D12はADR0069と裁定台帳へ記録済み。自己点検・構造レビュー・user-simulatorレビュー済み。

**Goal:** Normal→Normal接続と自己接続を、前tickの実空きに基づいて決定論的に更新する。

**Architecture:** `BeltSimulation` がNormal接続用の読み取り状態をtick開始時に固定する。通常segmentは搬出の成否と搬送内容を接続ごとの領域へ記録して自分の走行列を一度だけ更新し、全Normal更新完了後に受け取り側へ挿入を確定する。

**Tech Stack:** C#、Unity参照なしのGame.BeltSegment、既存.NET8 linked-source NUnit/Unityテスト。

## Requirements

- D11: Normal→Normalを許可し、該当接続を抽出して各tick開始時に前tickの参照状態を固定する。Normalの自己接続も同じ仕組みで扱う。
- D12: 「満杯なら止まる：前tickの実際の空きだけで受け入れを判定する」。同tickの搬出で空く予定量を加算しない。満杯の輪は停止する。
- Normal→Normalの同じ状態と速度は、列挙順と逐次/並列の違いにかかわらず同じGUID・個数・距離になる。新しく渡ったアイテムを受け取り側で同tickに再前進させない。
- 実空きはtick開始時に固定する。受け取り側がそのtickに前進して空きが増えても、搬入判断には先取りしない。次tickには新しい空きを参照する。
- Normal→Merge/外部機械とbuffer→Normalの既存段階は維持する。後者は段階3で搬入したものが段階4で進むという正本規則を維持する。
- Normalの搬入元最大1つ、全更新対象を重複なくSimulationへ渡す、配線変更後にSimulationを再構築する既存の呼び出し契約を維持する。複数送り元が同じNormalへ同時に書く不正構成を、この変更で許可しない。
- 公開更新入口は `BeltSimulation.Tick(bool parallel)` のまま。段階ごとの更新入口は公開しない。Unity/World/セーブ/ネットワークをCoreへ参照させない。
- D11の輪の決定論的な切れ目と保存復元はWorld→segment構築側の責務。このCore変更のテストは完成した自己接続を与える。Worldで輪の配置が完成したと主張しない。
- 新しいtick処理の逐次実行にtickごとの配列・List・delegate確保を加えない。読み取りスナップショットは対象ごとに入口の実空きなど必要最小限を保持し、全アイテム配列を毎tick複製しない。

## Global Constraints

- ユーザーが指定した作業先は `C:/Users/5080/Documents/GitHub/moorestech`。接続PRと分け、この変更はNormal接続用branchで扱う。
- partial/Func/デフォルト引数禁止、日英コメント、新C#200行以下/新dir10code以下、Unity生成metaのみ、Library削除禁止。
- 正本Coreの現行Normal搬出先制限はD11の明示的追加仕様で更新する。D9機械の搬入出、D10bufferのみなら歯車待機負荷は、この変更で別の規則へ変更しない。
- Q1速度混在/Q3フィルター分岐器/Q4旧セーブ超過/Q7縦向きはこの内部接続機構の入力として決める必要がなく、仮決定しない。

## 配置と前例

| 対象 | 責務と前例 |
|---|---|
| `moorestech_server/Assets/Scripts/Game.BeltSegment/Simulation/BeltSimulation.cs` | 既存の段階間バリアを持つ唯一のrunner。開始時snapshotとNormal段階のcommitバリアをここで駆動 |
| `moorestech_server/Assets/Scripts/Game.BeltSegment/Simulation/BeltConveyorSegment.cs` | 自分の走行列の前進・搬出成否を担当。Normal宛ての直接書き込みを今回の記録経路へ委譲し、queueはここが所有し続ける |
| `moorestech_server/Assets/Scripts/Game.BeltSegment/Simulation/BeltNormalTransfer.cs` | 1つのNormal→Normal edgeの固定offer、搬入方向、成功時のitem/length、段階末尾commitを所有するinternal型 |
| `moorestech_server/Assets/Scripts/Game.BeltSegment/Simulation/BeltNormalStep.cs` | 1つのNormalと任意のNormalTransferを結ぶinternal readonly値。Parallel.ForEachの更新単位。indexを揃えた2配列で暗黙の対応関係を持たない |
| `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltSegment/BeltNormalConnectionTest.cs` | 新接続の固定期待値、順序非依存、snapshot更新、自己接続/満杯拒否を検証 |
| `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltSegment/BeltNormalConnectionReplayTest.cs` | 多tick逐次/並列一致、既存Merge/Branchとの接続、Capture/Restore後の一致を検証 |
| `tools/BeltSegment/README.md` | Normal接続の段階とD12、Worldの切れ目生成は別責務であることを記載 |

ゲーム→共有CoreのTick→段階内の各列更新→結果をクライアント/GPUへ、という既存の方向のまま。新型は搬送Core内の書き込み所有とバリアを担当する。配置/保存/描画用の一般サービスは追加しない。

## Task 1: 固定したNormal接続と段階末尾commit

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.BeltSegment/Simulation/BeltSimulation.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.BeltSegment/Simulation/BeltConveyorSegment.cs`
- Create: `moorestech_server/Assets/Scripts/Game.BeltSegment/Simulation/BeltNormalTransfer.cs`
- Create: `moorestech_server/Assets/Scripts/Game.BeltSegment/Simulation/BeltNormalStep.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltSegment/BeltNormalConnectionTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltSegment/BeltNormalConnectionReplayTest.cs`
- Modify: `tools/BeltSegment/README.md`

**Interfaces:** 公開APIは変更しない。具体的な内部シグネチャは以下の責務に閉じる。

```csharp
internal sealed class BeltNormalTransfer
{
    internal BeltNormalTransfer(BeltConveyorSegment target, BeltDirection inputDirection);
    internal void CaptureAvailableSpace();
    internal bool TryStage(int length, in BeltItem item);
    internal void Commit();
}
internal readonly struct BeltNormalStep
{
    internal BeltNormalStep(BeltConveyorSegment segment, BeltNormalTransfer transfer);
    internal void Advance();
}
// null transfer means the existing Merge/machine/direct path.
internal void BeltConveyorSegment.AdvanceAndTransfer(BeltNormalTransfer transfer);
```

1. Simulation構築時、NormalのOutputがNormalであるedgeだけにtransferを作る。方向は送り元の搬出方向の反対。自己接続も区別しない。公開のOutputは実接続先を保持し、proxyで置き換えない。必要な方向参照はinternalに閉じる。
2. `Tick`の先頭、段階0より前に全transferがtarget.GetOfferで入口の実空きを保存する。前tickのpending成功は直前のCommitで解消済みである。ここで全件完了する。
3. 正本段階0～3を現在と同じ順序で実行する。段階4では各NormalStepが自身のsegmentを更新する。Normal宛てはlengthと固定offerからTryStageで成否を決め、成功時だけBeltItem/lengthを保存する。targetのqueueはここでは変更しない。他の搬出先へのTryReceiveは現在と同じ経路。
4. 各sourceのqueue.Advanceを一度だけ実行する。全NormalStep完了後に、成功したtransferだけがtarget.TryReceiveで入力を確定する。Normal入力最大1という既存構成契約によりtargetごとの書き込みは一意。自己接続は自己前進後に同じqueue末尾へ戻る。pending item参照はcommit後に解放する。
5. senderは正常に受け取れることが確定した場合だけ削除する。固定offerで可だった入力がcommit時に不可になる状態は、有効構成では起こらないことを検証する。同時入力を許すような回避用bufferや、新たな保持容量は足さない。

- [ ] **Step 1: Normal接続の固定期待値と再現テストを書く**

`BeltNormalConnectionTest` の主要な回帰テストは以下。追加ケースの具体的な入力と期待値は「固定期待値」を使う。

```csharp
[TestCase(false, false)]
[TestCase(false, true)]
[TestCase(true, false)]
[TestCase(true, true)]
public void IncomingNormalItemAdvancesExactlyOnce(bool parallel, bool reverse)
{
    var source = new BeltConveyorSegment(1, 64, BeltSegmentKind.Normal, 0);
    var target = new BeltConveyorSegment(1, 64, BeltSegmentKind.Normal, 0);
    var item = new BeltItem
    {
        Guid = new Guid(1, 0, 0, new byte[8]), ItemId = 1,
        Position = new ItemPosition(new BeltCell(0, 0, 0), BeltEntryDirection.FromBack, 0)
    };
    source.ConnectTo(target, BeltDirection.Front);
    source.RestoreItems(new[] { new BeltItemState(item, 32) });
    var order = reverse ? new[] { target, source } : new[] { source, target };
    new BeltSimulation(order).Tick(parallel);
    Assert.That(source.Count, Is.Zero);
    var received = target.CaptureItems();
    Assert.That(received.Length, Is.EqualTo(1));
    Assert.That(received[0].Item.Guid, Is.EqualTo(item.Guid));
    Assert.That(received[0].DistanceToExit, Is.EqualTo(224));
}
```

`BeltNormalConnectionReplayTest` は既存 `BeltSegmentReplayTest` の役割に従い、同一GUID・配線・状態から順方向逐次、逆方向逐次、並列を構築して毎tickの全segmentの `CaptureItems`、Buffer、PriorityIndexを比較する。新しいsnapshot書式をproduct公開APIへ追加しない。各列挙順でGUIDをソートして位置を失わせず、segmentごとに同じ順序で比較する。

- [ ] **Step 2: tick開始snapshotと段階末尾commitを実装する**

`BeltNormalTransfer` はsnapshot容量判定と一時的な搬送結果を所有する。`Commit`の失敗は構成契約違反として例外で顕在化させ、送信元から取り除いたアイテムを無音で捨てない。これは通常の詰まりではなく、競合入力などによりsnapshot時点の受け入れ保証を破った場合だけである。

```csharp
using System;

namespace Game.BeltSegment
{
    internal sealed class BeltNormalTransfer
    {
        readonly BeltConveyorSegment target;
        readonly BeltDirection inputDirection;
        int availableSpace;
        int stagedLength;
        BeltItem stagedItem;
        bool hasStagedItem;

        internal BeltNormalTransfer(BeltConveyorSegment target, BeltDirection inputDirection)
        {
            this.target = target;
            this.inputDirection = inputDirection;
        }

        internal void CaptureAvailableSpace()
        {
            // 搬入判定は全segmentの前進前の実空きに固定する。
            // Freeze actual entrance space before any segment advances.
            availableSpace = target.GetOffer(inputDirection);
        }

        internal bool TryStage(int length, in BeltItem item)
        {
            if (availableSpace < length) return false;
            stagedItem = item;
            stagedLength = length;
            hasStagedItem = true;
            return true;
        }

        internal void Commit()
        {
            if (!hasStagedItem) return;
            // 全Normal前進後に一度だけ入力を確定する。
            // Commit incoming items once after all Normal advances.
            if (!target.TryReceive(inputDirection, stagedLength, stagedItem))
                throw new InvalidOperationException("A staged Normal transfer lost its reserved entrance space.");
            hasStagedItem = false;
            stagedItem = default;
            stagedLength = 0;
        }
    }
}
```

有効構成では各edgeの `TryStage` はtickごとに最大1回、`Commit`はtick末尾に必ず呼ばれ、成功したpendingは全解消される。空き不足の `false` は通常の搬送状態でありログを毎tick発生させない。既存 `BeltConveyorSegment.TryReceive` と同じ意味の受け入れ判定で、失敗理由の隠蔽を目的としない。

```csharp
namespace Game.BeltSegment
{
    internal readonly struct BeltNormalStep
    {
        readonly BeltConveyorSegment segment;
        readonly BeltNormalTransfer transfer;

        internal BeltNormalStep(BeltConveyorSegment segment, BeltNormalTransfer transfer)
        {
            this.segment = segment;
            this.transfer = transfer;
        }

        internal void Advance() => segment.AdvanceAndTransfer(transfer);
    }
}
```

`BeltConveyorSegment` の `internal BeltDirection OutputDirection => outputDirection;` と以下の段階4だけを変更する。段階1のbuffer回収・段階2予約・段階3の受け取りは変更しない。

```csharp
internal void AdvanceAndTransfer(BeltNormalTransfer transfer)
{
    bool sent = false;
    int length = OutputLength;
    if (0 < length && Output != null)
    {
        // 通常列への搬出は相手の更新と分離して記録する。
        // Stage Normal output separately from the receiver update.
        sent = transfer != null
            ? transfer.TryStage(length, queue.HeadItem)
            : Output.TryReceive(BeltDirections.Opposite(outputDirection), length, queue.HeadItem);
    }
    queue.Advance(tickSpeed, sent);
}
```

Simulationの`normal`を`BeltNormalStep[]`へ置換し、`BeltNormalTransfer[] normalTransfers`を持たせる。`all/merges/buffers`の構成責務は既存と同じ。

```csharp
var normalList = new List<BeltNormalStep>();
var transferList = new List<BeltNormalTransfer>();
// Existing foreach (var segment in segments):
if (segment.Kind == BeltSegmentKind.Normal)
{
    BeltNormalTransfer transfer = null;
    if (segment.Output is BeltConveyorSegment target && target.Kind == BeltSegmentKind.Normal)
    {
        transfer = new BeltNormalTransfer(target, BeltDirections.Opposite(segment.OutputDirection));
        transferList.Add(transfer);
    }
    normalList.Add(new BeltNormalStep(segment, transfer));
}
else bufferList.Add(segment.Buffer);
// After enumeration:
normal = normalList.ToArray();
normalTransfers = transferList.ToArray();
```

```csharp
static readonly Action<BeltNormalStep> advanceNormal = step => step.Advance();

public void Tick(bool parallel)
{
    // 前tickの参照状態を全件固定してから既存段階を始める。
    // Capture all prior-tick reads before starting the existing phases.
    foreach (var transfer in normalTransfers) transfer.CaptureAvailableSpace();
    foreach (var segment in segments) segment.BeginTick();
    Run(buffers, collectBuffer, parallel);
    Run(merges, reserveMerge, parallel);
    Run(buffers, transferBuffer, parallel);
    Run(normal, advanceNormal, parallel);
    foreach (var transfer in normalTransfers) transfer.Commit();
}
```

現在の配線はSimulation再構築まで固定という既存契約を利用してedge抽出を構築時に行い、状態のバックアップ自体は毎tick行う。Unity外からの公開入口は引き続き `Tick(bool)` だけで、段階を呼び出し側へ露出しない。

- [ ] **Step 3: .NET・Unity・ベンチで検証しREADMEへ記録する**

```powershell
dotnet test tools/BeltSegment/Tests/BeltSegment.Tests.csproj -c Release
& 'C:/Users/5080/Documents/ChatGPT/uloop-native-3.2.2/uloop.exe' compile --project-path ./moorestech_client
& 'C:/Users/5080/Documents/ChatGPT/uloop-native-3.2.2/uloop.exe' run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'Tests.UnitTest.Game.BeltSegment'
dotnet run --project tools/BeltSegment/Benchmark/BeltSegment.Benchmark.csproj -c Release -- 1000 64 2000 200 serial
dotnet run --project tools/BeltSegment/Benchmark/BeltSegment.Benchmark.csproj -c Release -- 1000 64 2000 200 parallel
```

期待値: 全Coreテスト成功、Unitycompileエラー0、ベンチGUID・個数保存検査成功。既存ベンチは外部sinkなのでNormal→Normal経路の速さは測れない。変更前後の既存経路の回帰を調べ、Normal→Normalの割当は別のlinked-source診断で1000本自己接続・容量64・速度32・16item/segmentを200tick warmup後2000tick計測する。`Stopwatch`は診断側のみ、tickループ外で生成。保存状態の比較や文字列作成は計測区間外とする。

READMEには「対象edgeの実空きをtick開始時に記録する」「全Normal更新後に入力を確定する」「満杯の輪は停止する」「決定論的切れ目はWorld構築が担当する」の4点を追記する。正本0〜4段階のbuffer→Normalと今回のNormal→Normalでは搬入時点が異なることを説明する。

- [ ] **Step 4: 対象ファイルとUnity生成metadataをコミットする**

```powershell
git add -- moorestech_server/Assets/Scripts/Game.BeltSegment/Simulation moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltSegment tools/BeltSegment/README.md
git commit -m 'Task 1: 通常segment間の搬送をtick開始時の空きで確定する'
```

既存の無関係なdirtyファイルはステージしない。新metadataはUnityの自動生成後にだけ対象へ加える。

## 固定期待値

固定GUIDを使い、source/targetの更新配列順を両方試す。

```csharp
// A(capacity1,speed64,item distance32) -> B(capacity1,speed64,empty)
// One Tick: A empty, B item distance224, both update orders.
// Self(capacity4,speed64,item distance32): One Tick -> distance992.
// Full Self(capacity2,speed64,distances0,256): unchanged and count2.
// Blocked B(capacity1,speed64,item distance64) has offer=-64 at start.
// A may not use the space B creates during the same tick.
```

追加ケースは空構成、空Normal列、容量1自己接続、速度0/128、接続先速度0、前tickに満杯で次tickに外部搬出により空きが生じる連鎖、2〜3本Normalの循環、Normal→Normal→Merge→buffer→Normal。多tickごとに全GUIDと距離、buffer、RRを比較する。Capture/Restoreは同じ配線で再構築して再開し、再構築前後で継続状態が一致する。

| 入力・操作 | 固定期待値 |
|---|---|
| 空Simulation、空自己接続 | 例外なし、Count0 |
| cap1自己接続、speed64、item@32を2tick | 1tick目@0、2tick目も@0。容量1で1itemなのでD12に従う |
| cap4自己接続、item@32、speed0/128を1tick | それぞれ@32/@928、GUID不変 |
| A(cap1,speed64,item@32)→B(cap1,speed0,empty) | A空、B@224。次tickはB@224で停止 |
| A(cap1,64,item1@32)→B(cap1,64,item2@64)→C(cap2,64,empty) | tick1:A@0,B@0,C空。tick2:A@0,B空,C:item2@448。tick3:A空,B:item1@192,C:item2@384 |
| 3本Normal循環、各cap4/speed64、合計3itemを異なる距離に配置 | 200tickで全GUID/位置が逐次・逆順・並列一致、総数3 |
| 80tick後に全segmentをCapture/Restoreし同じ配線でSimulationを再生成 | 継続80tickの全GUID/位置/buffer/RRが再生成しない対照と一致 |

## 検証と閉じる手順

- `dotnet test tools/BeltSegment/Tests/BeltSegment.Tests.csproj -c Release`。既存15件と追加ケースが全成功。
- native uloopでplain client compile→`--test-mode EditMode --filter-type regex --filter-value 'Tests.UnitTest.Game.BeltSegment'`。Unityで同じproduction/testソースを確認。
- このCoreはまだWorldに呼び出されていないため、到達できないゲームテストを新設しない。後続World統合で輪の切れ目・セーブ・実搬送の実ゲーム検証を行う。
- 新tick経路についてウォームアップ後の逐次tick割当を比較し、既存のCoreベンチを変更前後で同じ引数で実行。速度の絶対値だけで性能改善を主張しない。
- 実装・Unity生成meta・D12 docsをコミット。moores-code-reviewでbranch全体をレビューし、pr-createでstacked PRを作成する。レビューで更新条件/順序を変えたら該当Coreテストを再実行。

## 判断記録（ADR）

- ユーザーD11/D12とADR0069を仕様の出所とする。満杯の停止は選択済みであり、将来予測の容量を導入して覆さない。
- snapshotを全アイテム配列のcopyとせず、隣接参照が実際に必要とするofferに限定するのはagentの機構判断。相手のライブqueueを参照していないことを検査する。
- 段階末尾commitは書き込み競合と二重前進を除くagentの機構判断。追加storageをゲーム仕様として持たせるものではなく、Tick完了時にすべて解消する。
- この内部更新規則を先に独立検証できるためWorldの速度やセーブ移行先の未回答を仮定しない。
- 録画付きプレイ検証/軽量EditModeInPlayingTestは、現時点でWorldからこのCoreへ到達する経路が無いため本PRには新設しない。既存のUnity EditModeで同じCoreを実行し、World統合段階で自己接続の実ゲーム検証を追加する。
- 受動案（現行の相手TryReceive直書きを残してsnapshotだけ加える）では相手queue.Advanceとの書き込み競合と列挙順による二重移動が残る。採用する段階末尾commit案は既存runnerの段階間バリアを拡張するもので、Core外に第2のrunnerを置かない。

## 既存操作の死活表

| 操作 | この変更後 | 根拠 |
|---|---|---|
| Normal→Merge/外部機械 | 維持 | transferの無い更新単位は現行TryReceive経路 |
| buffer→Normalの同tick前進 | 維持 | 段階3・4の順序は維持 |
| Capture/Restore、buffer、RR | 維持 | 新edgeはtick境界でpendingを持たず既存状態表現をそのまま復元 |
| 同じゲームワールドでの配置・撤去・セーブ | 本PRでは既存経路 | Worldから新Coreを使う切替は後続の統合で行う |

## Task 2: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること

- [ ] 接続段階の確定HEADをbaseとしてこのbranchの全差分をレビューする。ユーザーD11/D12、既存Coreの構成契約、独立Unity検証の範囲を4カテゴリcontextへ渡す。
- [ ] 指摘を反映したら対応するCore検証を再実行し、source反映diffの再レビューまで完了する。Worldから到達しない処理の実ゲーム検証を通過済みと主張しない。

## Task 3: セッション終了可能状態にすること

- [ ] pr-createスキルで接続branchをbaseとするstacked Draft PRを作成する。全対象変更をcommit/pushし、baseとの競合があれば解消・コンパイル確認後にpushする。新Core PRができてもsegment全体の移植完了とはせず、World統合を続ける。

## 事前レビュー結果

- 本体自己点検: D11/D12、配置層、呼び出し方向、CoreとWorldの責務を照合済み。
- 構造レビュー: 強0/弱1/第三分類0。弱1は異なるsourceとtargetのNormal分類を同じenum比較と捉えた指摘。役割も対象も異なり同じ導出結果の二重保持ではないため現構成を維持する（agent実装判断）。
- user-simulator: Critical0/Warning0/追加裁定0。offerがNormal自身の前進で減少しないこと、全呼び出し、linked-source収集、規模を確認。Q1/Q3/Q4/Q7へ依存する仮定を追加していない。
- Workflow/元モデルは利用不可。元Opus/Fableにgpt-6-astra high、元Sonnetにgpt-6-sol highを使用。元モデルを使用したと主張しない。
