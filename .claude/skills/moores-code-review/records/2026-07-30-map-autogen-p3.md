# map-autogen-P3（生成地形・鉱脈・実機QA）レビュー記録 (2026-07-30)

<!-- One review run = one immutable file; only the post-merge outcome section may be appended later. -->

## 対象
- base: `3bf15f5d6` / reviewed head: `1d6e38f0d`（レビュー修正コミット `642269eb2`、ディレクトリ修正 `1d6e38f0d` を含む。既存dirtyの `user-simulator/modes/improve/misses.md` は対象外）
- ブランチ: `feat/map-autogen-p3` / PR: 作成前
- context要約 — ゴール: generated地形の転送・実行時構築・見た目キャッシュ、スポーン座標補正、item/fluid鉱脈、露頭・範囲表示、録画Player QAを完成させる。非目標: `GeneratedTerrain5x5`撤去、`gridSize`二重責務、`PlacementEntry.WorldPosition`座標系分割、複数tile splat座標、`PacketTest/`分割。許容トレードオフ: 単一tile、石油0件はmulti-tile前提のmaster帯域として別タスク、性能最適化は今回対象外。制約: Unity Playerで非pink、generated経路・落下復帰・原点2値を実機確認し、PR前に全レビューを通す。

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | 解消済み | comparison 0。Editor直下13件はQA入口を`Editor/Terrain`へ移して解消。限定catch 7件は外部ファイルI/O境界またはUniTaskテスト捕捉、行数2件は努力目標、PacketTest48件は明示的別タスク |
| 設計レンズ9本 | 解消済み | worldId path traversal、terrain meta不正組合せ、共通coreのitem語彙、outcrop初期化順、range view寿命、Stone材質を検出・修正 |
| generic reviewer 15本 | 解消済み/抑制あり | item/fluid衝突排除、裸定数、初期化前fall recoveryを修正。単一caller全local化等の規約過剰適用は破棄 |
| Codex外部監査 | 解消済み/Warning繰越 | path traversal、cacheピークコピー、outcrop fire-and-forget、template設定退行、gzip上限、Stone材質を修正。typed chunk result、Addressables全寿命、残存配列メモリは別タスク |
| Fable全般 | 解消済み/仕様記録 | Stone材質とoutcrop初期化を修正。oil=0は現行1km tileと2km外bandの組合せによる仕様上の繰越 |
| comment-rationale-guard | なし | Playtest隔離2件と木によるheight摂動順の根拠コメントを復元後、最終再検査Criticalなし |
| comment-convention-guard | なし | 冗長コメント32件を削除・短縮。根拠・複雑アルゴリズム・XML構造597件は例外として保持 |

## 適用した修正
- worldIdを16桁lower-hexへ限定しcache root逸脱を拒否、境界テスト追加（レンズ/Codex）→ `642269eb2`
- visual cacheを固定bufferのstreaming hash/read/writeへ変更しpayload複製を撤去、cache I/O失敗を派生物のmissへ隔離（Codex）→ `642269eb2`
- terrain metaをfactory経由の有効状態へ閉じ、gzip展開長をメタ期待値へ固定（レンズ/Codex）→ `642269eb2`
- item占有AABBをfluid配置へ渡して衝突を実装上排除、domain gateとseed/閾値定数を具体側へ移動（reviewer/レンズ）→ `642269eb2`
- outcrop生成を初期化awaitへ接続し、range viewのruntime material/rootをdispose（Fable/レンズ）→ `642269eb2`
- template地形の80/1設定を復元し、Player初期化前の落下補正を停止（Codex/reviewer）→ `642269eb2`
- Stone prefabをUnity Editor経由でStone materialへ差し替え（Fable/レンズ）→ `642269eb2`
- スポーンを全分岐でtile開区間へ制約し、2つのテストModを中心座標へ更新、境界/pathテストを分割（reviewer/Codex）→ `642269eb2`
- コメント根拠保全・規約修正と比較演算子修正（post-check/決定論）→ `642269eb2`
- QAビルド入口を既存`Editor/Terrain`へ移して直下ファイル増加を解消（決定論）→ `1d6e38f0d`

## 設計判断（AskUserQuestion裁定）
- 新規質問なし。ユーザーの申し送りにある5件の別タスク化と、性能・将来拡張を今回考慮不要とするAGENTS指示を適用した。

## 破棄した指摘
- chunk失敗をwire上のtyped resultへするCritical — malformed/generated欠損時の診断改善としては有効だが、現行はserver境界で原因付きfail-loudし、正常系データ消失はない。プロトコル全層変更になるためWarning繰越。
- class直下の単一caller private helperをすべてlocal function化 — `#region Internal`はローカル関数をまとめる用途の指定であり、全private helperのlocal化義務ではないため規約過剰適用。
- `InitializeScenePipeline`の既存`#if UNITY_EDITOR`分散 — 今回差分以前の構造で、今回の受入れを壊す根拠がない。
- 200行超過2件 — integration rule上の努力目標。今回追加ファイルは全て200行以下。
- `GeneratedTerrain5x5`、`gridSize`、`PlacementEntry.WorldPosition`、複数tile splat座標、PacketTest48件 — ユーザーが理由付きで別タスク化。

## 事後結果（マージ後追記可）
- なし

## メタ
- セッションID: Codex root + `moores_holistic` / `moores_lenses` / `moores_reviewers`
- スキップ系統: なし。Codex外部監査は357k tokenまで実行。post-checkは最終diffで再実行。
- 備考: 最新compile Error 0。関連回帰28/28、レビュー修正前を含む対象群54/54、実機統合3/3 PASS。全14件の再バッチは`-nographics`時のCEF Mach IPC待ちで停止したため、修正前の14/14証跡と最新の変更対象3/3を採用。
