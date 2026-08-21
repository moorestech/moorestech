---
name: moores-grill-with-docs
description: |
  実装前の設計インタビュー。裁定をADR・用語集に残しwriting-plansへ接続する。
  Use when:
  1. 機能追加・挙動の変更・「〜にしたい」「〜作りたい」型の依頼すべて — バグ修正や小修正に見えても、望む挙動を指定する依頼は対象。実装より先に起動する
  2. 設計相談・壁打ち・仕様相談・「これどうしたらいい？」型の相談
  3. 「grillして」「grill-with-docsで」「ブレストして」と言われた時
  対象外: 調査・質問への回答・明示されたクラッシュ/コンパイルエラー修正・レビュー依頼
hooks:
  PostToolUse:
    - matcher: "Write|Edit"
      hooks:
        - type: command
          command: "bash .claude/skills/user-simulator/scripts/shadow-gate.sh track"
  Stop:
    - hooks:
        - type: command
          command: "bash .claude/skills/user-simulator/scripts/shadow-gate.sh stop"
---

Run a `/grilling` session, using the `/domain-modeling` skill.

設計対話中のB判定（設計原則との照合）には [references/moorestech-principles.md](references/moorestech-principles.md) を参照する（旧brainstormingから移設。user-simulatorの知識indexも同ファイルを参照している）。

## HARD GATE（実装着手の禁止）

設計裁定が出揃いADRを書き終えてwriting-plansへ接続するまで、実装スキルの起動・コードの書き込み・プロジェクトのscaffoldを一切行わない。「シンプルすぎて設計不要」という例外は無い — TODOリスト1個・関数1本・設定変更1行でも通す。真に単純なら対話は数問で終わる。短くてよいが省略しない。

## moorestech追加規約

### 1. ADR出所欄（必須）

各ADRの決定（Considered Optionsの採択・却下を含む主要な裁定）には、誰が決めたかを機械可読に記録する:

- **ユーザー裁定**: ユーザーの発言引用または質問への回答に基づく決定。日付つきで記録する。
  例: `出所: ユーザー裁定 2026-07-28「ツールは装備スロットで使いたい」`
- **agent前提**: agentが原則・前例・調査から決めた事項。適用した根拠の実名を添える。
  ユーザーが文書を黙認しても、agent前提はユーザー裁定に昇格しない（免責力を持たない）。
  例: `出所: agent前提（既存GrabInventory同型の先行パターン）`

後日「これは誰が決めたか」を遡れることが目的。出所の偽装（agent判断を裁定済みの顔で書く）は禁止。

### 1.5 裁定の質問設計と記録の忠実性

- **選択肢・プレビューは帰結まで描く。** 新規要素単体で描かず、その決定が影響する既存要素との相互作用（何と重なる・何が消える・何が変わる）を選択肢文とプレビューに含める。帰結が見えない選択肢への承認を、その帰結への裁定として扱ってはいけない。
- **Other自由記述の枠内圧縮禁止。** 自由記述が提示選択肢のどれとも一致しない場合、既決裁定を覆す読みが成立しないかを必ず検討し、採用する解釈を一行で復唱して再確認してから確定する。
- **棄却案は実提示のみ。** ADR・`.decisions/` の棄却案に書けるのは、実際にユーザーへ提示した案だけ。提示していない案を棄却として記録しない（出所偽装の一形態）。
- **拒否・保留された質問を同じ選択肢で再提示しない。** AskUserQuestion が拒否された、または返答が提示選択肢のどれでもない単語・短文だった場合、それは「選択肢集合が合っていない」シグナルとして扱う。次の一手は自由記述で欲しい形（位置・大きさ・何と同じか・何の下か）を取り、それを新しい案として載せ直すか、「現状と同じでよいか」を先に聞く。同じ二択の再提示で Recommended を選ばせて裁定にするのは禁止。
- **プレビュー内の副次情報は裁定に昇格させない。** 選択肢の label/description で問うていない要素（プレビュー図に書き込んだ秒数・文言・寸法・配色）は、その案が採択されても決定文に含めない。含めたいなら別質問にするか、決定文から分離して `agent前提` と書く。
- **原文の語句を解釈した決定は原文を逐語引用する。** 依頼原文の句を選択肢へ変換して裁定を取った場合、出所欄には採択ラベルだけでなく**原文の句を逐語で**並記する（`出所: ユーザー裁定 YYYY-MM-DD 原文「…」→ 選択「…」`）。`.decisions/` のファイル名リンクや「質問で採択」という言い換えは引用の代わりにならない。下流の含意チェック（moores-code-review `core-any-user-intent-fulfillment` §5）は引用文だけを検査対象にするため、引用が無い裁定は転記の歪みがあっても検査不能で素通りする。

較正実例（PR1176・PR1157の意図取り違え事故・レイアウト裁定の特化ガイド）: [references/adjudication-fidelity.md](references/adjudication-fidelity.md)

### 2. 出口の一本化（writing-plans へ直行）

設計・ADRが確定したら、終端状態は「**同一セッションでの writing-plans スキル起動**」のみ。spec等の中間文書は書かない — 要件は会話コンテキスト経由でplan先頭の `## Requirements` セクションへ流れ込む。他スキル・実装への分岐は禁止。
writing-plans 側の user-simulator による plan review（sim-gate配線）は既存のまま維持する。
設計フェーズでは user-simulator を自動起動しない（大きな設計で必要な場合のみユーザーが手動起動する）。

### 3. セッション終了時の自動シャドー採点（shadow-gate配線）

設計成果物（設計doc/plan）を書き終えたら、セッションを終える前に user-simulator の **shadowモード**
（`.claude/skills/user-simulator/modes/shadow/protocol.md`）で自セッションのtranscriptを盲検採点する。
インライン予測はしないので設計対話中の体感遅延はゼロ。採点はセッション末尾に1回だけ行う。

- 発動はfrontmatter hooksの shadow-gate（Stop関所）が機械的に保証する。設計doc書き込みで武装し、
  `user-simulator/datasets/` への格納で解除される（ブロックメッセージに自transcriptパスが入る）
- 予測体は **model: opus必須明示**・1質問1エージェント・バックグラウンド起動可
- 採点・永続化・misses.md記録まで shadowモード手順のとおり実施してからセッションを終了する
