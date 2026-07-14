---
name: moores-code-review
description: |
  moorestech固有の設計レンズレビュー。実際のPRレビュー指摘（PR978/987/988/996/997/1000ほか）から抽出した
  設計レンズ群（ドメイン境界・サーバー状態同期3点セット・DataStore分離・マスタデータ防御・型構造・前例一致）を、
  決定論スクリプト＋条件発火サブエージェントで並列実行し、実コード照合のうえ統合するスキル。
  all-code-review（汎用）の機構を抽出したmoorestech特化版。汎用レビューの代替ではなく、moorestech設計規約の専用網。
  Use when:
  1. moorestechでPR作成前・マージ前のレビューを行う時（pr-create前に必ず1パス）
  2. subagent-driven-development の最終ブランチレビューを行う時
  3. 「moores-code-reviewで」「moorestechレンズでレビュー」「設計レンズを通して」と言われた時
  4. all-code-review と併走させる時（汎用系はall-code-review、moorestech設計規約は本スキルが担当）
---

# moores-code-review

moorestechの実レビュー指摘から抽出した設計レンズを、**決定論チェック → 条件発火レンズ群の並列実行 → 実コード照合 → 自動適用 → 報告** の順で回す。

## 実行順序（厳守）

> **① 決定論チェック（スクリプト） → ② レンズselector → ③ 発火レンズ＋前例一致レンズを1メッセージで並列起動 → ④ 回収・実コード照合・重複排除 → ⑤ 機械的修正を自動適用＋コンパイル → ⑥ 報告＋設計判断のみAskUserQuestion（末尾集約）**

## Step 1: レビュー対象と4カテゴリcontextを確定する

1. **作業範囲を特定** — このセッションで生成・変更した成果物をコミット範囲・staged・unstagedから確定し、統合unified diffを `/tmp/moores-review-patch-<ts>.diff` に書く（PATCH_PATH）。
2. **4カテゴリcontextを書く** — `/tmp/moores-review-context-<ts>.md`（USER_PROMPT_PATH）に以下を埋める。埋め忘れるとレンズがfalse-positiveを量産する:
   - **目指す（ゴール）** / **目指さない（非目標）** / **許容するトレードオフ** / **尊重すべき制約**
   - 自分の判断は「（自分の判断として）」と明記し「ユーザー合意済み」と偽装しない。

## Step 2: 決定論チェック ①

```bash
python3 .claude/skills/moores-code-review/scripts/deterministic_checks.py "<PATCH_PATH>" --repo-root "$(pwd)"
```

- `confirmed`（Defaultフォールバック・PacketResponse直下の非プロトコル・10ファイル/200行超・partial・try-catch）は検出正確・裏取り不要。Criticalとして統合に直接載せる。
- `candidates.event_tag_sync`（新規EventTagのクライアント購読漏れ候補）は server-state-sync レンズ、`candidates.schema_optional_true`（optional新設候補）は master-data-defense レンズの裏付けデータとして扱う（正当な例外がありうるためレンズが裁定）。
- `~/.agents/skills/all-code-review/scripts/deterministic_checks.py` が存在すれば併走させてよい（汎用規約の機械判定）。無ければスキップ（黙って縮退しない、報告に1行記す）。

## Step 3: レンズselector ②

```bash
python3 .claude/skills/moores-code-review/scripts/select_lenses.py "<PATCH_PATH>"
```

出力は `レンズ絶対パス<TAB>モデル` のTSV。`lenses/*.md` 先頭YAMLの `paths`（変更ファイルパス正規表現）・`extensions`・`keywords` はAND結合（各グループ内はOR、空グループは制約なし）、`always: true` は無条件発火。

## Step 4: レンズを並列起動する ③

**1メッセージ内で並列に** 次を全部Agent起動する（順次起動は禁止）:

1. **各発火レンズ** — selectorのTSVどおりの `model` で、3行契約:
   ```
   Read this : <レンズの絶対パス>
   Patch path : <PATCH_PATH>
   User prompt : <USER_PROMPT_PATH>
   ```
