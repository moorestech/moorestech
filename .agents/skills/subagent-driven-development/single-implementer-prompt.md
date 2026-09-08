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

    計画ファイル冒頭の `> **For agentic workers:**` ブロックと末尾の最終レビュー・PR作成タスクは
    コントローラー向けの指示である。**あなたは subagent-driven-development スキルを起動せず、
    subagent を派遣せず、PR も作成しない。** 実装対象は [TASK_RANGE] のみ。

    moorestech設計ルール: [LENS_DIGEST_ABS]（実装前に読むこと）

    作業ディレクトリ: [WORKTREE_ABS_PATH]（隔離worktree。この外で編集・コミット禁止）
    報告ファイル: [REPORT_FILE]（完全な報告をここに書く。返答は契約の15行未満フォーマット）

    ## 進め方（計画全体を担うための追加規律）

    - タスクは計画の順に1つずつ実装し、**タスクごとにコミット**する（subjectに `Task N:` を含める）
    - **`N` は計画ファイルに書かれた絶対タスク番号**である（継続派遣でも1から数え直さない。
      `[TASK_RANGE]` が `Task 3〜5` なら最初のコミットは `Task 3:` であり `Task 1:` ではない）
    - タスクを1つ終えるたびに報告ファイルへ `Task N: done <sha7> — <1行要約>` を**追記**する
      （Write で置き換えず、既存の内容の末尾へ足す。前の派遣が書いた done 行を消すと
      完了範囲が確定できなくなる）。途中で止まっても完了タスクが判別できるようにするため
    - 契約の自己レビューはタスクごとに行い、最後に全体をもう一度通す
    - **テストの実行単位:** 途中タスクのコミット前は変更対象の焦点テストのみ。全スイートは
      `[TASK_RANGE]` の最終タスクのコミット前に1回だけ実行する（契約の「コミット前に一度だけ
      全スイート」を、このモードではタスク単位でなく**派遣単位**として読む）。継続派遣も同じく
      その派遣の最終タスク前に1回
    - コンテキストが逼迫し全タスクを終えられないと判断したら、**完了したタスクだけをコミット済みに
      し、進行中タスクの未コミット変更は破棄して直前のコミットまで戻す**（中途半端なコミットを
      残さない。継続体はそのタスクを頭からやり直す）。報告ファイルに `Task N: partial — <残作業>`
      を書き、ステータス **PARTIAL** で返す。最終メッセージに完了タスク番号と最後のコミットshaを
      含める。無理に続けない
    - 計画同士の矛盾・計画と実コードの矛盾を見つけたら推測で進めず NEEDS_CONTEXT で返す
      （NEEDS_CONTEXT は継続上限に数えられない。遠慮せず聞くこと）
    - DONE を返す前に `[TASK_RANGE]` 全タスクぶんの done 行が報告ファイルに揃っているか自分で確認する

    ## コンテキスト

    [状況説明: この計画がプロジェクトのどこに位置するかの1行、
     ブリーフで気づいた曖昧さに対するコントローラーの解消]
```

**継続派遣（途中失敗時）:** 同じテンプレで `[TASK_RANGE]` を残りタスクに絞り、`## コンテキスト` に
「Task 1〜N は完了済み（commits <base7>..<head7>、報告ファイルの `Task N: done` 行を参照）。Task N+1 から続行せよ。
前の派遣が書いた `Task N: partial — <残作業>` 行があれば読み、そのタスクは頭からやり直せ」を足す。
報告ファイルは同じものへ**追記**させる（`[REPORT_FILE]` は前回と同じパスにする）。
継続の対象は PARTIAL のみ・最大2回（SKILL.md「継続再派遣（途中失敗時）」）。NEEDS_CONTEXT は回答して再派遣し、この2回に数えない。

**プレースホルダー:**
- `[PLAN_NAME]` — 計画の名前（planファイルのH1）
- `[SKILL_DIR_ABS]` — このスキルディレクトリの絶対パス
- `[PLAN_FILE_ABS]` — 計画ファイルの絶対パス（`scripts/task-brief` は使わない）
- `[LENS_DIGEST_ABS]` — moorestech設計レンズダイジェストの絶対パス（`<repo>/.claude/skills/moores-code-review/references/lens-digest.md`）
- `[TASK_RANGE]` — 実装するタスク番号の範囲（例: `Task 1〜4`。継続派遣では残りのみ）
- `[WORKTREE_ABS_PATH]` — 隔離worktreeの絶対パス
- `[REPORT_FILE]` — `scripts/sdd-workspace` が表示したディレクトリ配下の `single-report.md`
