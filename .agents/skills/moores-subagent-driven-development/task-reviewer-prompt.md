# Task Reviewer プロンプトテンプレート

タスクレビュアーsubagentを派遣する際にこのテンプレートを使用する。レビュー手法・
較正・出力フォーマットの定型部分は[task-reviewer-contract.md](task-reviewer-contract.md)に
あり、subagentが自分で読む。派遣プロンプトにはタスク固有の情報だけを書くこと。

**目的:** 1つのタスクの実装が要件と一致しているか（過不足なく）、かつ
良く構築されているか（クリーン・テスト済み・保守可能）を検証する

```
Subagent (general-purpose):
  description: "Review Task N (spec + quality)"
  model: [MODEL — 必須: SKILL.mdのモデル選定に従って選ぶこと。モデル未指定は
         セッションの最も高価なモデルを暗黙に継承する]
  prompt: |
    あなたはTask N: [task name] の実装をレビューする。

    まず契約ファイルを読む: [SKILL_DIR_ABS]/task-reviewer-contract.md
    レビュー手法・較正基準・報告と返答のフォーマットのすべてが書かれている。
    厳守すること。特に返答は契約のコンパクトフォーマットに従い、全文は
    レビュー報告ファイルへ書くこと。

    タスクブリーフ（依頼された内容）: [BRIEF_FILE]

    このタスクを拘束するspec/設計からのglobal constraints:
    [GLOBAL_CONSTRAINTS]

    Implementerの報告（未検証の主張として扱う）: [REPORT_FILE]

    レビュー対象のDiff:
    - Base: [BASE_SHA] / Head: [HEAD_SHA]
    - Diffファイル: [DIFF_FILE]

    レビュー報告ファイル（全文をここに書く）: [REVIEW_FILE]
```

**プレースホルダー:**
- `[MODEL]` — 必須: SKILL.mdのモデル選定に従ったレビュアーモデル
- `[SKILL_DIR_ABS]` — このスキルディレクトリの絶対パス
- `[BRIEF_FILE]` — 必須: タスクブリーフファイル（`scripts/task-brief PLAN N`が
  パスを表示する。implementerが作業したものと同じファイル）
- `[GLOBAL_CONSTRAINTS]` — 計画のGlobal Constraintsセクションまたはspecから
  逐語的にコピーした拘束力ある要件: 正確な値、フォーマット、コンポーネント間で
  述べられている関係性（プロセスルールではない — それは契約ファイルに含まれている）
- `[REPORT_FILE]` — 必須: implementerが詳細な報告を書いたファイル
- `[BASE_SHA]` — このタスク開始前のコミット
- `[HEAD_SHA]` — 現在のコミット
- `[DIFF_FILE]` — 必須: コントローラーがレビューパッケージを書き出したパス
  （`scripts/review-package BASE HEAD`が書き出した一意のパスを表示する。
  このパッケージはコントローラーのコンテキストには一切入らない）
- `[REVIEW_FILE]` — 必須: レビュー報告全文の書き先。ブリーフに合わせた名前
  （`…/task-N-brief.md` → `…/task-N-review.md`）

**レビュアーが返すもの（コンパクト）:** Spec判定（✅/❌ + ❌時は各1行）、
⚠️項目の全文、Task quality判定、Critical/Important各1行、Minor件数、
レビュー報告ファイルのパス。全文所見はレビュー報告ファイルにある。

Fix派遣はspecのギャップと品質所見を同時に対処できる。fix subagentには
レビュー報告ファイルのパスを渡すこと — コントローラーが所見を転記する必要は
ない。修正後の再レビューは両方の判定をカバーする。
