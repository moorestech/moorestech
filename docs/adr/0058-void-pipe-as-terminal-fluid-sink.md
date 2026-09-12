# 0058. ボイドパイプは受けた流体を全量消滅させる終端ブロックにする

日付: 2026-09-11
状態: 採択

## Context

流体系（`Game.Fluid` / `Game.Block/Blocks/Fluid`）には余剰液体を消滅させる手段が無く、ボイラー廃水や蒸留副産物が詰まると上流が停止する。パイプ以外の受け手（機械・ポンプ・列車プラットフォーム）は `IFluidInventory.AddLiquid` の残量返却で受け入れ可否を表現し、パイプ網からは `FluidBoundaryPort`（境界圧力0）として扱われる。機械・ポンプの出力も同じ `AddLiquid` を直接呼ぶ。

## Decision

- **ボイドパイプは新blockType `VoidPipe` の終端ブロック。`IFluidInventory` として受けた流体を全量消滅させ（残量0を返す）、パイプ網からは機械と同じ境界ポート扱い。通過はしない。内容量・セーブ状態を持たない。**
  出所: ユーザー裁定 2026-09-11 原文「液体を捨てれるボイドパイプがほしい」→ 選択「終端の吸い込み口ブロック」（[[2026-09-11-ボイドパイプは終端の吸い込み口ブロックにする]]）
  棄却案: 通過もできる漏れパイプ（`FluidSimNode` として網に参加し毎tick内容量を消滅。Stepperにノード種別分岐が要り下流供給量が読みにくい）

- **受け入れ面は1面のみ（inflowConnects 1方向・outflowConnects無し）。設置時の回転で向きを決める。**
  出所: ユーザー裁定 2026-09-11 質問「ボイドの受け入れ面（inflowConnects）はどうしますか？」→ 選択「1面のみ受け入れ（向きあり）」（[[2026-09-11-ボイドパイプの受け入れ面は1面のみにする]]）
  棄却案: 6面すべてで受け入れ（意図せぬ隣接パイプ・機械出力を吸う）

- **サーバー可変状態を持たず、BlockState・StateProcessor・プロトコルを新設しない。クライアントはprefabのみ。**
  出所: ユーザー裁定 2026-09-11 質問「ボイドのクライアント表示（BlockState同期）はどこまでやりますか？」→ 選択「状態同期なし・静的モデルのみ」（[[2026-09-11-ボイドパイプは状態同期なしの静的モデルにする]]）
  棄却案: 直近tickに消滅させた流体IDと量を毎tick同期し排水演出を出す

- **解放研究は「蒸気機関」（鉄のパイプと同時）。研究コストは変えない。**
  出所: ユーザー裁定 2026-09-11 質問「ボイドパイプはどの研究で解放しますか？」→ 選択「蒸気機関（鉄のパイプと同時）」（[[2026-09-11-ボイドパイプは研究「蒸気機関」で解放する]]）
  棄却案: 新しい配管（鋼鉄のパイプと同時）／液体タンク（タンクと同時）

- **建設コストは鉄板2。**
  出所: ユーザー裁定 2026-09-11 質問「ボイドパイプの建設コスト（requiredItems）は？」→ 選択「鉄板2」
  棄却案: 鉄板1（鉄のパイプと同じ）／鉄板5（ポンプ並み）

- **見た目は専用プレハブ `Vanilla/Block/VoidPipe`（PipeStraightのバリアント・非受け入れ側 +Z 端をキャップで封止）を使う。**
  出所: ユーザー裁定 2026-09-12（PR #1349 独立レビュー F01「逆向きに置くと無音で繋がらず見た目では気づけない」への裁定 other=「派生モデルをmoorestech_clientに作成してmasterと紐づけする」）
  これは下記の 2026-09-11 裁定（PipeStraight流用・新規プレハブを作らない）を上書きする
  旧裁定: 既存 `Vanilla/Block/PipeStraight` を流用し新規プレハブを作らない（[[2026-09-11-ボイドパイプの見た目は既存PipeStraightモデルを流用する]]）

