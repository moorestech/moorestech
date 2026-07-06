# Web UI — TODO / 現状インデックス

CEF 上で動く React 製ゲーム内 UI（`moorestech_web/webui` + `Client.WebUiHost`）の**入口ドキュメント**。
最新の全体像はここを見る。個別項目の詳細な受け入れ条件・監査根拠は下記「詳細台帳」を参照。

**最終更新**: 2026-07-06

---

## ドキュメントマップ

| ファイル | 役割 |
|---|---|
| **`TODO.md`（本書）** | 現状スナップショットと残タスクの入口。まずここを読む |
| `cef-webui-migration-todo.md` | **詳細台帳**。INFRA/FEAT 全項目の網羅 TODO（2026-06-14 スナップショット）。個別項目の根拠・受け入れ条件はここ |
| `cef-webui-plan.md` | CEF + React 採用の意思決定根拠・技術比較（Servo/Ladybird/Ultralight 検討ログ） |
| `cef-webui-tree2-render-investigation-2026-07-04.md` | vite.config 残骸による描画不能バグの解決記録（再発防止の教訓） |
| `ui-completeness-reaudit-plan.md` | uGUI→Web の網羅性 再監査**手順書**（見落とし再発防止のプロセス定義。再利用可） |
| `archive/2026-07-02-webui-mantine-migration.md` | Tailwind→Mantine 移行計画（**完了済み・履歴保管**） |

> 注: `cef-webui-migration-todo.md` は 2026-06-14 時点のスナップショットで、以降の実装
> （topicStore 導入・C#⇔TS ワイヤ契約単一化・`WebUiCefToggle.cs` の Ctrl+I トグル）は
> 未反映。**最新の進捗は本 TODO.md の「現状」節を正とする**。

---

## 現状スナップショット（2026-07-05）

Web UI は**中核ループ（インベントリ・クラフト・モーダル・進捗）が動作する第一段階**まで到達。
基盤（ワイヤ契約・状態管理・CEF ライフサイクル）は増設に耐える形に固まった。
個別ブロック詳細（機械/発電機/採掘機/ギア/フィルタ分岐器）と研究ツリーは移行済み（2026-07-06）。
uGUI パリティの残りの大物（チャレンジ・列車・電柱ネットワーク・スキット等）は未着手。

### 実装済み（機能）
- プレイヤーインベントリ（メインスロット + ホットバー、Grab オーバーレイ、ドラッグ/クリック操作）
- ホットバー選択（数字キー）
- ブロックインベントリ: Chest（汎用収納）/ Tank（流体）/ Generic（未登録ブロックのフォールバック汎用描画）
- 個別ブロック詳細 UI（FEAT-BLK-2/3/4/5/8・2026-07-06）: 機械 / 発電機 / 採掘機 / ギア機械 / フィルタ分岐器。capability 表示（電力・トルク・ギアネットワーク・燃料/進捗・FluidSlots）とフィルタ分岐器のモード/フィルタ設定アクションまで配線済み
- 研究ツリー（FEAT-RES-1・2026-07-06）: UIPosition 配置 + 接続線 + 研究実行。表示可否は `ui_state.current` の `ResearchTree` から導出（`research.tree` topic はノードデータのみ運ぶ）
- レシピビューア（クラフト `CraftRecipeView` / 機械 `MachineRecipeView` / アイテムリスト / ページャ）
- クラフト進捗バー（`ProgressArrow`）
- モーダル（確認ダイアログ）
- トースト通知（アクション失敗・バリデーション違反）
- 再接続オーバーレイ（切断検知で UI 全体をブロック）
- アイテムアイコン PNG / アイテムマスタ JSON の HTTP 配信
- E2E（Playwright + mock-host）: inventory / blockInventory / fluidSlot / hotbar / modal / progress / recipe / uiState / blockDetails / filterSplitter / research

### 基盤の到達点（直近コミットで整備）
- **ワイヤ契約の単一ソース化**: `bridge/protocol.ts` に Topic/ServerMsg/ClientMsg/Payload を集約。C# `WireFixtures/` を共有する `wireContract.test.ts` / `WireContractTest.cs` が C#⇔TS の型一致を両側から強制
- **状態管理の一方通行化**: zustand を `topicStore`（サーバ由来 state）/ `toastStore` / `uiStore`（クライアント UI 状態）に分離。`deliverTopicPayload` が唯一の書き込み口でバリデーション失敗は toast + 破棄
- **入力排他レイヤー** `activeLayer`（game / modal 等）で画面間の入力排他
- **CEF ライフサイクル隔離**: 起動部分失敗時ロールバック、Editor ドメインリロード/PlayMode 終了/Editor 終了フックでの確実なクリーンアップ、WS 境界の隔離
- **Ctrl+I 排他トグル** `WebUiCefToggle.cs`（uGUI/CEF 表示の重なり制御、INFRA-3 相当。※台帳未反映）
- Mantine v8 + CSS Modules への移行完了（Tailwind 依存は package.json から撤去済み）
- **INFRA-6 uGUIステートマシン・パススルー型（2026-07-06）**: uGUIの`UIStateControl`が唯一の状態源としてフル稼働（B/G/T/R/Esc 等すべて従来通り）。CEFはwebモード中**常時表示の透明オーバーレイ**（body透過 + 画面表示中のみ dim バックドロップ）。Webが置換済みのビュー（`PlayerInventoryViewController` / `RecipeViewerView`）だけ `SetActive` 内で webモードゲート。`ui_state.current` topic + `ui_state.request` action は維持。CEF RawImage の raycastTarget=0 で世界クリック貫通。PlayMode 遷移マトリクス **10/10 PASS** 検証済み（2026-07-06、`.superpowers/sdd/task-4-verification-report.md`）

