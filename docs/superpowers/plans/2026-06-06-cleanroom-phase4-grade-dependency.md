# クリーンルーム フェーズ4 グレード依存 決着メモ（再決着版）

- 日付: 2026-06-06
- **改訂: 2026-06-12 — codemap v2 整合（プッシュ型・専用機械・Vanilla非改変）＋批判的レビュー反映（再決着）**
  - 旧版の根拠事実「アップグレード フェーズB計画は未作成・存在しない」は**誤り**だった（`2026-06-06-upgrade-system-phase-b-quality.md` が同ディレクトリに実在する）。本改訂で事実関係を訂正し、衝突回避の論拠を「フェーズBが無いから」ではなく「**フェーズBは Vanilla 機械対象・別worktree系統であり、クリーンルームは Vanilla 機械ファイル非改変の制約を負うから**」に差し替えて再決着する。
  - 「SourceGenerator がレベルファミリーのアイテム・段間合成レシピを自動生成する」という旧前提も**誤り**（mooresmaster はスキーマ→C#モデル生成のみ。マスタ**データ**は生成しない）。手書き定義に訂正。
  - 抽選順序を設計書§4 の処理順（**down-bin が品質シフトより先**）に統一し、EUV catastrophic 失敗の実装担当をフェーズ4 に確定。
- 対象: クリーンルーム フェーズ4（専用機械統合）の前提＝「出力チップのグレード（Lv）表現と抽選機構」をどう用意するか
- 種別: **決定メモ**。フェーズ4 TDDプランの前提を1つに固定する。契約（正）はコードマップ v2（`2026-06-06-cleanroom-phases2-5-codemap.md`）とバランス確定書。

---

## 1. 問題

クリーンルームの純度は「最大グレードの天井＋汚れによる下位グレードへの格下げ（down-bin）」を課す（設計書§4）。これは**出力チップが Lv1〜LvN の区別を持つ**ことを前提にする。だが:

- `ItemStack` に品質/Lvフィールドは**無い**（`ItemStackMetaData` はスケルトンのみ・未使用）。グレードは**独立 ItemId**で表すのがアップグレード設計書§3の確定事項。
- 「独立 ItemId のレベルファミリー」「機械の出力レベル分布抽選」「抽選順序の決定性（§7.2）」は、アップグレードシステムの**フェーズB（品質軸）**にも割り当てられている。
- つまり「レベル抽選」をクリーンルームとアップグレードBの**どちらが・どこに**実装するかを決めないと、二重実装の衝突または完成待ちブロックが起きる。

## 2. 事実関係（2026-06-12 訂正）

旧版の認識を以下のとおり訂正する:

| 項目 | 旧版の記述 | 実際（訂正） |
|---|---|---|
| フェーズB計画 | 「未作成・`2026-XX-XX-upgrade-system-phase-b.md` は存在しない」 | **存在する**。`2026-06-06-upgrade-system-phase-b-quality.md`（`levelFamilies.yml`＋`LevelFamilyMaster`＋`VanillaMachineProcessorComponent` 完了出力でのレベル抽選） |
| フェーズBの対象 | — | **Vanilla 機械系**（`VanillaMachineProcessorComponent`/`VanillaMachineOutputInventory` を Modify）。**別worktree系統の作業**（クリーンルームとは並行・独立に進む） |
| クリーンルーム側の制約 | （明示なし） | **Vanilla 機械ファイルを一切変更しない**（作業系統間の競合回避。コードマップ v2 §0/§4 で確定） |
| レベルファミリーの生成 | 「SourceGenerator が `ICチップ_Lv1..Lv4`＋段間合成レシピを自動生成」 | **誤り**。mooresmaster SourceGenerator は yaml スキーマ→C#モデル/ローダ生成のみ。アイテム・レシピの**マスタデータは items.json / craftRecipes.json への手書き定義** |
| フェーズA成果物（決定的乱数等） | 「`_processedCycleCount`/`DeterministicRoll` を流用」 | フェーズAも Vanilla 機械対象＝**流用するとフェーズA完成待ちでブロックされ、かつ Vanilla 依存が生じる**。クリーンルーム専用機械が**自前の同等フィールド**を持てば、規約（決定的シード・順序固定）への準拠だけで足りる |

## 3. 選択肢

