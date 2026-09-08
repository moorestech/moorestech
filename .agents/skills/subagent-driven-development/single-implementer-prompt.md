# Single Implementer Subagent プロンプトテンプレート（単一subagent実装モード）

規模ゲート未満の計画を、`opus` 固定のimplementer subagent 1体に丸ごと実装させる際に使う。
作業手順・自己レビュー・報告フォーマットの定型部分は[implementer-contract.md](implementer-contract.md)に
あり、subagentが自分で読む。派遣プロンプトには計画固有の情報だけを書くこと。
**フォアグラウンドで派遣する**（バックグラウンド派遣は孤児化して止まる事故があった）。

```
Subagent (general-purpose, フォアグラウンド):
  description: "Implement whole plan: [PLAN_NAME]"
  model: opus
  prompt: |
    あなたは実装計画 [PLAN_NAME] の全タスクを1体で実装する。

    まず契約ファイルを読む: [SKILL_DIR_ABS]/implementer-contract.md
    作業手順・質問すべきタイミング・エスカレーション方法・自己レビュー・
    報告フォーマットのすべてが書かれている。厳守すること。

    次にブリーフを読む: [PLAN_FILE_ABS]
    計画ファイル全体があなたの要件であり、値はそのまま使うこと。
    `## Requirements`・`## Global Constraints`・`## 判断記録（ADR）`は全タスク共通の制約として扱う。
    実装対象: [TASK_RANGE]（末尾の最終レビュー・PR作成タスクはコントローラーが行うので実装しない）

    moorestech設計ルール: .claude/skills/moores-code-review/references/lens-digest.md（実装前に読むこと）

    作業ディレクトリ: [WORKTREE_ABS_PATH]（隔離worktree。この外で編集・コミット禁止）
    報告ファイル: [REPORT_FILE]（完全な報告をここに書く。返答は契約の15行未満フォーマット）

    ## 進め方（計画全体を担うための追加規律）

    - タスクは計画の順に1つずつ実装し、**タスクごとにコミット**する（subjectに `Task N:` を含める）
    - タスクを1つ終えるたびに報告ファイルへ `Task N: done <sha7> — <1行要約>` を追記する。
      途中で止まっても完了タスクが判別できるようにするため
    - 契約の自己レビューはタスクごとに行い、最後に全体をもう一度通す
    - コンテキストが逼迫し全タスクを終えられないと判断したら、現タスクをコミット可能な状態まで
      戻して報告ファイルに `Task N: partial — <残作業>` を書き、ステータス BLOCKED で返す。
      最終メッセージに完了タスク番号と最後のコミットshaを含める。無理に続けない
    - 計画同士の矛盾・計画と実コードの矛盾を見つけたら推測で進めず NEEDS_CONTEXT で返す

    ## コンテキスト

    [状況説明: この計画がプロジェクトのどこに位置するかの1行、
     ブリーフで気づいた曖昧さに対するコントローラーの解消]
```

**継続派遣（途中失敗時）:** 同じテンプレで `[TASK_RANGE]` を残りタスクに絞り、`## コンテキスト` に
「Task 1〜N は完了済み（commits <base7>..<head7>、報告ファイルの `Task N: done` 行を参照）。Task N+1 から続行せよ」を1行足す。
報告ファイルは同じものに追記させる。継続は最大2回（SKILL.md「継続再派遣（途中失敗時）」）。

**プレースホルダー:**
- `[PLAN_NAME]` — 計画の名前（planファイルのH1）
- `[SKILL_DIR_ABS]` — このスキルディレクトリの絶対パス
- `[PLAN_FILE_ABS]` — 計画ファイルの絶対パス（`scripts/task-brief` は使わない）
- `[TASK_RANGE]` — 実装するタスク番号の範囲（例: `Task 1〜4`。継続派遣では残りのみ）
- `[WORKTREE_ABS_PATH]` — 隔離worktreeの絶対パス
- `[REPORT_FILE]` — `scripts/sdd-workspace` が表示したディレクトリ配下の `single-report.md`