---

## 残タスク

### 0. 後始末（軽微・即対応可）
- [x] `moorestech_web/webui/tailwind.config.js` / `tailwind.config.d.ts` を削除（Mantine 移行の唯一の残骸。依存・ディレクティブは既に不使用）。併せて `tsconfig.node.json` の `include` から存在しない `tailwind.config.ts` 参照を除去（2026-07-06）
- [x] 進捗の正を本 TODO.md に一本化する方針を確定。`cef-webui-migration-todo.md` 冒頭に「2026-06-14 スナップショットの詳細台帳・根拠アーカイブ」バナーを追加（最新進捗は反映しない運用に確定）（2026-07-06）

### 1. 横断インフラ（`cef-webui-migration-todo.md` INFRA-* 参照）
- [ ] **INFRA-1 CEF バイナリの恒久統合**（最優先の未解決課題）: `manifest.json` は今も `jp.juha.cefunitysample` を git URL 参照。LFS ポインタが解決されず手動 pull 回避を繰り返している。embedded package 化等の恒久対応
- [ ] **INFRA-4 C#→TS 型自動生成**: `bridge/payloadTypes.ts` は現状手書き。C# からの生成に置換
- 🟡 **INFRA-6 UIState 橋渡し（最小版済・2026-07-06）**: `ui_state.current` topic + `ui_state.request` action で UIState⇔Web を橋渡し済み。CEF表示はUIState駆動、App.tsx が state で画面ルーティング、webモード中の未対応state遷移は抑止。**GameStateType（第2状態機械）のTopic化は未着手**
- [ ] INFRA-5 アセット配信拡張 / INFRA-7 サーバーイベント push 規約 / INFRA-8 Windows・Linux 対応（`ViteProcess.cs:246` に Windows pid 特定の TODO）/ INFRA-9 本番配信堅牢性 / INFRA-10 CEF 音声専有 / INFRA-11 i18n / INFRA-12 要素 ID 規約 — いずれも未着手
- [ ] INFRA-13 CEF 堅牢性 — 一部前進（起動隔離・WS 境界隔離済み）、残りは継続

### 2. 機能移行（uGUI パリティ・大部分未着手）
ブロック系 payload は capability 詳細（機械/発電機/採掘機/ギア/フィルタ分岐器・FluidSlots・Progress）まで拡充済み（`BlockDetailDtoBuilder`）。残りのブロック種も**「実装漏れ確定 → topic 拡充 → ビュー実装」の順**を守る。

- [x] 個別ブロック UI（FEAT-BLK-2/3/4/5/8）: 発電機 / 機械 / 採掘機 / ギア系（`GearEnergyTransformerUIView`）/ フィルタ分岐器（2026-07-06）
- [ ] **個別ブロック UI（残り）**: 電柱ネットワーク情報（`ElectricPoleNetworkInfoUIView`）/ 列車 PF / ベースキャンプ
- [x] 研究ツリー（FEAT-RES-1, `ResearchTreeView`）（2026-07-06）※報酬アイテムの個数表示は保留（ワイヤ型が個数を未伝搬・要 C# 変更）
- [ ] **チャレンジ / 実績**（FEAT-CHAL-1/2, `ChallengeListUI` / `CurrentChallengeHudView`）
- [ ] **列車 UI 一式**（FEAT-TRAIN-1, `TrainInventoryView` / 各 PF インベントリ / `TrainHUDScreen`）
- [ ] クラフトツリー（FEAT-CRAFT-4）/ 長押しクラフト仕上げ（FEAT-CRAFT-1）
- [ ] モード系 HUD（FEAT-MODE-1〜5: 設置 / 削除 / デバッグ / 給電範囲 / 直接採掘）
- [ ] 共通部品（FEAT-COM-1 コンテキストメニュー / COM-4 キーヒント / COM-6 全 UI 一括非表示 / COM-7 カーソル追従オーバーレイ）
- [ ] ワールド系（FEAT-WORLD-1 3D ツールチップ / WORLD-2 HP バー / WORLD-3 マップ）
- [ ] チュートリアル（FEAT-TUT-1）/ スキット（FEAT-SKIT-1/2）/ カットシーン（FEAT-CUT-1）
- [ ] ポーズメニュー（FEAT-SYS-1）※メインメニュー SYS-2/3 は D3 決定でスコープ外

### 3. 検証（未検証で残る実機挙動）
- [ ] PlayMode で Ctrl+I トグルの実機目視確認（`unity-playmode-recorded-playtest` で録画可）
- [ ] INFRA-1 解消後の 実機 web↔host 連携検証（現状の保証は mock-host 相手の e2e + 録画 + コンパイルまで）
- [x] webモードの実機遷移確認（Tab開閉・ブロックインタラクト・✕ボタン）: **PlayMode 遷移マトリクス 10/10 PASS**（2026-07-06、レポート `.superpowers/sdd/task-4-verification-report.md`）

#### 既知の制限
- **入力の二重配送**: Web パネルのクリックが uGUI / 3D にも届く。実害は限定的だが恒久対応は INFRA-2（入力排他の一元化）で対処予定
- **Vite dev server 死活検知なし**: dev server 停止時の検知・復旧が未実装（INFRA-13 系フォローアップ）

### 4. 検討事項（`cef-webui-plan.md` 由来・台帳未収録）
- [ ] Ultralight 等 軽量代替レンダラーの試用・抽象化レイヤー要件抽出
- [ ] CEF リファクタ（ブラウザ差し替え可能な抽象化）