| 案 | 内容 | 評価 |
|---|---|---|
| A. アップグレードのフェーズBに相乗りする | レベルファミリー＋出力レベル抽選はフェーズB（`levelFamilies.yml`）の完成を待ち、クリーンルームはそれを利用 | フェーズBは **Vanilla 機械対象・別worktree系統**。完成・マージ待ちでクリーンルームがブロックされ、さらにクリーンルームの効果を Vanilla 機械側へ挿す必要が生じて**非改変制約と矛盾**する。✗ |
| **B. フェーズ4が「レベル抽選コア」を半導体チェーン限定・専用機械内に内包（再決着・採用）** | クリーンルーム専用機械 `CleanRoomMachine` の中に「半導体チップのレベルファミリー（独立ItemId・手書き）＋出力レベル分布抽選」を実装。分布スキーマはフェーズBの `levelFamilies.yml` と**衝突しない半導体専用マスタ**に置く | クリーンルームを単独で完結できる。Vanilla 非改変制約とも整合。フェーズBとはファイル・スキーマ・機械が分離しており**物理的に衝突しない**。○ |
| C. クリーンルームとアップグレードBを合同で1プラン化 | 両者が同じ抽選を共有するので一緒に作る | 別worktree系統を結合することになり、競合回避の前提を自ら壊す。スコープ肥大。✗ |

## 4. 決定（再決着）：案B — フェーズ4が専用機械内にレベル抽選コアを内包する

**フェーズ4は、半導体チェーンのチップに限定した「レベルファミリー（手書き独立ItemId）＋出力レベル分布抽選」を、クリーンルーム専用機械 `CleanRoomMachine` の内部に実装する。クリーンルームの効果（MaxGrade／DownBinRate）は、プッシュされた `CleanRoomEffect` としてその抽選にかかる修正子になる。Vanilla 機械ファイルは一切変更しない。**

### 4.1 なぜ衝突しないか（フェーズBとの関係・再整理）

旧版の「フェーズBが存在しないから衝突しない」は誤った根拠だった。正しい衝突回避の構造は次のとおり:

- **ファイルの分離**: フェーズBの変更対象は Vanilla 機械ファイル＋`levelFamilies.yml`＋`LevelFamilyMaster`。フェーズ4 の変更対象は専用機械（`Game.Block/Blocks/CleanRoom/`）＋`semiconductorChips.yml`＋`SemiconductorChipMaster`。**同一ファイルを触らない**ため、worktree マージで物理衝突しない。
- **スキーマの分離**: 分布定義は半導体専用マスタ `semiconductorChips.yml`（スキーマID `semiconductorChips`）に置く。フェーズBの `levelFamilies.yml`（スキーマID `levelFamilies`）とはIDも構造も独立。
- **機械の分離**: フェーズBのレベル抽選は Vanilla 機械の出力に効く。フェーズ4 の抽選は `CleanRoomMachine` の出力に効く。1つの機械に両方の抽選が乗ることは構造上ない。
- **規約の共有（実装の非共有）**: 両者は §7.2 の「決定的シード・抽選順序の固定」という**規約**だけを共有し、実装（乱数関数・フィールド）は各自が持つ。フェーズ4 は専用プロセッサに自前の `_processedCycleCount` を持つため、フェーズA/B の実装に依存しない。

### 4.2 将来の統合パス（作りっぱなしにしない）

フェーズBの `levelFamilies.yml` が汎用レベルファミリー基盤として安定したら、半導体側は次の最小手順で統合できる形にしておく:

- **Lv↔ItemId 対応の読み出しを `SemiconductorChipMaster` の1箇所に隔離**する（フェーズ4プラン Task 2）。統合時はこの1箇所を `LevelFamilyMaster` 参照に差し替えるだけで、抽選コア（`SemiconductorChipDraw`）・専用機械・テストは無変更で済む。
- 分布テーブルの型 `IReadOnlyList<(int level, double weight)>` と抽選順序・salt 規約（§5）を公開仕様として固定し、フェーズB側がクリーンルーム式の修正子（天井/down-bin）を将来 Vanilla 側へ持ち込みたくなった場合の参照実装とする。

### 4.3 フェーズ4が用意するもの（コアの最小セット）

