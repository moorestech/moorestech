# Belt Segment Core Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Dropboxの現行Coreを共有アセンブリへ移植し、新仕様のテストと同じソースを使う単体ベンチを実行できるようにする。

**Architecture:** `Game.BeltSegment` はUnity・ワールド・通信への依存を持たず、明示的な接続と外部ポートだけで計算する。サーバーとクライアントが同じコードを参照する。ワールドからの構築とGPUは全体移植の後続レビュー単位であり、このplan完了を全体の完成とは扱わない。

**Tech Stack:** C#、Unity 6000.3.8f1の共有asmdef、.NET 8、NUnit。

## Requirements

- 正本は `E:\Dropbox\seg\mock\8\Core` の10個のC#ファイルとREADME。旧Demo/Tests/Benchmarksをコピーしない。公開された有効入力の搬送結果が一致する。
- 幅256、速度確定→buffer回収→合流予約→buffer搬出→通常前進の段階順を維持し、serial/parallel両方の最終状態が一致する。
- 正常な満杯/搬入拒否は搬送の通常結果として扱う。コアへUnityログ、ワールド、ネットワーク、実時間を導入しない。
- itemのGUID・種類・位置情報、走行列の隙間・順序、buffer占有、RRを保持し、Capture/Restoreで引き継げる。
- ベンチは移植した本番Coreを参照し、コマンド引数でsegment数・容量・tick数・warmup・serial/parallelを変え、時間・割当・アイテム数・環境を出す。
- 未回答Q1〜Q6を決めない。旧ベルコンの撤去とゲーム内の切替は後続統合で行う。

## Global Constraints

- 作業先 `C:\Users\5080\Documents\GitHub\moorestech`、ブランチ `codex/belt-segment-migration`。ユーザーがこのパスでの作業を明示した。
- 基点 `d6a4d7d3189ce245786bca1dc64a050c8bee6b2c`、fetch後の `origin/master` と一致を確認済み。
- `AGENTS.md` に従い新規C#は200行以下、ディレクトリごとのコード10ファイル以下、partial/Func/デフォルト引数禁止。日本語→英語コメント。Unity YAMLと.metaは手書きしない。
- ソースのアルゴリズムを高速化/修正しない。前提外入力の扱いを独断で変えない。呼び出し側が速度0〜128、長さ1〜256、対応するcapacity・接続数を満たす。
- `uloop compile --project-path ./moorestech_client` を実行する。現時点の既存CEFパッケージ取得エラーは未解決であり、成功扱いにしない。

## File Structure

| 場所 | 責務 |
|---|---|
| `moorestech_server/Assets/Scripts/Game.BeltSegment/Game.BeltSegment.asmdef` | noEngineReferences=true、参照なしの共有Core |
| `moorestech_server/Assets/Scripts/Game.BeltSegment/Simulation/BeltConveyorSegment.cs` | ポート、予約、速度、段階に応じた操作 |
| `moorestech_server/Assets/Scripts/Game.BeltSegment/Simulation/BeltItemQueue.cs` | ring、gaps、blockSizes、capture/restore、前進/搬出 |
| `moorestech_server/Assets/Scripts/Game.BeltSegment/Simulation/BeltBuffer.cs` | 終端1アイテムと搬出RR |
| `moorestech_server/Assets/Scripts/Game.BeltSegment/Simulation/BeltSimulation.cs` | 全体の段階バリア |
| `moorestech_server/Assets/Scripts/Game.BeltSegment/Simulation/BeltPorts.cs` | IBeltSource、IBeltReceiver、BeltSegmentKind |
| `moorestech_server/Assets/Scripts/Game.BeltSegment/Items/` | BeltCell.cs、ItemPosition.cs、BeltItem.cs、BeltItemState.cs |
| `moorestech_server/Assets/Scripts/Game.BeltSegment/Primitives/` | BeltDirection.cs、BeltEntryDirection.cs、BeltConstants.cs |
| `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltSegment/` | 新仕様のCoreテスト |
| `tools/BeltSegment/` | 同じ本番ソースをリンクする.NETテスト/ベンチのcsprojと実行入口 |

全Core型のnamespaceは `Game.BeltSegment`。200行に収めるための責務分割以外の一般化はしない。

### Task 1: 共通Coreを移植し、搬送仕様を検証する

