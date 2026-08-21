# Implementer Subagent プロンプトテンプレート

implementer subagentを派遣する際にこのテンプレートを使用する。作業手順・
自己レビュー・報告フォーマットの定型部分は[implementer-contract.md](implementer-contract.md)に
あり、subagentが自分で読む。派遣プロンプトにはタスク固有の情報だけを書くこと —
派遣プロンプトはコントローラーのコンテキストに残り続けるため、定型文の展開は
コンテキストの無駄である。

```
Subagent (general-purpose):
  description: "Implement Task N: [task name]"
  model: [MODEL — 必須: SKILL.mdのモデル選定に従って選ぶこと。モデル未指定は
         セッションの最も高価なモデルを暗黙に継承する]
  prompt: |
    あなたはTask N: [task name] を実装する。

    まず契約ファイルを読む: [SKILL_DIR_ABS]/implementer-contract.md
    作業手順・質問すべきタイミング・エスカレーション方法・自己レビュー・
    報告フォーマットのすべてが書かれている。厳守すること。

    次にタスクブリーフを読む: [BRIEF_FILE]
    あなたの要件であり、値はそのまま使うこと。

    作業ディレクトリ: [WORKTREE_ABS_PATH]（隔離worktree。この外で編集・コミット禁止）
    報告ファイル: [REPORT_FILE]（完全な報告をここに書く。返答は契約の15行未満フォーマット）

    ## コンテキスト

    [状況説明: このタスクがプロジェクトのどこに位置するかの1行、
     前タスクからのインターフェースと決定事項、
     ブリーフで気づいた曖昧さに対するコントローラーの解消]
```

**プレースホルダー:**
- `[MODEL]` — 必須: SKILL.mdのモデル選定に従う
- `[SKILL_DIR_ABS]` — このスキルディレクトリの絶対パス
- `[BRIEF_FILE]` — `scripts/task-brief PLAN N`が表示したパス
- `[WORKTREE_ABS_PATH]` — 隔離worktreeの絶対パス
- `[REPORT_FILE]` — ブリーフに合わせた名前（`…/task-N-brief.md` → `…/task-N-report.md`）
