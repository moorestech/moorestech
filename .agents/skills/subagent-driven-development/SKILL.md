---
name: subagent-driven-development
description: 現在のセッションで、独立したタスクからなる実装計画を実行する際に使用する。規模ゲート未満はopus固定の単一subagentが計画全体を実装し、閾値超はタスクごと派遣＋タスクレビューで進める
---

# Subagent-Driven Development

実装計画を、本体セッションが実装コードを書かずに完走させる。規模ゲート未満はopus固定の単一subagentが計画全体を実装し（**単一subagent実装モード**）、閾値超はタスクごとにimplementerを派遣してタスクレビューを挟む（**SDD本体**）。どちらも最後に moores-code-review → pr-create で閉じる。

- **核となる原則:** 本体は実装を書かない。subagentにはセッション履歴を継承させず、必要なコンテキストだけをファイルで渡す。本体コンテキストは調整作業のために温存する
- **ナレーション:** ツール呼び出しの間は最大1行。台帳とツール結果が記録を担う
- **継続実行:** タスクの合間に「続けてよいですか？」と確認しない。止まってよいのは解決できないBLOCKED・真に進行を妨げる曖昧さ・全タスク完了のみ

## 必須ゲート（3つ）

免除は人間がこのセッション内で自分の言葉で明示した場合だけで、その指示を進捗台帳に記録する。

1. **ワークスペース隔離（最初のsubagent派遣前・必須）** — 専用worktreeの外でimplementer（単一subagent含む）を派遣しない。例外は「既にworktree内にいる（再利用）」「人間が本体ワーキングツリーでの作業を明示した」の2つだけ。後者は隔離の免除であって担い手の変更ではない。本体でfeatureブランチを切っていても共有されているのはディレクトリなので例外にならない。手順・dirty時の移送・Library複製: [references/workspace-isolation.md](references/workspace-isolation.md)
2. **最終ブランチ全体レビュー** — 最後のタスク完了の瞬間に moores-code-review を **Skillツール経由**で自動・無条件・確認なしに実行する。「playtestまで」「テストが通るまで」というゴール文言は免除にならない。「推奨します・必要なら実行します」で終えるのはスキップと同じ
3. **PR作成** — 最終レビュー後、pr-create でPRを作成しセッションを閉じられる状態にするまでが完了。masterとのコンフリクトは解消（実作業はopus subagentへ委譲）してpush。「PRが必要なら作ります」で終えない

根拠・呼び出し方・所見対応の詳細: [references/controller-gates.md](references/controller-gates.md)

## 規模ゲート（ADR 0053）

**5条件をすべて満たすなら単一subagent実装モード。** 1つでも外れたらSDD本体:

1. 予想変更が約15ファイル以下
2. 予想変更が全体で約1,000行以下
3. 計画の実装タスクが7個以下
4. 実装と並行した長いデバッグ・実機e2e往復が見込まれない
5. 並列実行したい独立タスク群が無い

- **数えるのは実装タスク。** 動作確認・最終レビュー・PR作成などコードを書かない末尾の定型タスクは数えない。計画の見出しを機械的に数えない
- **判定は声に出す。** 最初の派遣より前に「実装4タスク・4ファイル → 単一subagent」の形で1行ユーザーへ出す
- **名指し起動でもゲートは評価する。** 閾値未満なら「この規模なら単一subagentモードが既定です」と1行述べて単一モードへ進む。SDD本体へ行くのは人間が「タスクごとに派遣して」等と明示した場合だけ
- 閾値未満で本体が書く選択肢は無い。閾値の根拠と旧インライン廃止の経緯: [references/background.md](references/background.md)

## 共通の前段（どちらのモードでも、最初の派遣前）

1. 台帳を確認する: `cat "$(git rev-parse --show-toplevel)/.superpowers/sdd/progress.md"`。完了記載のタスクは再派遣せず、完了マークの無い最初のタスクから再開する
2. ワークスペース隔離（ゲート1）
3. 計画を一度読み、**事前計画レビュー**を行う: タスク間・Global Constraintsとの矛盾、レビュー基準で欠陥になる義務付け、`moores-code-review/references/lens-digest.md` 違反を一括で人間へ提示する（問題なければ無言で進む）。詳細: controller-gates.md
4. 規模判定を1行で声に出す

