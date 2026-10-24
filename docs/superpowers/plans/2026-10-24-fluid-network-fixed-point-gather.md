# パイプ網の整数化（1/1000 単位）と gather 型 Stepper Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** パイプ網（網の配列・Stepper・差分と全量・ハッシュ・パイプのセーブ・クライアントのコピー）の流体量を 1/1000 単位の `long` にし、Stepper をノードごとに集める形（ノード範囲を分けて呼べる）に組み替える。機械側は double のまま境界で変換する。

**Architecture:** `FluidSimNetwork` の量・容量・速度・上限を `long`（ミリ単位）にし、構築時にノード→接続面・ノード→ポートの CSR 一覧を持つ。`FluidSimulationStepper` は 9 つのフェーズをノード範囲／面範囲／ポート範囲の関数にし、各フェーズは自分の添字の要素だけを書く。比例縮小は `desired × amount / sum` の整数有理数で行い、圧力は充填率を 2^30 で持つ。境界（`IFluidBoundaryPort.Deliver`・`FluidPipeComponent.AddLiquid`）で double と単位を変換し、増える側には丸めない。セーブは版 7（`amountMilli` / `velocityMilli`）。

**Tech Stack:** C# (.NET Standard 2.1 / Unity 6)、MessagePack、Newtonsoft.Json、NUnit（uloop）、csc 直実行でのコンパイル確認。

## Requirements

1. 網の量・容量・面の速度・面の上限・ポートの上限と速度が `long`（1 = 流体量 1/1000）。受け入れ: `FluidSimNetwork` に `double` の配列が無い。`FluidUnits.UnitsPerAmount == 1000`。
2. Stepper の結果が範囲分割に依らない。受け入れ: 同じ網を `Step(network, scratch)` と `Step(network, scratch, partitionCount: 3)` で 200 tick 回した配列（量・流体ID・面速度・ポート速度・ハッシュ）が完全一致する（新テスト）。
3. 質量保存が整数で厳密。受け入れ: `ChainConservationTest` 等で総量が `==`（許容誤差なし）。
4. 異種流体: 空ノードへ同じ tick に複数の流体が入ろうとしたら、番号最小の流入面の流体を採り、他は閉じる。受け入れ: 新テスト（空ノードの両側から水と油）で番号の小さい面の流体だけが入る。
5. 境界の丸め: 機械→パイプは `floor(amount×1000)` を受け入れ端数を機械に返す。パイプ→機械は機械が受け入れた double を `ceil` で単位にしてノードから引く（ノードが引く量 ≥ 機械が得た量、差は 1 単位未満）。受け入れ: 新テスト（0.0005 の流入は 0 受理で全量残る／機械が 0.3335 受け入れたらノードは 334 単位減る）。
6. 同期: 差分（受理量・配達量）と全量（量・容量・速度・上限）が `long` で通信され、クライアントのコピーがサーバーとビット一致。受け入れ: `FluidNetworkReplicaTest`・`FluidNetworkTickEventPacketTest`・`FluidNetworkFullSnapshotEventPacketTest` が通る。
7. ハッシュが `long` 要素で感度を保つ。受け入れ: `FluidNetworkStateHashTest` が通る。
8. セーブ形式: `WorldSaveAllInfo.CurrentVersion = 7`。`SaveMigrationStepV6ToV7` がパイプの `amount`→`amountMilli`（×1000 最近傍）、`faceVelocities[].velocity`→`velocityMilli` に変換する。受け入れ: 新テスト（版 6 の JSON が変換される・壊れた形は Failed）と `FluidPipeSaveLoadTest`。
9. クライアント表示: 水位は充填率、WebUI は `amount/1000.0`。受け入れ: `FluidBlockDetailDtoBuilderTest` で 40.0 / 100.0 が出る。
10. 本番コードにテスト専用 public を残さない。`partitionCount` 付きの `Step` は将来のスレッド配分の入口として本番 API とする。

