---
name: agent-runtime-compat
description: このrepoのskillはClaude Codeのツール名（AskUserQuestion・Agentツール・SendMessage・Workflow・model: opus 等）で書かれている。自分がCodexで動いているとき、またはskill本文のツールが手元に無いときに、何へ読み替えるかの対応表。skillを実行する前・「このツールが無い」と気付いたときに読む。skillを新規作成・改訂して実行体依存の手順を書くときの書式もここが正本。
---

# 実行体別の読み替え（Claude Code / Codex）

このrepoのskillは同じ `.agents/skills/` を Claude Code（`.claude/skills` symlink）と Codex（`.codex/skills` symlink）の両方が読む。本文は歴史的に Claude Code のツール名で書かれている。**手順の意図は共通、手段だけが実行体で違う。** 各skillの「実行体別」節にそのskill固有の差分があり、そこに無いものはこの表に従う。

## 自分がどちらかの判定

- ツール一覧に `Agent` / `AskUserQuestion` / `Skill` がある → **Claude Code**
- ツール一覧に `spawn_agent` / `wait_agent` / `exec_command`（`apply_patch`）がある → **Codex**
- どちらでもない実行体は、Codex 列を「そのツールが無い場合の素の代替」として読む

## 対応表

| 意図 | Claude Code | Codex |
| --- | --- | --- |
| ユーザーに裁定を仰ぐ | `AskUserQuestion`（選択肢付き・推奨を先頭） | 対話セッション: 本文に「質問・選択肢（推奨を先頭）・各案の帰結」を番号付きで書いてターンを終える。`codex exec`／無人・委任済み: **聞かない**。推奨案で進め、仮定を報告と `bd note` に残す |
| subagentを派遣 | `Agent`（`subagent_type`・`model`・prompt） | `spawn_agent`（`task_name`・`model`・`reasoning_effort`・`message`。`fork_turns: "none"` で文脈を引き継がせない） |
| subagentの完了を待つ | 前景派遣なら戻り値。背景なら完了通知を待つ（pollしない） | `wait_agent`。**`timeout_ms` は 600000 以上**にする（60000 の連打は2026-09-21に10回以上の空振りとnudgeを生んだ）。タイムアウトは失敗ではない — `list_agents` で生存を見て待ち直す |
| 生きているsubagentへ追加指示 | `SendMessage`（agent id / name） | 作業中なら `send_message`（`target: "/root/<task_name>"`）、完了済みへ次の仕事を渡すなら `followup_task` |
| subagentの生存確認 | `ListAgents` | `list_agents` |
| 決定論的な多体オーケストレーション | `Workflow`（`scriptPath`・`args`） | **無い。** そのskillの「Workflow不可時のフォールバック」節へ落ち、落ちた事実を報告冒頭に書く |
| 別skillを起動 | `Skill` ツール | `.agents/skills/<name>/SKILL.md` を自分で全文読み、その手順に従う（「読んだだけで実行しない」は起動していないのと同じ） |
| 長いコマンドを裏で回す | `Bash` の `run_in_background: true` | `exec_command` を短い `yield_time_ms` で返し、同じセッションIDを後で読む。別プロセスに切り離すなら `nohup … &` とログファイル |
| ファイル編集 | `Edit` / `Write` | `apply_patch` |
| 当ターンの作業チェックリスト | ハーネス内蔵タスクリスト（タスク台帳は bd が正） | `update_plan`（同上） |
| 無人実行の縛り | `/goal`・skill frontmatter の `hooks:` | どちらも**効かない**（下記） |

### モデル名の読み替え

skill本文の `model:` 指定は「その工程に要る判断力の段」を表す。Codex では同じ段のモデルを明示する。**未指定で派遣しない**（既定モデルへ黙って落ちる）。

| skill本文（Claude） | 役割 | Codex |
| --- | --- | --- |
| `fable` / `opus` | 設計判断・実装・反証・コンフリクト解消 | `gpt-6-astra` |
| `sonnet` | 手順が決まった実務・統合・整形 | `gpt-5.6-sol` |
| `haiku` | 機械的な抽出・圧縮 | `gpt-5.6-luna` |

2026-09-21 の実測では、実装を `gpt-5.6-luna` へ渡した Task が Unity コンパイル待ちで 543 秒空転し、2ファイルの Task に 14 分かかった。skillが `opus` 固定と書いている実装役を `luna` へ落とさない。

## skill frontmatter の `hooks:` は Claude Code でしか動かない

`hooks:` を持つskill（pr-independent-review・pr-adjudicated-apply・bug-report-auto-fix・daily-build-repair・writing-plans・moores-grill-with-docs）は、Claude Code ではskill発動中だけ関所（Stopブロック・AskUserQuestionのdeny・編集追跡）が立つ。**Codex ではこの関所は存在しない**ので、同じ規律を自分で守る:

- 関所が「成果物が無いまま終われない」型なら、ターンを終える前に成果物ファイルの存在を自分で `ls` して確かめる
- 関所が「質問をdenyする」型なら、質問せず既定表・推奨案で進める
- 関所が「編集を追跡してStopで検査する」型なら、終了前にそのskillが指定する検査コマンド・検査工程を自分で実行する

repo全体の hook（`.dev-hooks/`）は `.codex/hooks.json` にも登録済みで Codex でも効く（bd ガード・decisions 索引・check-diff・commit-map・logs-sync）。Claude 側にだけあるのは poll-guard・commit-guard・decisions-ruling-reminder・`LEARN:` 行の自動 note 化（Stop hook）。**Codex では学びを `bd note <id> "LEARN: …"` で自分で書く。**

## パスの読み替え

- skill本文の `.claude/skills/<name>/…` は `.agents/skills/<name>/…` と同じ実体（symlink）。Codex からは `.agents/skills/` か `.codex/skills/` で引く
- Claude の transcript は `~/.claude/projects/<slug>/<session>.jsonl`、Codex の rollout は `~/.codex/sessions/YYYY/MM/DD/rollout-*.jsonl`。transcript を読むスクリプト（user-simulator の shadow 採点等）は Claude 形式前提で、Codex の rollout にはそのまま使えない
- 機械ローカルの規約: Claude は `CLAUDE.local.md`（repo直下・git管理外）、Codex は `~/.codex/AGENTS.md`。中身の正本は前者

## Codex で実際に起きた逸脱（2026-09-21・再発させない）

- 指定されていないskill（superpowers:brainstorming）の承認ゲートを持ち込み、委任済みなのに実装前に止まった → 指定skillに無いゲートを足さない
- 他セッションが使っているメインの Unity Editor を `uloop launch -q` で落とした → `moores-wt status` で所有を確かめ、自分の worktree の Editor 以外に触らない
- ユーザーが裁定していない判断を `.decisions/` に自筆した → `.decisions/` はユーザー裁定の蒸留専用
- 実測できなかった RED 手順を `[x]` にした → 未実測は未実測と書く
- `.superpowers/sdd/task-N-report.md`（tracked）を上書きした → plan 専用サブディレクトリを使う

## skillに実行体依存の手順を書くときの書式

本文は意図で書き、手段が割れる箇所だけ次の形で併記する。対応表どおりの読み替えで足りる箇所には何も足さない（二重管理を増やさない）。

```markdown
## 実行体別（Claude Code / Codex）
共通の読み替えは agent-runtime-compat。このskill固有の差分のみ:
- **<工程名>** — Claude: <手段>。Codex: <手段>。
```
