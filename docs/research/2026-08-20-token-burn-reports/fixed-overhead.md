# 固定オーバーヘッドの定量（直近5日, 2026-08-15〜08-20 UTC, 約4.9日）

## 1. 起動時ベースコンテキストの分布と固定費

「起動時ベース」＝各transcriptの最初のassistantメッセージの `input_tokens + cache_creation + cache_read`。

| 区分 | n(起動回数) | median | p90 | mean | 合計tok |
|---|---|---|---|---|---|
| 親セッション | 211 | 58,828 | 75,922 | 45,432 | 9,586,071 |
| サブエージェント | 1,468 | 36,961 | 46,281 | 37,316 | 54,779,558 |
| 合計 | 1,679 | — | — | — | 64,365,629 |

- 起動回数は5日で1,679回（1日あたり約342回）。うち87%(1,468/1,679)がサブエージェント。
- 固定費（cache_creation単価$18.75/M、起動時cache_creation分のみで計算）: 親$115.7 + サブ$764.5 = **$880.2/5日**（≈$179/日）。
  - 参考: ベース全量（in+cc+cr）をcc単価で評価すると$1,206.9（上限見積り）。
- cache_creation単体の分布: 親 median 37,366 / p90 56,891、サブ median 27,309 / p90 33,850。ベースの過半がcache_creation＝毎回書き直されている＝キャッシュヒットで消せる余地。

## 2. ベース構成要素の実サイズ

| ファイル | bytes | 概算tok(bytes/4) |
|---|---|---|
| `moorestech/AGENTS.md` | 14,413 | ~3,600 |
| `moorestech/CLAUDE.md`（`@AGENTS.md`のみ） | 10 | ~2 |
| `moorestech/CLAUDE.local.md`（この環境限定） | 5,290 | ~1,320 |
| `~/CLAUDE.md`（Mac mini運用ドキュメント） | 10,650 | ~2,660 |

- SessionStart hook出力（引数なし実行、書き込みなし）: `.dev-hooks/decisions-index.mjs` **25,719 bytes**（decisions台帳一覧）、`.dev-hooks/beads-prime.mjs` **2,280 bytes**（ready 100件の先頭要約）。decisions-indexが突出して大きい。
- hooks登録数: `~/.claude/settings.json` は12種のhookイベントに計17エントリ（UserPromptSubmit2, Stop2, SubagentStart/Stop各1, PreToolUse2, SessionStart2等）。repo側 `.claude/settings.json` は5イベント8エントリ（PostToolUse3含む）、`settings.local.json` にPreToolUse1（main-worktree-guard）。
- スキル数: repo直下 `.agents/skills` 36個、グローバル `~/.agents/skills` 40個（正本は`~/.agents`一本、repo側はsymlinkミラー）。
- MCPサーバ: repo `.mcp.json` は1件（`rider-debugger`, http）。グローバル`~/.claude.json`にmcpServers登録なし（claude-in-chromeはプラグイン経由でtool一覧にのみ載る＝settings上のMCPカウントには出ない）。

## 3. サブエージェント派遣プロンプトの分布と「読ませるファイル」上位

- 最初のuserメッセージ（派遣プロンプト）長: n=1,468、median 1,049字、p90 2,255字、mean 1,298字、max 6,535字。
- 派遣プロンプト中で参照される`.md`パスの頻度上位は、moores-code-review/pr-independent-reviewのcontext.md/contract.md（各7-9KB、40〜71回参照）と subagent-driven-development の `implementer-contract.md`(7.3KB,112回)/`task-reviewer-contract.md`(9.0KB,72回)。
- サイズ上位（実ファイル）は codex出力の転記: `moores-code-review/runs/*/codex-bughunt.out.md`等が300〜360KB級で複数存在（レビュー系サブエージェントのプロンプトにこれらのパスを読ませる指示が入っており、個別セッションでは巨大な追加読み込みが発生しうる）。`pr-independent-review/SKILL.md`自体も76.8KB。

## 4. 削減余地の見積り

- decisions-index hook出力（25.7KB≈約6-9千tok、日本語比率が高いので4字/tokより詰まる）を毎起動から間引ける場合: 親+サブ計1,679回×該当分＝5日で概算 **1,000万〜1,500万tok規模**（cc単価なら$190〜280/5日）の削減余地。特にサブエージェント（起動の87%）へのhook注入を「本当に設計判断が要るタスクのみ」に絞れれば効果が大きい。
- サブエージェントのベースmedian 36,961tokのうち相当割合がキャッシュ再生成（cc）。オーケストレータ経由の連続dispatchでキャッシュ再利用率を上げられれば、cache_read化により1回あたり実コストを$18.75→$1.5/Mへ大幅圧縮可能（cc→cr転換だけで約12.5倍の単価差）。
- 派遣プロンプトが読ませるcontext.md/contract.md（moores-code-review, pr-independent-review系）は1-9KBで多数のサブエージェントに共通配布されている。テンプレを軽量化すれば1レビュー実行(45-57体)あたり数十〜百KB×体数の削減が見込める。