やらないこと: 複数スレッドでの実行、機械のタンク・`FluidStack`・レシピ・UI の整数化、`FluidContainerSaveJsonObject` の変更。

## File Structure

新規:
- `Game.Fluid/FluidUnits.cs` — 単位定数と double↔long の変換（floor / ceil / nearest / ToAmount）。
- `Game.SaveLoad/Migration/Steps/SaveMigrationStepV6ToV7.cs` — パイプ state の整数化。
- `Tests/UnitTest/Game/SaveLoad/SaveMigrationStepV6ToV7Test.cs`。
- `Tests/UnitTest/Game/FluidSimulationStepperPartitionTest.cs` — 範囲分割の一致・異種流体の規則・境界の丸め。

変更（サーバー・網）: `FluidSimNetwork.cs`（long 配列、CSR、`ResolveAcceptedUnits`、`AddExternal(out long)`、`AddAccepted(long)`、`FillRate` は double を返す）、`FluidSimulationScratch.cs`（long／FluidId 作業配列）、`FluidSimulationStepper.cs`（gather・範囲関数・`Step(network, scratch, partitionCount)`）、`FluidSimulationConstants.cs`（整数比）、`IFluidBoundaryPort.cs`（`long` と `long Deliver(long attemptedUnits, FluidId fluidId)`）。
変更（同期）: `FluidNodeInflowRecord`・`FluidPortDeliveryRecord`・`FluidNetworkDiffRecorder`・`FluidNodeState`・`FluidFaceState`・`FluidPortState`・4 つの MessagePack・`FluidNetworkReplicaAssembler`・`FluidNetworkFullStateCapture`・`FluidNetworkReplica`・`FluidReplicaBoundaryPort`・`FluidNetworkStateHash`。
変更（ブロック）: `FluidPipeComponent`（容量と持ち越しを long、`Amount` は double を返す）、`FluidBoundaryPort`（`Deliver` の変換と ceil）、`FluidNetworkBuilder`、`FluidNetworkDatastore`（面上限・速度 long）、`IFluidNetworkDatastore`、`FluidPipeSaveComponent`／`FluidPipeSaveJsonObject`（`amountMilli`・`velocityMilli`）。
変更（セーブ）: `WorldSaveAllInfo.CurrentVersion`、`SaveAndEventServiceRegistration` の連鎖。
変更（クライアント）: `BlockDetailDtoBuilder`、`BlockInventoryTopic`（`_publishedPipeAmount` long）。
変更（テスト）: `FluidSimulationStepperTest`、`FluidNetworkReplicaTest`、`FluidNetworkStateHashTest`、`FluidFullStateAssert`、`FluidPipeSaveLoadTest`、`PumpDrainingSteadyStateTest`、`FluidBlockDetailDtoBuilderTest`、`FluidSyncPacketTestUtil`（必要なら）。

## Tasks

### Task 1: 単位と網の配列を long にする（Game.Fluid）
- [ ] `FluidUnits` を作る（`UnitsPerAmount=1000`、`ToUnitsFloor/Ceil/Nearest(double)`、`ToAmount(long)`）
- [ ] `FluidSimNetwork` の配列を long にし、ctor で CSR（`NodeFaceStart/NodeFaces`、`NodePortStart/NodePorts`）を作る。`ResolveAcceptedUnits`・`AddExternal(int, FluidStack, out long accepted)`・`AddAccepted(int, FluidId, long)`・`CleanupIfEmpty` を整数に
- [ ] `FluidNetworkStateHash` の要素関数を long に
- [ ] `IFluidBoundaryPort` を long に（`Deliver(long attemptedUnits, FluidId) → long acceptedUnits`）
- [ ] csc でコンパイル（Game.Fluid は単体では通らないので Task 3 まで通す）