**Files:**
- Create: 上記 `moorestech_server/Assets/Scripts/Game.BeltSegment/` の12 C#ファイルとasmdef。
- Create: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltSegment/BeltSegmentMovementTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltSegment/BeltSegmentJunctionTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltSegment/BeltSegmentReplayTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/Server.Tests.asmdef` — Game.BeltSegment参照を追加。
- Create: `tools/BeltSegment/Tests/BeltSegment.Tests.csproj`

**Interfaces:**
- `new BeltConveyorSegment(int capacity, int speed, BeltSegmentKind kind, int priorityIndex)`、`SetSpeed(int speed)`、getter `Speed`。正本のデフォルト引数だけ除去する。
- `BeltSimulation(IEnumerable<BeltConveyorSegment> segments)`、`Tick(bool parallel)`。段階内部APIの公開は増やさない。
- `BeltCell(int x, int y, int z)`、`ItemPosition(BeltCell currentCell, BeltEntryDirection entryDirection, int progress)`。座標軸は元のXY平面/Z高さのまま。
- `IBeltSource.TryGetOutput(BeltDirection)`、`IBeltReceiver.AttachInput/GetOffer/TryReceive`、`CaptureItems/RestoreItems`、bufferの `TryGetItem/RestoreItem/ConnectTo` は正本の契約と同一。
- internal `BeltItemQueue` はcapacity、count、totalLength、先頭gap/itemを持ち、`EnqueueTail(int gap, in BeltItem item)`、`DequeueHead()`、`Advance(int tickSpeed, bool sent)`、`CaptureItems()`、`RestoreItems(BeltItemState[] states)` を担当する。

- [ ] 正本のファイルhashを報告へ残し、10ファイルを全文読んで移植する。namespaceを変え、上記必須引数/SetSpeedへ変更する。`BeltConveyorSegment` のring関連フィールドと `EnqueueTail` 以降のメソッド、およびcapture/restoreの計算をqueueへ移す。queue内部の計算式・分岐順・更新順を変えない。`Advance` だけtickSpeedを明示引数として受け取る。

```csharp
public int Speed { get; private set; }
public void SetSpeed(int speed)
{
    if (speed > BeltConstants.ItemWidth / 2)
        throw new ArgumentOutOfRangeException(nameof(speed));
    Speed = speed;
}
```

移植する残りのメソッド本体は上記正本の同名メソッドを使用する。sourceが200行を超える `BeltConveyorSegment` はqueue委譲後の二つの責務で分け、partialで行数制限を回避しない。`BeltSimulation` のActionは段階を反復するための既存処理でありイベント発火用途ではない。

- [ ] 以下の仕様テストを追加する。期待値は明示した段階結果または素朴な距離計算を使い、ring実装をテスト内に写さない。

```csharp
[Test]
public void BranchArrivalAdvancesAgainInNormalPhase()
{
    var branch = new BeltConveyorSegment(1, 32, BeltSegmentKind.Branch, 0);
    var target = new BeltConveyorSegment(2, 32, BeltSegmentKind.Normal, 0);
    var alternate = new BeltConveyorSegment(2, 32, BeltSegmentKind.Normal, 0);
    branch.Buffer.ConnectTo(target, BeltDirection.Front);
    branch.Buffer.ConnectTo(alternate, BeltDirection.Right);
    branch.RestoreItems(new[] { new BeltItemState(new BeltItem { Guid = Guid.NewGuid(), ItemId = 7 }, 16) });
    new BeltSimulation(new[] { branch, target, alternate }).Tick(false);
    Assert.That(branch.Count, Is.Zero);
    Assert.That(branch.Buffer.HasItem, Is.False);
    Assert.That(target.CaptureItems()[0].DistanceToExit, Is.EqualTo(448));
}
```

他の必須ケース: 0/1アイテム、速度0/128、先頭到達後の後続詰め、出口に余剰進入距離を渡した成功と拒否、満杯ringのwraparound、合流は予約した方向だけ受入、予約だけではRR不変、空き候補を飛ばして成功しても開始indexは1だけ進む、段階1で満杯だったbufferは同tick再回収しない、capture/restoreでGUID/順序/隙間/buffer/RR保持、12方向位置補間、同一の閉じたMerge/Branchネットワークをserial/parallelで複数tick実行した全状態一致。安全な有効トポロジーのみ使い、Normal→Normal直結は作らない。

- [ ] 同じ本番Coreと同じテストファイルを.NET 8からコンパイルして実行する。

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
    <IsPackable>false</IsPackable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
    <Compile Include="../../../moorestech_server/Assets/Scripts/Game.BeltSegment/**/*.cs" />
    <Compile Include="../../../moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltSegment/*.cs" />
  </ItemGroup>
</Project>
```

Run: `dotnet test tools/BeltSegment/Tests/BeltSegment.Tests.csproj -c Release`、`uloop compile --project-path ./moorestech_client`。前者は全PASSが必要、後者のエラーは生出力を記録して全体検証の未完事項に残す。Unity稼働後は `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltSegment.*Test"`。
- [ ] Task 1として上記変更だけをコミットする。

### Task 2: 同じCoreのベンチ入口を用意する