- **チップのレベルファミリー**: `ICチップ_Lv1..Lv4` を独立 ItemId で **items.json に手書き定義**（決定的 GUID＝固定値の手書き。ランダム生成禁止＝セーブ破損防止、アップグレード設計書§4.3）。段間合成レシピ（Lv1×n→Lv2 等）も **craftRecipes.json に手書き**。
- **半導体専用分布マスタ**: `semiconductorChips.yml` — チップ Lv↔ItemGuid 対応＋**レシピ出力要素単位**のレベル分布（`machineRecipeGuid`＋`outputItemGuid`→`levelWeights`）。レシピ単位ではなく出力要素単位にする（副産物をチップに差し替えないため）。
- **決定的抽選**: 専用プロセッサ自前の `_processedCycleCount`（セーブ対象）＋`_blockInstanceId` から seed を作り、salt 付き splitmix64 ＋**出力要素インデックス混合**でサブストリーム分離（同一サイクル内の複数出力の完全相関を防ぐ）。
- **EUV catastrophic 失敗**: `machineRecipes` の `outputItems.percent`（スキーマ既存・コード未実装）を**フェーズ4 の専用機械が初めて実装**する（担当の宙吊り解消。Vanilla 機械は引き続き percent を無視＝挙動変更なし）。
- **クリーンルーム修正子の合成点**: プッシュ受信した `CleanRoomEffect` を用いて §5 の順序で抽選。`MaxGrade=0`（Out）は**抽選せず出力なし**（サイレント Lv1 禁止。バランス確定書§1）。

## 5. フェーズ4プランへの引き渡し事項

1. レベルファミリー＝**半導体チップ限定・手書き定義**（items.json／craftRecipes.json、決定的 GUID 固定値）。汎用化はフェーズBの `levelFamilies.yml` に委ね、Lv↔ItemId 解決は `SemiconductorChipMaster` の1箇所に隔離（将来統合点）。
2. レベル決定は §7.2 の③として**専用機械内**に実装し、**自前の決定的乱数源**（`_processedCycleCount`＋`_blockInstanceId`＋出力要素インデックス＋salt 付き splitmix64）に一本化。フェーズA/B の実装には依存しない。
3. 合成順序を設計書§4 の処理順で固定: **室Invalid停止（プッシュ受信ゲート）→（稼働）→ 完了時: EUV失敗（percent・出力なし）→ 天井クランプ（MaxGrade）→ 基礎分布抽選 → down-bin格下げ（DownBinRate）→ [品質シフト挿入点：将来アップグレードB・salt 予約] → Lv確定**。**down-bin が品質シフトより先**（設計書§4 の 3→4 の順。逆にしない）。
4. 出力差し替えは専用 `CleanRoomMachineOutputInventory.InsertOutputSlot` の出力 ItemStack 生成直前・**出力要素単位**で行う。容量予約は**空スロット方式**（レベル付き出力1件につき空スロット1。down-bin/効果変動による ItemId 不一致での出力消失を防ぐ）。multi-block の部屋帰属は **`CleanRoomDatastore` がプッシュ側で判定**（`BlockPositionInfo.MinPos..MaxPos` 全占有セル × 部屋の `Cells`。機械側に部屋探索ループを書かない）。
5. 将来アップグレードB が品質シフトを差す挿入点を温存する: `SemiconductorChipDraw` の **down-bin の後・確定の前**にコメントで明示し、品質シフト用 salt（`0xA5A5_0000_0000_0004`）を予約（既存 salt: level-draw=`…0001`／down-bin=`…0002`／EUV失敗=`…0003` と衝突させない）。

## 6. リスク

- **二重実装の併存**: フェーズB（Vanilla 機械の `levelFamilies.yml` 抽選）とフェーズ4（専用機械の `semiconductorChips.yml` 抽選）は意図的に分離した並行実装であり、当面「レベル抽選」が2系統存在する。物理衝突はしないが、**両方がマージされた後に統合（§4.2 のパス）を実施するまで、分布定義の置き場が2つある**ことをドキュメント上明示し続ける必要がある。
- 半導体限定で作ったレベルファミリー機構を後で汎用化する際の移植コスト。ただし Lv↔ItemId 解決を `SemiconductorChipMaster` に隔離してあるため、統合は参照差し替えで済む見込み（§4.2）。
- フェーズBが将来クリーンルーム式の修正子（天井/down-bin）を Vanilla 側にも持ち込む場合、抽選順序・salt 規約（§5-3/5-5）が参照仕様になる。フェーズ4 完了時に本メモと実装コメントの規約記述を一致させておくこと。
