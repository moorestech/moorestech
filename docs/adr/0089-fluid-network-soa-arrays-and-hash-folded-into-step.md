# ADR 0089: 流体網はノード・面を列の配列(SoA)で持ち、ハッシュは Step の走査に相乗りさせる

- 日付: 2026-10-23
- 状態: 採択
- 関連: ADR 0088（パイプの tick 差分同期）、`.decisions/2026-10-23-流体網のノードと面は配列(SoA)で持ちパイプは添字だけを握る.md`

## 背景
ADR 0088 で流体シミュを決定論的にしクライアントの複製が同じ計算を回す形にしたが、状態は `FluidSimNode` / `FluidSimFace` のクラスで、面がノードを参照で持ち、Stepper は毎パスで参照を追っていた。2026-10-23 の実測（Release、Ryzen 9 9950X）では、ノード 80 B のオブジェクトのうちシミュとハッシュが読むのは 12〜32 B で、キャッシュラインの大半を捨てていた。4 tick ごとのハッシュは別走査で、配置を連続にしても散らばらせても同じ時間だった（オブジェクト参照追いのため）。
計測で分かった前提: Editor の Code Optimization が Debug だと全アセンブリが JIT 最適化無効で CPU 時間が 3〜6 倍遅く出る（同日 Release 固定へ）。Mono(Release) の配列なめは普通の `for` で DRAM 26 GB/s、unsafe ポインタで 37 GB/s。ハッシュの演算は 1 要素 15 サイクルで、メモリより演算が律速。

## 決定
- **網の状態は `FluidSimNetwork` が列の配列で持つ。** ノード: 座標・容量・量・流体ID。面: 両端のノード番号・上限・速度・向き許可。ポートは数が少なく相手で振る舞いが違うのでオブジェクトのまま `IFluidBoundaryPort[]`（`PipeNode` 参照は `NodeIndex` に）。サーバーの datastore とクライアントの複製が同じ型を使う。
  出所: ユーザー裁定 2026-10-23（AskUserQuestion「SoA 化の対象」→「流体のノード・面だけ」）。原文「① 列の分離（SoA）は絶対にやりたい」
  棄却案: ベルトのセグメントも横断配列へ（セグメント内部は既に配列、横断アクセスが少ない）
- **パイプは添字だけを握る。** `FluidPipeComponent.NodeIndex` は再構築ごとに振り直され、網に入る前（ロード直後・設置直後）の中身は面速度と同じくパイプ側に持ち越し、最初の再構築で配列へ移す。中身の読み書き（流入・セーブ・返却・WebUI）は添字経由。
  出所: ユーザー裁定 2026-10-23（「網が配列を持ち、パイプは添字だけ」）
  棄却案: パイプが小さな状態オブジェクトを持ち網は参照配列を持つ（参照追いが残る）
- **Stepper の作業値は `FluidSimulationScratch`（専用配列）へ。** 網と一緒に作り直し、毎 tick 再利用。状態の配列は全量送信・ハッシュ・セーブが読むものだけになる。
  出所: ユーザー裁定 2026-10-23（「Stepper 専用の作業配列」）
- **ハッシュは Stepper の走査に相乗りし、網が `LastStateHash` として常に現在値を持つ。** Step の最終パスが全要素を足し直し、Step の外で量が変わる経路（機械からの流入 `AddExternal`、複製の差分再生 `AddAccepted`）は要素ハッシュの差分で O(1) 更新する。網の構築直後は全走査（`RecomputeStateHash`）。送る側（`IFluidNetworkLookup.CurrentStateHash`、クライアント `FluidNetworkClientCache.CurrentHash`）は計算せず読むだけ。4 tick に 1 回の別走査は無くなった。
  出所: ユーザー裁定 2026-10-23（「毎tick計算して網が保持」）。原文「②も絶対にやりたい」
  棄却案: ハッシュ tick だけ計算するフラグを Step に渡す（同期の都合が Stepper に入る）／別パスのまま（SoA 後でも走査が 1 本余る）
  実装上の注意: 差分更新の「前の要素値」は流体IDを書き換える前に取る。これを誤ると空のノードへ最初の流入が入った tick だけハッシュが食い違う（実装中に同期テストで検出し修正）。
- **unsafe は使わない。** 普通の `for`＋添字。計測で安全な `for` でも DRAM 読みは 26 GB/s に届き、ハッシュ・シミュは演算律速なので差が出ない。
  出所: ユーザー原文「unsafe ポインタはやりすぎかと」

## 影響
- Game.Fluid/Simulation: `FluidSimNetwork`（新）・`FluidSimulationScratch`（新）・`FluidSimulationStepper`（配列版）・`IFluidBoundaryPort.NodeIndex`。`FluidSimNode` / `FluidSimFace` は削除。
- Game.Fluid/Sync: `FluidNetworkStateHash` は網を受け、要素関数（Counts / NodeElement / FaceElement / PortElement）を Stepper と網へ公開。`FluidNetworkFullStateCapture.Capture(network)`。複製は `FluidSimNetwork` を持つ `FluidNetworkReplica`、組み立ては配列を埋めるだけ。
- Game.Block/Blocks/Fluid: `FluidNetworkDatastore` は `FluidNetworkBuilder`（新）で網を作り、面速度は位置ペアで旧網から引き継ぐ。`FluidPipeComponent` は `Position` / `Capacity` / `NodeIndex` / `Amount` / `FluidId` と持ち越し。`IFluidNetworkDatastore.Network`、`IFluidNetworkLookup.CurrentStateHash`。
- Client: `FluidNetworkClientCache.CurrentHash` / `TryGetNodeIndex`、水位表示と WebUI は配列を添字で読む。
- セーブ形式は不変（パイプごとの量・流体GUID・面速度）。マイグレーション不要。
- テスト: Stepper 単体テストは網の配列で組む。`FluidFullStateAssert` は全量と網を比べる。「Step が残すハッシュ ＝ 全走査」のテストを追加。

## 検証
- 流体・ポンプ・パイプ・tick 同期・WebUI 系 190 件通過（Release）。
- （計測の元ファイル `PlaytestResults/scratch/*.log` と `PlaytestResults/ticktest/*.csv` は 2026-10-24 の片付けで削除した。数値は本 ADR の記載が正本）
- ミクロ計測（Release、合成した一直線の網）: `PlaytestResults/scratch/baseline-release-microprobes.log`（前）と `after-soa-release-microprobes.log`（後）。シミュ 1 tick（後はハッシュ込み）228 / 2,280 / 22,800 ノードで 17.7 / 158 / 1,775 µs ＋ 4 tick ごとのハッシュ 1.0 / 9.6 / 138 µs → 13.4 / 120 / 1,332 µs、割り当て 0 B。
- 実機計測（Release、水パイプ 5,000 本を足したベルト計測ワールド 13,628 ブロック、60 秒、前後 2 回ずつ、`PlaytestResults/ticktest/soa-{before,after}-pipes5k*.csv`）: サーバー 19.8〜20.0 tick/s、tick 間隔 p50 50.0 ms、p99 55〜73 ms、max 136〜358 ms、>100 ms が 3〜8 回。前後の差は回ごとの揺らぎの中に収まり、**tick 間隔の指標では SoA の効果は観測できない**。5,000 ノードのシミュは 1 tick 数百 µs で 50 ms の pacing に埋もれるため、期待どおりの結果。効果の根拠はミクロ計測の方に置く。
