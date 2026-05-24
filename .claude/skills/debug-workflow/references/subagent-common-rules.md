# サブエージェント共通ルール

このファイルは debug-workflow の全観点サブエージェントが最初に読む共通契約。個別テンプレートより優先する。

## 0. 中核契約

debug-workflow のサブエージェントは **観点別の仮説生成器** である。code-reviewer の reviewer とは契約が異なる:

| 項目 | code-reviewer | debug-workflow サブエージェント |
|---|---|---|
| 早期終了 | 可 (`⏭️ skip`) | **禁止 — 最低 1 件の仮説を必ず生成** |
| Severity | 深刻度 | 確度の指標 (Critical=高 / Warning=中 / Info=低) |
| Critical の意味 | 「修正必須」 | 「Step 4 検証対象の最有力候補」 |
| 修正コード | 必要に応じて提案 | **絶対に書かない (Step 5 を侵食しない)** |
| もっともらしい誤仮説の扱い | 削除 | Info で残す (網羅性優先) |

## 1. 早期終了禁止

自分の観点が一見スコープ外でも `[applicable: no]` / `skip` / `pass` は出さない。必ず最低 1 件の仮説を生成する。Severity は Info で構わない。

「無理にこの観点で症状を再解釈するなら何が言えるか」を書き出す。観点が浅く適用しか出来ない場合でも、それは **観点が浅く適用されている事実そのもの** を Info 仮説として残す価値がある。

## 2. 修正コード禁止

Edit / Write による .cs / .py / .ts などのソースファイル変更を **絶対に行わない**。生成物は仮説テキストのみ。

「修正案」「fix」「patch」セクションを出力に含めない。`Recommended log placement` (Step 4 用デバッグログ) のみ許可される具体提案。

サブエージェントが修正コードまで踏み込むと、メイン側が Step 4 (ログ検証) ゲートをスキップして Step 5 (修正) に直接飛ぶ誘惑を生む。

## 3. 推測自体は許容、ただし Severity で区分

`Claim` と `Evidence` は可能な限り file:line で引用する。引用が薄い仮説も削除せず Info で出す:

- **Critical**: Evidence 3 件以上 + 既存成功経路との差分引用 + Falsification 明確
- **Warning**: Evidence 2 件以上 + Falsification 明確
- **Info**: Evidence 1 件以下、または Falsification 不明確、または観点周縁の仮説

## 4. Falsification 欄必須

各仮説に「このログがこう出たら仮説は棄却される」を事前固定する。

書けない仮説は Info 降格 (確証バイアス対策)。「ログを見たら何かわかるはず」式の漠然とした提案は不可。

## 5. 既存成功経路の探索努力義務

`working-precedent-hypothesis` 以外の観点でも、自分の観点で類似した「動いている経路」を `grep` / `Glob` で 1 回は探す努力をする。発見できたら Evidence に file:line を含める (Severity を Critical に上げる材料になる)。

## 6. 未読領域の自発展開義務

「コードに書かれていない → そうなっていない」という推論は禁止。

設定が prefab YAML / ScriptableObject / 設定ファイル / 外部 API ドキュメント / git 履歴で行われる可能性がある場合、**実際に開いて引用する** ことで立証する。`unread-evidence-hypothesis` 以外の観点でも、自分の Claim が「コードに書かれていないこと」に依存していたら、その立証責任を果たす。

立証できなければ Info 降格。

## 7. 「もっともらしいが根拠薄」も削除しない

code-reviewer は false-positive を削除するルールだが、debug 用途では網羅性が優先。根拠薄でも Info として残す。Step 4 が空振った時の次候補になる。

ただし「観点と症状の関連が全く見えない、無理にこじつけたもの」は 1 件のみに留め、複数並べない (信号/雑音比悪化を避ける)。

## 8. 出力の簡潔さ

- 1 仮説あたり 200 語程度を目安に
- メタコメント (「ここでは〜と思います」「私の見解では」) は不要
- 仮説の構造化を優先 (`hypothesis-output-format.md` の項目を必ず埋める)

## 9. 自分の Category 名を必ず書く

集約ロジックが Category 名でグループ化する。各仮説の `Category` 行に自分のサブエージェント名 (`data-input` / `state-lifecycle` / `dependency-event` / `boundary-conversion` / `working-precedent` / `recent-change` / `unread-evidence`) を必ず記載する。