- **受け入れconnectorのflowCapacityは1000。実効上限は常に送り手側で決まる。**
  出所: ユーザー裁定 2026-09-11 質問「ボイドの受け入れ面のflowCapacityは？」→ 選択「十分大きい値（例: 1000）でボトルネックにしない」（[[2026-09-11-ボイドパイプのflowCapacityは十分大きくしボトルネックにしない]]）
  棄却案: 鉄のパイプと同じ30／パイプより低い値（例: 10）

- **流体種別を問わず受け入れる（フィルタ無し）。**
  出所: ユーザー裁定 2026-09-11 質問「捨てる液体は種類を問わず受け入れますか。それともプレイヤーが指定した種類だけに限定しますか？」→ 選択「すべての液体を対象にする」
  棄却案: プレイヤーが指定した液体だけを対象にする（選択UI・設定セーブ・プロトコルが要りD3を覆す）

- **動力不要。電力・歯車に接続しない。**
  出所: ユーザー裁定 2026-09-11 質問「廃棄に電力や歯車の動力を必要としますか？」→ 選択「動力なしで動く」
  棄却案: 電力を必要とする／歯車の動力を必要とする（停電・停止で上流が詰まり、充足率同期が要りD3を覆す）

- **停止操作を設けない。配管の切断・ブロックの撤去で止める。**
  出所: ユーザー裁定 2026-09-11 質問「設置したボイドパイプを残したまま、プレイヤーが廃棄を一時停止できるようにしますか？」→ 選択「停止操作は設けない」
  棄却案: ブロックごとにオン・オフを切り替えられる（オンオフのセーブ状態・切替プロトコル・ブロックUIが要りD1・D3を覆す）

- agent前提:
  1. blockType名は `VoidPipe`（依頼原文の語「ボイドパイプ」に一致させる）。blockParamは `fluidInventoryConnectors` のみ（capacity・blockedRetryTicksは持たない。schemaの`optional`で吸収しない）
  2. サーバー実装は `Game.Block/Blocks/Fluid/VoidPipeComponent.cs`（`IFluidInventory`。`AddLiquid`は常に `new FluidStack(0, fluidId)` を返し、`GetFluidInventory`は空リスト）と `Game.Block/Factory/BlockTemplate/Fluid/VanillaVoidPipeTemplate.cs`。`FluidNetworkDatastore` はパイプ以外を既に境界ポートとして扱うため変更不要（前例: 機械・ポンプ受け手）
  3. IBlockSaveState・IBlockStateObservableを実装しない（可変状態が無いため）
  4. マスタ（v8 `moorestechAlphaMod_8`）: 名前「ボイドパイプ」、category「液体」subCategory「パイプ」、sortPriorityは鉄のパイプ(570)と歯車ポンプ(580)の間、blockSize 1x1x1、inflowConnects 1件（offset 0,0,0・directions [[0,0,-1]]・flowCapacity 1000・connectTankIndex 0）、outflowConnects 空。research.json「蒸気機関」の unlockBlock に追加し、`.mooreseditor/nodeGraph.v1.json` に鉄のパイプ隣のblockノードを置く（エディタがunlockBlockを配置から再生成するため両方揃える）。localization.csvに block名（Source/english/japanese/german）を追加
  5. テスト（CombinedTest）: ポンプ→パイプ→ボイドで上流が満杯にならず流れ続ける／機械出力の直付けで生産が止まらない／受け入れ面以外に置いたパイプは接続されない
  6. 別repo `moorestech_master` はブランチ＋push＋PRを作り、本repoの `.moorestech-external-revisions.json` のピンをそのpush済みコミットへ更新する（AGENTS.md必須手順）

## Consequences

- `VanillaSchema/blocks.yml` の blockType enum と blockParam switch に `VoidPipe` が増え、SourceGeneratorが `VoidPipeBlockParam` を生成する
- クライアント側の変更は無い（blockType別のswitchが無く、prefabのコンポーネントだけで動く）
- 将来アイテム版のボイドを作る場合は本ADRの「終端・状態無し」を前例として参照する
