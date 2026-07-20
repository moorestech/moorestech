# 実行計画書: uGUI→WebUI パリティ実装（2026-07-07）

> **2026-07-07 更新**: Phase 0〜2 は writing-plans 形式の実行計画
> `docs/superpowers/plans/2026-07-07-webui-parity-phase0-2.md` に詳細化済み。実行はそちらに従う。
> Phase 3〜6 のロードマップは引き続き本書が正。

`2026-07-07-parity-audit-verification-handoff.md`（裏取り済み申し送り）を実行に落とす計画。
**着手前に必ず申し送り本文を読むこと。** 特に「要訂正5点」を織り込まずに監査原文や
種リストをそのまま台帳化・実装すると誤った修正をする（GearEnergyTransformerキー追加、
プレイヤーインベントリ右クリック系の再実装、TankInventory整理、の3つが具体的な事故ポイント）。

## 前提となる現状

- all-code-review 全44系統は適用完了、最終QA全green（compile 0 / C# 57/57 / tsc 0 / vitest 122 / e2e 34）
- AGENTS.md 残タスクは2件: 「状態管理の適正化」「実装漏れの徹底洗い出し」
- 状態管理はレビュー全系統で Critical 0（進捗ドキュメント「クリーン判定」節）→ 軽量確認で足りる見込み
- 実装順序の規約: **「実装漏れ確定 → topic 拡充 → ビュー実装」の順を守る**（TODO.md 2節）

## Phase 0: 台帳一本化と後始末（実装に入る前に必ずやる）

1. **台帳統合**: 以下3ソースを `TODO.md` に一本化する
   - 申し送りの「そのまま採用可9項目」（訂正3の粒度指定に注意: 素材不足は「数値テキスト＋ツールチップが無い」と書く）
   - 申し送りの「要訂正5点」を反映した修正版の処方箋
   - `2026-07-06-all-code-review-progress.md` の種リスト全項目（Shift直接移動のSubInventory非対応 /
     BlockItemGrid右クリック系 / blockInventory e2e拡充 / ui_state.requestホワイトリスト /
     useItemMaster staleキャッシュ / モーダルプロデューサ配線 / crafting validators深掘り /
     Escでのblock UI close / 13種ブロックビュー / 列車インベントリ / 研究報酬の個数表示）
2. 統合後、進捗ドキュメント側の種リストに「TODO.md へ移設済み」と追記（削除はしない、履歴保全）
3. **台帳に書かないこと**: TankInventory の整理（意図的温存、訂正5）/ BaseCamp は低優先注記付きでのみ記載（訂正4）
4. **未検証項目の扱い**: A表の個別uGUIクラス名・UIScale・ツールチップ文言は「未検証」マークを付けて台帳化し、
   着手時に各自で実コード確認してから実装すること
5. 副産物3ファイル（`.moorestech-external-revisions.json` / `moorestech_client/.uloop/tools.json` /
   `Core.Master/_CompileRequester.cs` の diff）の処遇をユーザーに確認 → checkout で戻すか commit するか決める

## Phase 1: 状態管理の適正化（AGENTS.md タスク1・軽量）

レビューで Critical 0 のため、新規の大規模調査はしない。確認のみ:
- topicStore / toastStore / uiStore の責務分離が直近の機能追加（F4 の FluidSlots/Progress 再配信等）でも
  崩れていないか、`deliverTopicPayload` 単一書き込み口の迂回が生えていないかを grep で確認
- 問題なければ AGENTS.md からタスク行を削除してクローズ。問題があれば個別 issue として台帳へ

## Phase 2: ブロックスロット操作パリティ（優先度1・日常操作の穴）

対象: `features/blockInventory/blockLogic.ts`（現状 pickUpPayload / placePayload の2系統のみ）と
`BlockItemGrid`。uGUI 準拠で追加するもの:
- 右クリック半分取り / grab保持中の右クリック1個置き
- ダブルクリック収集
- Shift一括移動（main/hotbar↔block 双方向。プレイヤー側 directMove の main↔hotbar 限定も同時に解消）
- Escでのblock UI close（種リスト記録分）

実装手順:
1. uGUI側の該当操作の仕様を実コードで確認（プレイヤーインベントリ側の既実装
   `features/inventory/InventoryPanel/index.tsx` の onRightDown / onDoubleClick が流用元になる）
2. サーバー側 action（split / collect / 直接移動）が block インベントリ対象で既に受けられるか確認 →
   足りなければ C# 側 action 拡張から着手（順序規約どおり）
3. ビュー実装 → **blockInventory e2e を操作系ごとに拡充**（現状左クリックpickupのみで欠落を検出できない。
   e2e拡充は本Phaseの完了条件に含める）

## Phase 3: ギア伝達系＋ElectricToGearGenerator（優先度2・新発見の是正）