## 単一subagent実装モード（閾値未満）

1. **隔離worktree側をcwdにして** `scripts/sdd-workspace` を実行し、報告ファイルを `<workspace>/single-report.md` に決める。既存の同名ファイルは削除する。`git rev-parse --short HEAD` を BASE として控える
2. 台帳へ `Single-subagent: dispatched base <sha7> report <path>` を書く
3. [single-implementer-prompt.md](single-implementer-prompt.md) で `model: opus` を明示し**フォアグラウンド**で派遣する。`scripts/task-brief` は使わず計画ファイル全体の絶対パスを直渡しし、`[TASK_RANGE]` を列挙し、計画ヘッダ・末尾タスクの無効化文言を必ず含める
4. ステータスに対応する（下表）。DONE なら報告ファイルの `Task N: done` 行が `[TASK_RANGE]` を全て覆い、`git log <base>..HEAD` の `Task N:` コミットと一致することを確認する
5. 台帳へ `Single-subagent: complete (commits <base7>..<head7>)` を書き、ゲート2 → ゲート3 へ進む。タスクレビュアーは派遣しない

- **継続再派遣は PARTIAL のみ・上限2回。** NEEDS_CONTEXT は回答して再派遣し数えない。BLOCKED の原因が計画欠陥なら回数に関わらず即座に人間へエスカレーション。2回で終わらなければ規模誤判定とみなし、残りをSDD本体へ切り替えて台帳に `Single-subagent: switched to SDD per-task at Task N (continuations 2)` を書く
- 計画のチェックボックス（`- [ ]`）は誰も更新しない。進捗の正は報告ファイルの `Task N: done` 行と台帳
- 継続手順・DONE_WITH_CONCERNS の fix 経路・compaction後の復旧: [references/single-subagent-mode.md](references/single-subagent-mode.md)

## SDD本体（閾値超）

タスクごとに次を回し、全タスク後にゲート2 → ゲート3 へ進む:

1. `scripts/task-brief PLAN_FILE N` でブリーフを抽出し、派遣前の `HEAD` を BASE として控える
2. [implementer-prompt.md](implementer-prompt.md) でモデルを明示して派遣する。渡すのはブリーフ・報告ファイル・前タスクからのインターフェースと決定事項・曖昧さの解消だけ（セッション履歴や定型契約文を貼らない）
3. ステータスに対応する（下表）
4. `scripts/review-package BASE HEAD` を生成し（BASEは派遣前に控えたコミット。`HEAD~1` は複数コミットタスクを無言で切り詰めるので禁止）、[task-reviewer-prompt.md](task-reviewer-prompt.md) で派遣する
5. Critical/Important は fix subagent へ（レビュー報告ファイルのパスと `implementer-contract.md` の絶対パスを渡し、所見は転記しない）→ 再レビュー。Minor は台帳に記録し最終レビューへ引き継ぐ
6. spec ✅ かつ quality Approved で台帳へ `Task N: complete (commits <base7>..<head7>, review clean)` を書き、次タスクへ

- レビュアーの ⚠️（diffから検証不能）項目は本体が計画横断コンテキストで解決する。実ギャップならspec不合格として差し戻す
- 複数の実装subagentを並列に派遣しない
- ファイルハンドオフ・レビュアープロンプトの組み立て・モデルティア: [references/per-task-mode.md](references/per-task-mode.md)

## Implementerのステータス対応

| ステータス | 対応 |
| --- | --- |
| DONE | SDD本体: review-package → タスクレビュアー。単一モード: 網羅突合 → 台帳完了行 → 最終レビュー |
| DONE_WITH_CONCERNS | 懸念を読む。正しさ・スコープに関するものなら対処してからレビューへ（単一モードは網羅突合の後、単一fix subagent・opus。継続回数に数えない）。単なる所見ならメモして進む |
| PARTIAL（単一モードのみ） | 報告ファイルと `git log` で完了タスクを確定し、残りだけを継続再派遣する。継続上限2回に数える唯一のステータス |
| NEEDS_CONTEXT | 不足コンテキストを回答して再派遣する。上限なし |
| BLOCKED | ①コンテキスト不足 → 補って同モデルで再派遣 ②推論不足 → 上位モデル ③大きすぎ → 分割 ④計画自体の誤り → 人間へエスカレーション。変更なしで同じモデルに再試行させない |