2. **前例一致レンズ**（`lenses/precedent-alignment.md`・always発火）も同じ契約。発火レンズが0件でもこれだけは必ず起動する。

各レンズは `Critical: あり/なし` + `修正方針: - <ファイル:行>: <何を直すか>` を返す。全部揃うまでStep 5へ進まない。

## Step 5: 実コード照合・重複排除 ④

`references/integration-rules.md` に従う。要点:
- レンズのCriticalは適用前に該当箇所をReadして裏取り。事実誤認は破棄（件数だけ最終報告に残す）。
- 複数レンズ一致は1件に統合し「N系統一致（高確度）」。
- 決定論confirmedは裏取り不要。

## Step 6: 確定修正の自動適用＋コンパイル ⑤

- 具体名（ファイル/クラス/メソッド）と修正方針が挙がっていて選択の余地が無い機械的修正は、確認を挟まず自動適用する（デフォルト動作）。
- 設計判断（複数の妥当な選択肢・スコープ影響・アーキテクチャ変更）は適用せずStep 7へ保留。
- .csを修正したら `uloop compile --project-path ./moorestech_client` を実行し、エラー0を確認する。

## Step 7: 報告＋AskUserQuestion ⑥

1. 統合報告: Critical/Warning件数、各指摘の出所（決定論/レンズ名/N系統一致）、適用した修正、コンパイル・テスト結果。raw出力をそのまま貼らない。
2. 保留した設計判断だけをAskUserQuestionで選択肢付き一括提示（0件ならスキップ）。
3. `/tmp` の一時ファイルを削除する。

## レンズ一覧（lenses/）

| レンズ | 担当（由来PR） | 発火条件 |
|---|---|---|
| domain-boundary | 汎用基盤へのドメイン語彙漏れ・Update()ポーリング・共通サービス委譲漏れ（978/1000） | Game.Block/Gear/EnergySystem等の.cs |
| server-state-sync | サーバー状態同期3点セット・Applier禁止・ハンドシェイク順序（988） | Server.Protocol/Server.Event/Client.Network |
| datastore-access-separation | Lookup/Mutation分離・static変更露出（988） | DataStore系キーワード |
| master-data-defense | optional濫用・??フォールバック・ローダープリフィル（978） | VanillaSchema/Core.Master/BlockTemplate |
| type-driven-structure | 共用体struct・god-context・N択1役割の型排除・DTO配置（987/996/997） | struct/Context/interface系キーワード |
| redundant-member-duplication | バッキングフィールド＋素通しプロパティの二重保持・同値別名メンバーの排除（sonnet） | プロパティ/フィールド宣言を含む.cs |
| implicit-cardinality-assumption | マスタ/ドメイン集合の単一要素決め打ち（`[0]`/`First`）で暗黙に単数を仮定（1017） | MasterHolderを読む.cs |
| precedent-alignment | 前例一致（全PR横断・役割で前例を選ぶ） | 常時 |

## 有効性の測定（eval/）

- **リプレイ評価**: `eval/fixtures.tsv` + `eval/make-fixture.sh` でレビュー当時のdiffを再生成し、`eval/expected-findings.md` の期待検出（22指摘）と突合する。レンズ・スクリプトを変更したら必ず1回流す。手順は `eval/README.md`。
- **前向きログ**: マージ済みPRごとに `eval/log.md` へ「人間指摘数・分類・ハーネス事前検出の有無・却下数」を記録する（手動運用。手順は `eval/README.md` の「指摘の反映手順」）。

## Gotchas

- **4カテゴリcontextを埋めないとレンズが誤検知する** — 空contextは「合意なし」と解釈され既定Criticalが出る。
- **レンズは実コード照合が前提** — サブエージェントはpatchとcwdを読むが、統合側でも裏取りしてから適用する。
- **AskUserQuestionは末尾だけ** — 確定修正の途中で割り込まない。
- **レンズ本文の更新は指摘駆動で行う** — 新しい人間レビュー指摘が出たら、その指摘を防げたはずのレンズ（無ければ新レンズ）へ実例として追記し、`eval/expected-findings.md` にも行を足す（手順は `eval/README.md`）。
