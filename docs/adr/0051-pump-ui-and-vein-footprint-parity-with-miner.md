# 0051. ポンプUIを採掘機と同格にし、鉱脈判定・設置制限も採掘機と同規則にする

日付: 2026-09-05
状態: 採択

## Context

油井（`ElectricPump`）と歯車ポンプ（`GearPump`）はマスタの `blockUIAddressablesPath` が空で、クライアントは `IsBlockOpenable()` が偽のため `BlockInteractable` を付けず、UIが一切開かない。サーバー側の `PumpFluidOutputComponent` は内部タンクをセーブするだけで `IBlockStateDetail` を持たず、クライアントへ配信される状態が無い。Web UIの `SectionStackView` にもポンプ用の設定・セクションが無い。

鉱脈との対応もポンプだけ古い規則で残っている。`PumpFluidGenerationUtility.ResolveGenerationEntries` は設置原点1セルを `GetVeinsContainingCell`（AABB Y±1 inclusive）で引き、採掘機がADR 0039で移行した「底面フットプリントのXZ重なり（`OverlapsVeinXz`）」とは別物である。さらに採掘機は `VeinPlacementReporter` で掘れる鉱脈の外に置けないが、ポンプはどこにでも置け、鉱脈外に置くと電力を消費しながら何も出さない。油井は3×8×9と大きく、1セル判定では見た目と判定の不一致（ADR 0039が採掘機で直した問題）がそのまま出る。

並列セッションで暫定版（内部タンクを `FluidMachineInventoryStateDetail` で配信し汎用液体列に載せる最小実装）が進行中。uGUI完全撤去（`.decisions/2026-09-05-uGUIはパッケージごと完全撤去する.md`）も本日進行中。

## Decision

- **ポンプUIは採掘機UIと同格の情報を出す。** 内部タンク（液種・量・容量）、動力行、公称生成速度（分間量）、汲み上げ対象が無い場合の警告行。動力行は油井が電力充足率＋稼働状態ラベル（ADR 0010）、歯車ポンプが既存 GearSection（RPM・トルク）。
  出所: ユーザー裁定 2026-09-05 選択「タンク + 電力 + 生成速度 + 鉱脈状態」（[[2026-09-05-油井UIはタンク電力生成速度鉱脈状態を表示する]]）、選択「含める（電力行を歯車行に差し替え）」（[[2026-09-05-歯車ポンプも油井と同じポンプUI設計に含める]]）
  棄却案: ①タンク＋電力のみ ②タンクのみ（暫定版で確定） ③油井だけUI化し歯車ポンプはUIなし

- **ポンプの鉱脈判定は採掘機と同じ `OverlapsVeinXz`（底面フットプリントXZ重なり・Y不問）に統一し、設置も「汲み上げられる流体鉱脈の上」に限定する。** 汲み上げ対象は「XZ重なり ∧ マスタ `generateFluid` に含まれる流体」の鉱脈。`GetVeinsContainingCell` は手掘り用途にのみ残す。
  出所: ユーザー裁定 2026-09-05 選択「採掘機と同規則に統一し、設置も鉱脈上限定」（[[2026-09-05-ポンプの鉱脈判定は採掘機と同規則にし設置も鉱脈上限定にする]]）
  棄却案: ①判定規則だけ統一し設置は自由のまま ②現状の1セル判定を維持しUIで報告するだけ

- agent前提（ユーザー確認済み 2026-09-05・裁定ではない）:
  1. データ経路は採掘機と同型。サーバーは新設 `PumpBlockStateDetail`（汲み上げ中の流体ID列・公称の秒あたり生成量）、既存 `CommonMachineBlockStateDetail`（油井のみ。実効要求電力＝ADR 0010）、既存 `FluidMachineInventoryStateDetail`（入力0本・出力タンク1本）で配信する。歯車ポンプの動力は既存 `GearStateDetail`。新プロトコルは作らない（前例: `VanillaMinerProcessorComponent.GetBlockStateDetails`）
  2. 「鉱脈状態」は別boolを送らない。汲み上げ中流体が0件のときにWeb UIが警告行を出す（前例: `CommonMinerBlockStateDetail.CurrentMiningItemIdInts`）
  3. 生成速度は公称値（充足率100%時の分間量）。実効量は充足率と併せて読む（前例: `MinerDetailDto.ItemsPerMinute`）
  4. 稼働状態ラベルは「汲み上げ対象あり ∧ タンクに空きあり」で稼働中、それ以外は待機中。既存 `CanGenerateFluid` と同じ境界。停止中は無い
  5. 設置制限はクライアント側のみ（`VeinPlacementReporter` に採掘機と並ぶ第3の制限として追加）。サーバーは弾かない。鉱脈範囲表示は既に流体鉱脈を出している（`PlacementVeinViewResolver`）ので設置判定と同じ集合へ揃える。既存セーブの鉱脈外ポンプはロード時に新規則で引き直す（ADR 0039と同性質の割り切り）
  6. Web UIは `features/blockInventory/details/PumpSection.tsx` を新設し `SectionStackView` の汎用合成に乗せる。小型パネル。レジストリ登録はしない。`configByBlockType` に `ElectricPump` / `GearPump` を追加し液体列を出す
  7. マスタ `blockUIAddressablesPath` は「開ける」フラグとして機械と同じ値を入れる。フィールドの改名・廃止はuGUI完全撤去側が持つ
  8. 暫定版（タンクのみ）は本設計の部分集合。マージ後にその上へ積み増す

## Considered Options

`.decisions/` 同日3ファイルの棄却案を参照。

## Consequences

- サーバー: `PumpFluidGenerationUtility.ResolveGenerationEntries` の引数が原点セルから `BlockPositionInfo`（フットプリント）へ変わり、流体鉱脈の全列挙を `OverlapsVeinXz` で絞る。`PumpFluidOutputComponent` または新設コンポーネントが `IBlockStateDetail` を実装。油井は `CommonMachineBlockStateDetail` も出す。`PumpFluidVeinTest` の「原点セル基準」前提は「フットプリント基準」へ更新
- クライアント: `VeinPlacementReporter` に「ポンプは汲み上げられる流体鉱脈の上だけ」を追加し、ツールチップ文言（`PlaceMinerOutsideVein` と並ぶ新キー）をlocalizationへ追加。`BlockDetailDtoBuilder` に `PumpDetailDto`（汲み上げ中流体・分間量・電力）を追加
- Web UI: `PumpSection` 新設、`SectionStackView` 設定追加、`blockRegistryCoverage` の fixture/allowlist 更新、e2e/unit テスト追加
- マスタ（moorestech_master）: 油井・歯車ポンプの `blockUIAddressablesPath` を埋める。PRとピン更新
- 用語集: 「汲み上げ対象」「内部タンク」「公称生成速度」を追加
