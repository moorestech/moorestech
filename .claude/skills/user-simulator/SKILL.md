---
name: user-simulator
description: |
  spec/planをユーザーレビューに出す直前や、ユーザーに設計判断を尋ねる直前に、
  「ユーザーなら何を指摘し、どう答えるか」を先回りで予測して適用するスキル。
  Use when:
  1. brainstorming完了後・specをユーザーレビューに出す直前（reviewモード・毎回必須）
  2. writing-plans完了後・planをユーザーレビューに出す直前（reviewモード・毎回必須）
  3. ユーザーにしか決められない設計質問（AskUserQuestion）を出す直前（preanswerモード・毎回）
  4. 「/user-simulator improve <id>」で起動された時、またはシミュレーターの外し（追加指摘・誤検知）が起きた時（improveモード）
  5. 「シミュレーターにかけて」「予測レビューして」「spec-plan-reviewで」と言われた時
---

# user-simulator

このファイルは配線だけを持つ。スキルの素性と動き方は `agents/judge.md`、各モードの手順は `modes/` が正。

## モード判定

| 状況 | 読むファイル |
|---|---|
| spec/plan完成→ユーザーレビュー前 | `modes/review/protocol.md` |
| ユーザーへの設計質問を出す直前 | `modes/preanswer/protocol.md` |
| `improve <id>` 引数つき起動 | `modes/improve/protocol.md` |
| review/preanswerで外しが発生 | `modes/improve/protocol.md`（発行手順） |

## 判事の起動（review/preanswer共通）

Agent tool・**model: fable**（subagentのFable指定はこのスキル限定の例外。decisions.md #2）:

```
Read this : <このスキルdir>/agents/judge.md
mode      : review | preanswer
doc       : <対象の絶対パス>
context   : <4カテゴリ文脈ファイルの絶対パス>
protocol  : <このスキルdir>/modes/<mode>/protocol.md
```

## 不変の規律（全モード共通）

- 実行結果の採点は必ず `modes/improve/misses.md` に追記（メインセッションの責務）
- 外しは溜めない。その場でハンドオフ発行（`modes/improve/pending/`）
- 知識・エージェント・プロトコルの変更は必ず `decisions.md` に行を残す（追記型）
- 予測をユーザー裁定と偽装しない。docのADRへは出所「シミュレーター予測→ユーザー承認」と書く
