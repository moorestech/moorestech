# design-question-triage 開発ハンドオフ

新セッションでこの作業を再開するための唯一のドキュメント。会話履歴の読み直しは不要。

> **STATUS (2026-07-05 15:55 完了)**: 残作業 1〜6 すべて完了。
> - iteration-2: with-skill 84% / baseline 65% (+19pt)。ユーザーレビュー承認済み（feedback.json）
> - GOAL VERIFICATION: PASS / blocker なし（fresh sonnet、score 非開示）
> - コミット: 8c69779c9（本体+references）/ 043ac2022（evals）/ 8042bb592（HANDOFF）
> - live trial: ✅ 合格。素の「壁打ちしたい」で brainstorming → 本スキルが自然発火、前提6件を根拠つき宣言、質問は「適用粒度」1問のみ（C型）。fresh evaluator PASS / blocker なし。報告書: .mso/live-trial/20260705-152411-design-question-triage-natfire/report.md
> - 未実施は任意の 7（description 最適化ループ）のみ
>
> **iteration-3（2026-07-05 16:15 完了・低コスト構成）**: iteration-2 の残弱点2系統に改善投入し検証済み。
> - 改善: ①新設ゲート（a7e6e53c3）②推奨リトマス（1dbd35cc1）
> - 検証: with-skill のみ低コスト再走（sonnet）。eval-0 7/7（前回6/7、新機構必然性が FAIL→PASS）、
>   eval-1 6/6（前回4/6、支配戦略質問化と質問内容の2件が FAIL→PASS）。iteration-2 の with-skill 失点は全て解消
> - 限界: baseline 側は再走していない（差分計測でなく絶対値の確認のみ）。n=1/eval なのでブレ余地あり

## 1. 何を作っているか

スキル `design-question-triage`（本ディレクトリ）。壁打ち・要件確認でユーザーへ質問する前に、
質問候補を「A: 調査で解決」「B: 原則で自明」「C: ユーザーの意思決定」にトリアージし、
A/B は自己解決して根拠つき前提として宣言、C だけを質問として届ける。

発端は 2026-07-05 のスタック数アップグレード壁打ちで受けた3指摘（スキルのアンチパターン実例に記録済み）:
1. 冪等にできる強化を increment で設計した
2. 動的状態をマスタ層に置く案を推奨した
3. 導出可能な値のために新プロトコル新設を設計に含めた

## 2. 準拠すべきメタスキル（必読）

- `/Users/katsumi/moorestech/.agents/skills/run-skill-iter-improve/SKILL.md` — 改善ループの規律（8 INVARIANTS）
- `/Users/katsumi/moorestech/.agents/skills/run-skill-live-trial/SKILL.md` — tmux 実セッションでの受け入れ試験

### iter 0 GOAL DECLARATION（宣言済み・変更禁止）
- **goal**: 壁打ち開始時、ユーザーに届く質問が「ユーザーにしか決められない価値判断」だけになり、自明な技術判断は根拠つき前提として宣言される（上記3指摘の同型を出さない）
- **proxy 妥当性**: アサーション通過率は Partial な代理 → 収束時に score 非開示の fresh agent による GOAL VERIFICATION 必須
- **緩め禁止リスト**: アサーション削除・弱体化 / 質問数上限の緩和 / baseline プロンプトへ型ヒント（「前提と質問を書け」等）を再注入

## 3. 現在の状態

### 成果物（このディレクトリ）
- `SKILL.md` — 本体（3指摘すべて反映済み。導出可能テストは SSoT ベースの抽象表現に修正済み）
- `references/moorestech-principles.md` — プロジェクト固有の B 判定照合表
- `evals/evals.json` — iteration-2 用テスト3件（誘導語除去済みプロンプト）
- **未コミット**。INVARIANT 6 により iteration-2 の eval データ確認後にロジック単位でコミットする

### 評価ワークスペース
`/private/tmp/claude-501/-Users-katsumi-moorestech-worktrees-tree1/9d96d54e-efeb-4311-8520-4e440a086d9d/scratchpad/dqt-workspace/`
（/tmp 配下なので消えている可能性あり。消えていたら iteration-2 を最初からやり直せばよい）

- `iteration-1/` — 完了。結果: with-skill 100% / baseline 89%（差は eval-0 の2項目のみ）
  - 教訓1: baseline プロンプトに「前提と質問」と書いてしまい型を教えていた（評価設計欠陥）
  - 教訓2: 構造チェック系アサーションは Fable/sonnet が素で満たすため判別力が無い
- `iteration-2/` — **中断**。baseline 3本完了（timing.json あり）、with-skill 3本はユーザーが kill（outputs 不完全、再実行必要）
  - `eval-{0,1,2}/eval_metadata.json` に強化済みアサーション定義済み（失敗モード狙い撃ち型）

### 実行レイアウト規約（重要・ハマった）
- aggregate_benchmark.py は `eval-N/<config>/run-1/grading.json` の3階層を要求。trial には最初から `run-1/outputs/` に書かせる
- grading.json の expectations フィールド名は `text` / `passed` / `evidence` 厳守（viewer が依存）
- 集計/ビューア: `cd ~/.agents/skills/skill-creator && python3 -m scripts.aggregate_benchmark <iter-dir> --skill-name design-question-triage` → `eval-viewer/generate_review.py <iter-dir> --skill-name ... --benchmark <iter-dir>/benchmark.json`（iteration-2 では `--previous-workspace <iteration-1>` を付ける）
- trial agent への注意書き: 「サブエージェント待ちで止まらず自力で完結」「最終メッセージは保存先パスのみ」を入れる（eval0 で早期終了事故あり）

### モデル方針（ユーザー指示: コスト削減）
- trial / 採点: **sonnet**（Fable 5 は使わない）
- 集計・ビューアは Python スクリプトで LLM 不使用

## 4. 残作業（順番どおり）

1. **iteration-2 の with-skill trial 3本を sonnet で再実行**
   - プロンプトは `iteration-2/eval-N/eval_metadata.json` の prompt を使用。with-skill 側のみ「作業前に本スキルの SKILL.md と references を読み手順に従え」を付加。baseline は再実行不要（完了済み）
   - 出力先: `iteration-2/eval-N/with_skill/run-1/outputs/response.md`（既存の壊れた outputs は削除してから）
2. **採点 6本（sonnet）**: grader は `~/.agents/skills/skill-creator/agents/grader.md` 準拠、eval_metadata.json の assertions を使用、`run-1/grading.json` に保存
3. **集計 + ビューア**（`--previous-workspace` 付き）→ ユーザーレビュー → feedback.json 反映
4. **GOAL VERIFICATION**: fresh agent 1体に score・履歴を渡さず、with-skill の response.md 群だけを見せて goal 達成を PASS|FAIL + blocker 列挙で判定させる（採点 agent とは別個体）
5. **コミット**（INVARIANT 6）: eval データ確認後、「スキル本体」「evals」を分けてロジック単位でコミット
6. **live trial（受け入れ）**: `run-skill-live-trial` に従い tmux の本物 claude セッションで「壁打ちしたい」とだけ言って本スキルが**自然発火**するか＋AskUserQuestion の質問が C 型に絞られるかを確認（subagent 方式では原理的に検証不能な部分）
7. （任意・高コスト）skill-creator の description 最適化ループ

## 5. 改善時の禁止事項（再掲）

- score を上げるためにアサーション・評価を緩めない（PASS 詐欺）
- 1 iteration の改善投入は 1-2 件まで
- 改善の正否判定は fresh agent のみ。orchestrator の体感を根拠にしない