### Task 2: Stepper を gather・範囲関数にする
- [ ] `FluidSimulationScratch` を `NodeOutflowSum/NodeInflowSum/NodeAmountStart(long)`、`NodeIncomingFluid(FluidId)`、`FaceDesired/FaceTentative/FaceSettled(long)` に
- [ ] `FluidSimulationConstants` を `PressureGainNumerator=12/Denominator=100`、`DampingPerMille=2`、`FillRateOne=1L<<30` に
- [ ] `FluidSimulationStepper.Step(network, scratch)` と `Step(network, scratch, int partitionCount)`。フェーズ: 速度更新（面）→ポート速度（ポート）→流出合計と開始量（ノード）→暫定流量（面）→流入合計と受け入れ流体（ノード）→確定流量（面）→量と流体ID（ノード）→配達（ノード、ポートは CSR 順）→掃除とノードハッシュ（ノード）。各フェーズは部分ハッシュ `uint` を返し、呼び出し側が足す

### Task 3: 同期の型を long にし、ブロック側を追従させる
- [ ] 差分レコード・全量 struct・MessagePack 4 本・`DiffRecorder`・`ReplicaAssembler`・`FullStateCapture`・`FluidNetworkReplica`・`FluidReplicaBoundaryPort`
- [ ] `FluidPipeComponent`（`CapacityUnits`、持ち越し long、`Amount`/`Capacity` は double を返す、`TakeContents`・`TakeLoadedFaceVelocity` long）、`FluidBoundaryPort`（`GetFlowCapacityPerTick` long、`Deliver` の変換）、`FluidNetworkBuilder`、`FluidNetworkDatastore`、`IFluidNetworkDatastore`
- [ ] `FluidPipeSaveJsonObject`（`amountMilli`・`velocityMilli`）、`FluidPipeSaveComponent`
- [ ] csc で Game.Fluid → Game.Block → Server.Event → Server.Boot が通る

### Task 4: セーブの版 7
- [ ] `WorldSaveAllInfo.CurrentVersion = 7`、`SaveMigrationStepV6ToV7`、連鎖へ登録
- [ ] `SaveMigrationStepV6ToV7Test`（変換・冪等・壊れた形の Failed）

### Task 5: クライアント
- [ ] `BlockDetailDtoBuilder`・`BlockInventoryTopic` を long→double 変換に
- [ ] csc で Client.Game・Client.WebUiHost

### Task 6: テストの追従と新設
- [ ] 既存テストの double 期待値を単位へ（許容誤差は 1 単位か 0）
- [ ] `FluidSimulationStepperPartitionTest`（分割一致・異種流体の番号規則・境界の丸め）
- [ ] uloop で `Fluid|Pipe|Pump|SaveMigration` の regex を実行

### Task 7: 文書
- [ ] ADR 0088 の「double のまま」の決定に「ADR 0090 で整数化」の追記、ADR 0089 の「tick で変わるのは量…」は型だけ追記
- [ ] PR 説明文の下書きに整数化の節を足す

## 判断記録（ADR）

| # | 決定 | 出所 |
|---|---|---|
| 1 | 今の PR に足す | ユーザー裁定 2026-10-24 原文「この並列化可能な状態への変更を追加したい」→ 選択「今の PR に足す」 |
| 2 | 範囲はパイプ網だけ | ユーザー裁定 2026-10-24 選択「パイプ網だけ」 |
| 3 | 1 単位 = 1/1000、long | ユーザー裁定 2026-10-24 選択「1/1000（ミリ）」 |
| 4 | 異種流体は番号最小の面を採る | ユーザー裁定 2026-10-24 選択「面の番号が小さい方を通す（今と同じ）」 |
| 5 | 並列実行はせず構造だけ | ユーザー裁定 2026-10-24 選択「構造だけ並列可能にし、実行は直列のまま」 |
| 6 | 境界の丸め・定数の整数比・比例縮小の有理数化・CSR・セーブのキー名 | agent前提（ADR 0090） |