## 進捗台帳（`.superpowers/sdd/progress.md`）

会話メモリはcompactionを跨いで残らない。位置を見失ったコントローラーが完了済みタスク列を再派遣した失敗が最も高くついた。台帳が復旧マップであり、compaction後は自分の記憶より台帳と `git log` を信じる。

- SDD本体: タスクごとに `Task N: complete (commits <base7>..<head7>, review clean)`
- 単一モード: `dispatched` / `continuation #k from Task N base <sha7>`（PARTIAL継続のみ。この行数がそのまま継続回数） / `switched to SDD per-task …` / `complete`
- 単一モードの復旧順: **台帳 → 元subagentの生存確認（ListAgents。生きていれば結果を待つ） → 報告ファイル → `git log`**。`dispatched` だけで `complete` が無いとき、生存確認なしに再派遣しない（死亡と決めつけて再派遣し同一worktreeを二重編集した実事故がある）
- `git clean -fdx` は台帳を消す。発生したら `git log` から復旧する

## モデル選定

- **単一subagent実装モードのimplementerは `opus` 固定**（呼び出し元のエージェント定義がモデルを実験条件として固定している場合はそちらが優先）。最終レビュー所見を直す fix subagent のモデルは `moores-code-review/SKILL.md` Step 7 が決め、ここでは二重定義しない
- SDD本体は役割をこなせる最も非力なモデルを使う。ただし**ターン数はトークン単価に勝る**: プロース記述から書くimplementerとレビュアーは中位モデルを下限、計画にコードが書いてある転記タスクと単一ファイルの機械修正は最安ティア、統合・デバッグは標準、設計判断と最終レビューは最上位。詳細: per-task-mode.md
- **派遣時は常にモデルを明示する。** 未指定はセッションの最も高価なモデルを暗黙に継承し、この節を無効化する

## 危険信号（絶対にしないこと）

- ユーザー同意なしにmain/masterで実装を始める／worktreeを作らず（既にworktree内かも確認せず）最初の派遣をする。「小さい計画だから」は理由にならない
- 規模ゲート未満だからという理由で本体セッションが実装コードを書く（ADR 0053）
- 単一subagentを `opus` 以外・model未指定・バックグラウンドで派遣する
- 計画ファイル全体を渡すときにヘッダ・末尾タスクの無効化文言を省く（入れ子のSDD起動や勝手なPR作成が起きる）
- 単一subagentの途中終了時に、生存確認・報告ファイル・`git log` を経ずに再派遣する
- SDD本体でタスクレビューを飛ばす／spec・qualityの片方を欠く報告を受け入れる／spec ❌ を「まあ十分」で通す／Critical・Importantが開いたまま次タスクへ進む
- SDD本体のタスク単位派遣で計画ファイル全体を読ませる（ブリーフを渡す）／派遣プロンプトにセッション履歴や定型文を貼る／状況説明を省く
- レビュアーに「フラグを立てるな」「最大でもMinor」と先取り判断させる／diffファイルなしでレビュアーを派遣する
- implementerの自己レビューを実レビュー（SDD本体はタスクレビュー、単一モードは最終レビュー）の代替にする／subagentの質問を無視する／手動で直す（コンテキスト汚染）
- 台帳が完了とマークしたタスクを再派遣する

## ファイル構成

- 派遣テンプレ: [single-implementer-prompt.md](single-implementer-prompt.md) / [implementer-prompt.md](implementer-prompt.md) / [task-reviewer-prompt.md](task-reviewer-prompt.md)。定型は [implementer-contract.md](implementer-contract.md) / [task-reviewer-contract.md](task-reviewer-contract.md) をsubagentが自分で読む
- scripts: `sdd-workspace`（作業ディレクトリ解決）/ `task-brief`（タスク抽出）/ `review-package`（diff束ね）
- references: workspace-isolation.md / controller-gates.md / single-subagent-mode.md / per-task-mode.md / background.md（根拠・図解・利点とコスト・関連スキル）
