# Web UI ネイティブ化 TODO

**入口・方針・依存関係**: `MIGRATION.md` / **各項目の作業詳細**: `plans/phase-*.md`
**最終更新**: 2026-07-18（旧 TODO を全面刷新。完了済み項目と旧方針の記述は `archive/` とgit履歴へ）

移行済み: インベントリ / ホットバー / ブロックインベントリ（機械・発電機・採掘機・ギア機械・フィルタ分岐器・チェスト・汎用）/
レシピビューア / クラフト / 研究ツリー / ビルドメニュー / モーダル / 進捗 / トースト / UIState ルーティング。
以下は**残作業のみ**。

---

## Track A: インフラ3本柱（`plans/phase-a-infra.md`）

- [ ] **A1: CEF バイナリ恒久統合（INFRA-1・最優先）** — LFS ポインタ手動回避の恒久解消（embedded package 化等）
- [ ] **A2: 入力・IME・フォーカス排他（INFRA-2）** — 入力二重配送の解消・IME・フォーカス往復後の入力復活
- [ ] **A3: 本番静的配信（INFRA-9）** — Vite 非依存のビルド済みアセット配信 + Vite 死活検知（dev）+ Windows 対応（INFRA-8、`ViteProcess.cs:246` pid TODO）

## Track B: ブロック系仕上げ・細部パリティ

### B1: ギア系ブロック（`plans/phase-b1-gear-blocks.md`）
- [ ] ギア伝達系ブロックのレジストリ登録（Generic 落ち解消。v8 blocks.json から対象 blockType を再列挙して確定）
- [ ] ElectricToGearGenerator 専用ビュー + 出力モード選択
- [ ] レジストリ網羅 e2e（v8 全 blockType × レジストリ照合）

### B2: 列車 PF・電柱（`plans/phase-b2-train-pf-electric-pole.md`）
- [ ] TrainItemPlatform / TrainFluidPlatform の PF インベントリ + 積込/卸しモードトグル
- [ ] ElectricPole ネットワーク情報表示

### B3: 細部パリティ（`plans/phase-b3-detail-parity.md`・1件ずつ独立）
- [ ] スプリットドラッグ（grab の複数スロット均等配分）
- [ ] 右ドラッグ連続1個配置
- [ ] クラフト長押し・進捗・キャンセル・連続クラフト
- [ ] CraftRecipeView の所持数/必要数テキスト（不足赤字）+ ツールチップ内訳
- [ ] アイテムリストのクラフト可能数バッジ/グレーアウト
- [ ] 機械詳細の分間生産数表示
- [ ] ホイールのホットバー切替を入力量累積に
- [ ] `ui_state.request` のホワイトリスト化（Story/PauseMenu 中の強制遷移防止）
- [ ] itemMaster の WS 再接続後リフレッシュ / crafting validator の堅牢化

## Track C: 大物画面（着手時に writing-plans 詳細計画を作成）

### C1: チャレンジ（`plans/phase-c1-challenge.md`）
- [ ] チャレンジツリー Topic + リスト/ツリービュー（接続線付きツリー基盤を確立）
- [ ] 進行中チャレンジ HUD（常駐・完了イベント購読）
- [ ] 死コード削除（`ChallengeListUI` 系2 + `UI/ChallengeList/` 空スタブ3）

### C2: ポーズ・モード HUD・共通部品（`plans/phase-c2-pause-mode-common.md`）
- [ ] ポーズメニュー（セーブ / メニュー復帰 / 切断表示）
- [ ] 設置モード HUD（選択ブロック・高さ・キー表示。3D プレビューは Unity 残置）
- [ ] 削除モード HUD + 不可理由ツールチップ
- [ ] 給電範囲オーバーレイの表示連携（3D は Unity 残置）
- [ ] 直接採掘 HUD（フォーカス・進捗）
- [ ] コンテキストメニュー / キー操作ヒント / クロスヘア / 全 UI 一括非表示（Ctrl+U）/ カーソル追従オーバーレイ

### C3: 列車 HUD・インベントリ（`plans/phase-c3-train-hud.md`）
- [ ] 列車乗車 HUD（入れ子状態機械・乗車入力・分岐選択 → 3D プレビュー駆動）
- [ ] 列車（貨車）インベントリ

### C4: スキット・チュートリアル・カットシーン（`plans/phase-c4-skit-tutorial.md`・**再設計から**）
- [ ] Web UI 要素 ID 規約（INFRA-12）の設計と全画面適用 ※チュートリアルハイライトの前提
- [ ] スキット再設計文書 → 実装（テキスト/選択肢 DOM 化 + 立ち絵/カメラ/ボイス同期。音声は INFRA-10 と要調整）
- [ ] バックグラウンドスキット（GameScreen オーバーレイ会話・軽量）
- [ ] チュートリアル再設計文書 → 実装（DOM ハイライト・キーガイド。3D ピン/矢印は Unity 残置）
- [ ] カットシーン連携（GameStateType Topic 化 → HUD 一括退避同期）

## Phase D: カットオーバー完了（`plans/phase-d-cutover.md`）

- [ ] クラフトツリー機能の削除（`InGame/CraftTree/` 一式 + 関連 UIState/参照）
- [ ] i18n（INFRA-11）: Web 側の文字列バインド基盤（`TextMeshProLocalize` 相当）
- [ ] 全 uGUI ビューのゲート化監査（`ui-completeness-reaudit-plan.md`〔archive〕の手順で網羅確認）
- [ ] PlayMode 全画面遷移スモーク + Ctrl+I トグル実機確認 + 実機 web↔host 連携検証
- [ ] （任意）INFRA-4: C#→TS 型自動生成 / GameStateType Topic の一般整備

## 品質バックログ（Phase 作業のついでに消化）

- 設計負債解消 WU1〜9（監査: `design-debt-audit-2026-07-17.md` / 実行計画: `subagent-execution-plan-2026-07-18.md`）
- モーダル RequestModal プロデューサ配線（実ユースケース決定待ち）