**Files:**
- Create: `tools/BeltSegment/Benchmark/BeltSegment.Benchmark.csproj`
- Create: `tools/BeltSegment/Benchmark/Program.cs`
- Create: `tools/BeltSegment/README.md`

**Interfaces:** `dotnet run --project tools/BeltSegment/Benchmark/BeltSegment.Benchmark.csproj -c Release -- <segmentCount> <capacity> <ticks> <warmup> <serial|parallel>`。

- [ ] .NET 8のコンソールprojectから `../../../moorestech_server/Assets/Scripts/Game.BeltSegment/**/*.cs` をリンクする。外部パッケージを追加しない。
- [ ] 引数5個を必須とし、segment数・capacity・ticksは正数、warmupは0以上、capacityはCore上限以下を検証する。各segmentはNormal/速度32、アイテムを4マスごとの間隔に置き、常に受け入れる個別sinkを付ける。warmupと測定には別の同じ構成を生成し、測定中は搬出成功したitemを次tick境界で搬入し、空になって負荷が消える条件を避ける。sinkは各segmentが専有し、parallelで共有可変sinkを作らない。

```csharp
var allocatedBefore = GC.GetTotalAllocatedBytes(true);
var stopwatch = Stopwatch.StartNew();
for (var tick = 0; tick < ticks; tick++)
{
    // 各sinkの搬出成功をtick境界で同じsegmentへ戻す。
    // Reinsert each sink's successful output at the next tick boundary.
    scenario.ReinsertOutputs();
    simulation.Tick(parallel);
}
stopwatch.Stop();
var allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
```

`scenario` の保持・生成・再投入は `Program.cs` 内の型で定義し、200行を超える場合は同ディレクトリに `BeltBenchmarkScenario.cs` を分ける。sinkは1個の保留itemと進入距離のみを持ち、`GetOffer` は256、`TryReceive` は保留が空のときだけ受ける。再投入時はpendingの全256ではなく出口に残った距離を用い、受入が成功したときだけ保留を消す。ベンチ上の循環源でありCoreへ追加する機能ではない。

出力JSON: 実行条件、RuntimeInformation.FrameworkDescription、ProcessorCount、measurementScope="tick-and-reinsertion"、elapsedMs、msPerTick、allocatedBytes、allocatedBytesPerTick、初期/終了アイテム数（sink保留を含む）。READMEにも時間と割当はTickと再投入処理の合計だと明記する。測定後に全アイテムのGUID重複と個数保存を確認し、不一致は非0終了。Stopwatchはベンチだけで使う。
- [ ] serial/parallel双方を小規模で実行し個数保存を確認する。続いて1000 segments ×64 capacity、2000 ticks、200 warmupを実行して結果を報告する。差分通知・GPU・ゲーム統合の性能ではないとREADMEへ明記する。
- [ ] Task 2としてベンチ変更をコミットする。

### Task 3: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること

- [ ] Core移植の差分とテスト、ベンチ、未決裁定の分離をレビューし、指摘反映後に関連検証を再実行する。

### Task 4: セッション終了可能状態にすること

- [ ] pr-createスキルでレビュー可能なPRを作成し、masterとのコンフリクトがあればマージして解消・コンパイル確認のうえpushする。Unityコンパイルが未完なら完成・マージ可能とは表示せずドラフトにする。全体の移植は引き続き未完として追跡する。

## 判断記録（ADR）

- [ADR0069](../../adr/0069-belt-segment-simulation.md)、[ユーザー裁定台帳](../../../.decisions/2026-09-23-ベルトsegment移植の確定事項.md)。D2が今回の挙動の正本。Q1〜Q6を代理回答せずCoreだけ切り出すのはagent前提。
- 独立質問監査 `01a0cc9b-f447-7521-b698-13a2bc4ee667` は6問とも未回答と判定。いずれもこのCoreへ渡すワールド構成や外部機械に関する裁定であり、今回の限定範囲には依存しない。
- 走行列をqueueへ分けるのはAGENTS.mdの200行制限への対応であり、同じデータ構造・計算順を維持する（agent前提）。
- このplan単独ではゲーム内実行経路を切り替えないためunity-playmode-recorded-playtestを含めない。全体移植で機械接続・同期・描画を切り替えた後に実施する。Coreの段階意味とparallelの一致は同じ本番ソースのNUnitで検証する（agent前提）。
- 同じタスクで実装を継続するのはユーザーの「これで実装完了して」に従う。新規セッションへの移動を前提にしない。
- 2026-09-23の独立計画レビューでCore範囲に追加裁定は無いと確認。Branchテストの2出力化とベンチ測定範囲の明記を反映。利用可能なモデルによるレビューであり、Fable/Opusによる予測採点とは扱わない（agent前提）。