- **やってはいけないこと**: `GearEnergyTransformer` というキーをレジストリに追加する（そのblockTypeは
  スキーマにもv8マスタにも存在しない。訂正1）
- 正しい作業:
  1. `/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json` で
     `blockUIAddressablesPath: "Vanilla/UI/Block/GearEnergyTransformerUI"` を持つ blockType を**再列挙して確定**
     （申し送り時点の答えは Shaft / Gear / GearChainPole の5ブロックだが、必ず再列挙で裏取りする）
  2. 確定した blockType を web の `blockComponents` レジストリに登録（既存のギアUIビューを共用）
  3. **ElectricToGearGenerator 専用ビュー**: uGUI の `ElectricToGearGeneratorBlockInventoryView` ＋
     出力モード選択を移植。モード選択 action の topic/action 配線が無ければ C# 側から（順序規約どおり）
- 完了条件: v8マスタの該当6ブロック（伝達系5＋ElectricToGear 1）が Generic 落ちせず専用UIで開き、
  モード変更が操作できる。e2e にレジストリ網羅テスト（v8 blocks.json の blockType 全種 × レジストリ照合）を
  追加できれば再発防止になる（検討推奨）

## Phase 4: 列車PF・電柱ネットワーク情報（優先度3）

- TrainStation / TrainItemPlatform / TrainFluidPlatform（各1ブロック）: PF インベントリ＋モードトグル
- ElectricPole（3ブロック）: `ElectricPoleNetworkInfoUIView` 相当のネットワーク情報表示
- いずれも topic 拡充が必要な可能性が高い。uGUI 側の表示項目を実コードで確定してから DTO 設計
- 列車インベントリ本体（TrainInventoryView）は大物画面群（Phase 6）と依存を共有するため、ここでは PF のみ

## Phase 5: 操作・表示の細部パリティ（優先度4-5・独立小粒タスク群）

並行実行可能な独立タスク。1件ずつ完結させる:
- プレイヤーインベントリの**ドラッグ配分系のみ**（スプリットドラッグ均等配分 / 右ドラッグ連続1個配置。
  右クリック半分・1個置き・ダブルクリックは実装済みなので触らない。訂正2）
- クラフト長押し・連続クラフト（FEAT-CRAFT-1）
- アイテムリストのクラフト可能数バッジ（`craftLogic.craftable()` の使い所拡張）
- CraftRecipeView の不足素材**数値テキスト（所持/必要・不足赤字）とツールチップ内訳**
  （40%透過減光は実装済みなので触らない。訂正3）
- 機械詳細の分間生産数表示（`details/MachineSection.tsx`。レシピ時間×倍率からの算出ロジック要設計）
- ホイールのホットバー切替を入力量累積に（`InventoryPanel` の deltaY 符号判定を uGUI 準拠へ）

## Phase 6: 大物画面（優先度6・各画面が独立プロジェクト規模）

着手順: チャレンジ → ポーズ → 設置/破壊（PlaceBlock/DeleteBar）→ 列車HUD＋列車インベントリ →
スキット/チュートリアル。各画面とも `uiScreenRouting.ts` への state 追加＋uGUI側ゲートが必要。
BaseCamp はv8マスタに実体0個のため、マスタに登場するまで着手しない（訂正4）。
本Phaseは画面ごとに個別の実装計画を立ててから着手すること（本書ではスコープ確定まで）。

## 横断の検証ゲート（各Phase共通）

- .cs 変更時: `uloop compile --project-path ./moorestech_client` → 関連テストを `--filter-type regex` で
- web 変更時: tsc / vitest / 該当 e2e。決定論チェック（規約の機械判定）も all-code-review 時のスクリプトで
- Phase 2 完了時点で一度 **PlayMode 実機スモーク**を挟む（InitializeScenePipeline 分割後の
  スモーク未実施が残っているため、ここで兼ねて消化する。`unity-playmode-recorded-playtest` 使用）
- 各Phase完了ごとにコミット＋TODO.md のチェックボックス更新

## 見積もりと切れ目

| Phase | 規模感 | 切れ目として適切か |
|---|---|---|
| 0 台帳一本化 | 小（ドキュメント作業のみ） | ここで切っても安全 |
| 1 状態管理確認 | 小 | 同上 |
| 2 ブロックスロット操作 | 中（C#+TS+e2e） | 完了までは切らない（e2e拡充まで一体） |
| 3 ギア系＋ElectricToGear | 中 | レジストリ登録のみ先行完結も可 |
| 4 列車PF・電柱 | 中〜大（topic設計含む） | ブロック種ごとに切れる |
| 5 細部パリティ | 小粒×6 | 1件ごとに切れる |
| 6 大物画面 | 大×6画面 | 画面ごとに計画書を別途作成 |
