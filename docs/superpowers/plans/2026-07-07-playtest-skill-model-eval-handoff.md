# 申し送り: プレイテストスキルのモデル別精度評価（Opus/Sonnetで再現できるか）

作成: 2026-07-07 / ブランチ: `feature/playtest-stabilization`（worktree: `/Users/katsumi/moorestech-worktrees/playtest`）
前回申し送り: `2026-07-07-playtest-input-layer-handoff.md`（Phase 3完了により役目を終えた）

> **実施済み（2026-07-07 同日）**: 6トライアル（Opus/Sonnet × L1/L2/L3）を完走し全合格。
> 結果は `2026-07-07-playtest-skill-model-eval-results.md` が正。実バグ2件確定（レールBlockIdは修正済み 05a8cb1a8）。

## ゴール

再構築した `unity-playmode-recorded-playtest` スキル（ユースケース別references構成）を使い、
**OpusやSonnetがFable相当の精度でプレイテストを遂行できるか**を測る。
スキルは「該当referenceだけ読めば実行できる」ことを設計目標にしており、その検証が本題。

## 現状（すべてコミット済み・動作確認済み）

- **スキル**: メインチェックアウト側 `/Users/katsumi/moorestech/.claude/skills/unity-playmode-recorded-playtest/`。
  SKILL.md=ルーター（絶対規則10・ユースケース表・Step 0）＋ references/ 7ファイル668行
  （run-scenario / write-scenario=Driver API全表 / place-blocks-via-ui / hotbar-driven-systems /
  input-injection / troubleshooting / legacy-manual-flow）
- **DSL**: UI経路設置（`PrepareBlockForUiPlacement`/`DragPlaceViaUi`/`PlaceBlockViaUi`）、
  ホットバー駆動（`GiveItemToHotbar`/`SelectHotbar`）、結線クリックまで実証済み
- **実証済みシナリオ5本**（`tools/playtest/scenarios/`）: sample-chest / belt-line（direct）/
  belt-line-via-ui（等価性8assert）/ gear-chain-pole-via-ui（2+3セグメント9assert）/
  gear-chain-connect-via-ui（クリック結線4assert）
- **preflight [5/5]**: ポート11564空きチェック追加済み（他worktree占有を300秒無言死の前に検出）
- このスキル経由で実バグ2件を検出・修正済み（PlaceBlock中Tab死・ポールゴーストBlockId未設定）

## 評価の設計（推奨）

### 課題セット（難易度3段階・カンニング耐性に注意）

1. **Level 1（手順追従）**: 「belt-line-via-ui.cs を実行して結果を報告して」
   - 見る点: run-scenario.mdの手順（PlayMode停止→一発実行→result.json読み→スクショ目視→revisions.json revert）を守れるか
2. **Level 2（応用作文）**: 既存手本の変形。例「鉄鉱石を採掘機→ベルト→かまど→チェストのラインをUI経路で組んで、
   精錬品がチェストに届くことを検証して」
   - 見る点: write-scenario.md のAPI表から正しく組めるか / 接続数assertを先に入れるか /
     masterのRequiredItems・inputConnectsを確認するか
3. **Level 3（未知領域）**: 既存シナリオに手本が無いタスク。例「電線で発電機と機械を繋いで稼働検証」
   「列車レールをUI経路で敷設」など。**既存シナリオのコピーで解けない題材を選ぶこと**
   - 見る点: Step 0（サブエージェント探索）を発動するか / legacy Input直読みに当たったとき
     input-injection.mdの手順（grep→HybridInput化）へ進めるか / 詰まったときtroubleshooting.mdの
     診断順（ログ的打ち→ライブリフレクション）を踏むか

### 計測指標（Fable基準との比較）

| 指標 | 今回のFable実績（参考基準） |
|---|---|
| 完走まで人間の介入回数 | Level 2相当=0回 / Level 3相当（歯車ポール）=1回（他worktree停止の確認のみ） |
| 誤ルート（OS入力・固定sleep・共有masterHEAD使用・ポート見落とし）の回数 | 0（スキルの絶対規則で防止される想定） |
| result.json全PASS到達までの試行回数 | belt-line-via-ui=3回 / gear-chain-pole=3回（うち1回は環境、1回は実バグ） |
| 「実バグ」と「自分のミス」の切り分け精度 | 実バグ2件を正しくプロダクト側修正に倒せた |

### 評価時の注意（テスト設計上の罠）

- **既存シナリオ・過去の会話履歴は強力なカンニングペーパー**。Level 2-3は座標・ブロック名を変えた新題材にする
- 評価対象モデルには**スキル経由の情報のみ**で解かせたい。メモリ（`~/.claude/projects/-Users-katsumi-moorestech/memory/`）にも
  同じ知見があるため、公平にするなら評価観点を「スキル＋メモリ込みの実運用構成で解けるか」に置くのが現実的
- run-skill-live-trial スキル（tmuxで本物セッションを実走させる）や run-skill-iter-improve が評価ハーネスとして使える。
  live-trialの落とし穴はメモリ `live-trial-harness-pitfalls` 参照
- 判定はresult.jsonだけでなく**録画/スクショの目視**まで含める（Fableでも「内部stateは正しいが絵が変」は目視でしか出ない）

## 環境の前提（開始前チェック）

1. playtest worktreeのUnity Editor(6000.3.8f1)が起動していること（`uloop launch ./moorestech_client`）
2. **他worktree（tree2等）のPlayModeが止まっていること**（ポート11564競合。preflightが検出するが、
   占有時はユーザーへ停止確認が必要。ソケットリーク時は当該Editorへ`RequestScriptReload()`）
3. ピン留めmaster: `/Users/katsumi/moorestech-worktrees/playtest-master`（584a14e）が存在すること
4. `uloop run-tests`と並走させない（UnityMcpSettings.json退避の衝突で全uloopコマンドが死ぬ）

## スキル改善ループの回し方（評価で穴が見つかったら）

- 評価セッションで詰まった箇所＝**referenceの記述不足**として扱い、該当referenceへ追記する
  （このセッションでも「詰まり所→スキル反映」を3周した。update-skill / code-review-subagent-updater が使える）
- 改善したらこの申し送りの「Fable実績」表を基準に再評価する

## 残課題（評価とは独立の機能面）

- 回転キー（R）注入による単クリック設置の方向指定（place system内部状態が外部から読めない）
- `TrainRailPlaceService.cs:69` に歯車ポールと同型の`PlaceInfo.BlockId`未設定疑い（レール設置が同じ壊れ方を
  している可能性。未検証——Level 3課題の題材候補でもある）
- EditModeInPlayingTest一括実行のフレーク（連続PlayMode遷移で初期化60秒タイムアウト。単独実行では通る）

## 参照

- スキル本体: `/Users/katsumi/moorestech/.claude/skills/unity-playmode-recorded-playtest/`（SKILL.md + references/）
- DSL実装: `moorestech_client/Assets/Scripts/Client.Playtest/`、ランナー: `tools/playtest/`
- 概要doc: `docs/playtest-dsl.md`
- 今回の主要コミット: a873f4d57(HybridInput移行+Tabバグ修正) / c7dbd5b57(UI経路API) /
  4d8c92e05(PrepareBlockForUiPlacement) / e6c5b388e(ポールBlockIdバグ修正) /
  0efb8d058(ホットバー基盤+ポート preflight) / f45dd98ef(クリック結線シナリオ)
