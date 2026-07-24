---
name: user-simulator
description: |
  ユーザー（katsumi）のシミュレーター。spec/planや設計質問に対し「ユーザーが何を指摘し、どう裁定するか」を
  Fable判事subagent＋sonnet圧縮斥候＋opus反証役で予測し、ユーザーの読む量・答える量を減らす。
  旧spec-plan-reviewの後継（レンズは knowledge/ に降格統合済み）。
  Use when:
  1. brainstorming完了後・specをユーザーレビューに出す直前（reviewモード・毎回必須）
  2. writing-plans完了後・planをユーザーレビューに出す直前（reviewモード・毎回必須）
  3. AskUserQuestionでC型設計質問を出す直前（preanswerモード・毎回）
  4. 「/user-simulator improve <id>」またはシミュレーターの外し（追加指摘・誤検知）の改善時（improveモード）
  5. 「シミュレーターにかけて」「予測レビューして」と言われた時
---

# user-simulator — ユーザーのシミュレーター

このスキル本体は配線だけを持つ。本質は `agents/`（役者の定義）と `modes/`（各モードの手順・出力契約）にある。

## モード判定と配線

| 状況 | 読むファイル | やること |
|---|---|---|
| spec/plan完成→ユーザーレビュー前 | `modes/review/protocol.md` | 手順に従いFable判事を起動し、予測レポートを適用・提示 |
| C型質問を出す直前 | `modes/preanswer/protocol.md` | 判事に予測させ、確信度で前提宣言/注記付き質問に振り分け |
| `improve <id>` 引数つき起動 | `modes/improve/protocol.md` | ハンドオフを読み、同根分析→修正→ゴールデン再演→decisions.md追記 |
| review/preanswerで外しが発生 | `modes/improve/protocol.md`（発行手順） | ハンドオフを即発行しユーザーにコピペ用1行を提示 |

## 判事の起動（review/preanswer共通の3行契約）

Agent tool・**model: fable**（subagentのFable指定はこのスキル限定の例外。decisions.md #2）:

```
Read this : <このスキルdir>/agents/judge.md
mode      : review | preanswer
doc       : <対象の絶対パス>
context   : <4カテゴリ文脈ファイルの絶対パス>
protocol  : <このスキルdir>/modes/<mode>/protocol.md
```

判事が内部で起動する斥候（sonnet）・反証役（opus）の契約は `agents/scout.md` / `agents/refuter.md`。

## 不変の規律（全モード共通）

- 実行結果の採点は必ず `modes/improve/misses.md` に追記（メインセッションの責務）
- 外しは溜めない。その場でハンドオフ発行（`modes/improve/pending/`）
- 知識・エージェント・プロトコルの変更は必ず `decisions.md` に行を残す（追記型）
- 予測をユーザー裁定と偽装しない。docのADRへは出所「シミュレーター予測→ユーザー承認」と書く
