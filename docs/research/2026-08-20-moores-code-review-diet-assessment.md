# moores-code-review ダイエット再検討（2026-08-20・bd moorestech-n9e5）

目的: 「レビュー1本 45〜57体・opus 30体・$470〜540/本（実装と同額以上）」（`2026-08-20-token-burn-reassessment.md` §6 ラダー#6）に対し、**体数削減が本当に効くのか**を実測で判定する。過去に何度か議題化して「現状維持」で終わっているため、今回は (a) 1体あたり費用の内訳と (b) 08-16ダイエット後の採用実績を先に測った。

計測入力・スクリプト・生集計は `../moorestech_logs/harness/moores-code-review/analysis/2026-08-20-diet-reassessment/`（`agent_cost.py`／`agent_cost_<session>.md|json`／`adoption_post_0816.md`／`tally.py`）。

## 1. 1本の費用はどこに消えているか（PR1176 run `22bef2fb` / PR1175 run `98c6cecd`・Opus換算・requestId max重複排除）

| 区分 | 体数 | PR1176 $ | PR1175 $ | 1体平均 $ | 平均ターン | 平均最大ctx | 実単価 $（sonnet×0.2） |
|---|--:|--:|--:|--:|--:|--:|--:|
| sonnetオーケストレータ | 1 | 194 | 240 | 194〜240 | 590〜625 | 251k〜313k | 39〜48 |
| reviewer（opus 21〜22 / sonnet 5〜6） | 27 | 186 | 164 | 6.1〜6.9 | 25〜27 | 106k〜120k | 149〜174 |
| lens（opus 4 / fable 1 / sonnet 1〜4） | 6〜9 | 39 | 40 | 4.5〜6.5 | 17〜25 | 102k〜117k | 32〜36 |
| investigator（sonnet） | 6 | 35 | 27 | 4.5〜5.9 | 20〜23 | 91k〜109k | 5〜7 |
| integrator（opus） | 1 | 18 | 17 | — | 30〜33 | 240k〜278k | 17〜18 |
| Fable全般 | 1 | 10 | 12 | — | 23〜32 | 175k〜199k | 10〜12 |
| post-check / digest / design | 3〜4 | 20 | 23 | — | — | — | 7〜17 |
| **合計（Claude側）** | 46〜48 | **503** | **524** | 10.9 | 35〜38 | 112k〜125k | **270〜301** |

費用の構造（両run同形）: 起動時ベース（system prompt＋tool定義＋skill一覧≈33k tok）**5%**／ベースの毎ターン再読 **17%**／出力 **12%**／**読み込みの累積 65%**。
reviewer 1体の典型は「Bashで `sed -n`/`cat` を40〜50回、12〜28万字を読み、25〜27ターンかけてctxが12万（上位は18〜21万）まで伸び、毎ターンそれを cache_read で再送」。つまり **費用は「体数」より「1体のターン数×ctx」と「opus既定」で決まる**。SessionStart hook（decisions-index 25KB）はサブエージェントには注入されていない（94体の transcript 全てに `<decisions-ledger>` 不在）ので、再計測ドキュメントの「ラダー#4」はレビュー体には効かない。

## 2. 08-16ダイエット後の採用実績（完走26 run: moores-code-review 16・pr-independent-review 10）

| | 採用 | うち swarm（lens/reviewer）単独 | 破棄 |
|---|--:|--:|--:|
| 合計 | 272 | **143（53%）** | 71 |

| 系統群 | 起動 | 共有計上の採用 | **単独採用** |
|---|--:|--:|--:|
| opus reviewer 21本 | 386 | 572 | **45** |
| sonnet reviewer 8本（08-16降格組） | 101 | 18 | 4 |
| opus lens 5本 | 71 | 94 | 6 |
| sonnet lens 6本（08-16降格組） | 42 | 4 | **0** |
| Fable全般 | 27 | 45 | 4 |
| Codex（3本→1系統） | 18 | 30 | 1 — **pr-independent-review 10本は全て0所見**（`codex` 不在誤診×7・401×2） |
| investigator 3観点 | 69体 | 41 | 1 |
| post-check convention-guard / rationale-guard | 21 / 18 | 16 / 4 | 14 / 4 |
| 決定論・verifier・gate | — | — | dead_member ゲートは **24/26 run で縮退**（review worktree に dotnet 不在） |

採用ゼロ（起動あり）: lens `redundant-member-duplication`(10)・`datastore-access-separation`(4)・`set-once-dependency-injection`(4)。起動ゼロ: reviewer `ts-component-colocation`・`ts-dev-production-separation`。ほぼゼロ: lens `implicit-cardinality`(1/10)・`server-state-sync`(1/8)・`master-data-defense`(2/6)、reviewer `any-user-intent-fulfillment`(2/27・単独0)・`cs-unidirectional-flow`(4/14・単独0)・`cs-unity-convention`(1/7)。**全て08-16のsonnet降格組**で、ダイエットの狙いは当たっていた。opus reviewer のうち単独採用0は `cs-architecture-lifecycle`・`ts-react-antipattern`・`ts-result-state-propagation`・`ts-default-resolution-ownership`・`ts-speculative-abstraction`（ただし共有計上14〜18＝合議の票としては効いている）。

定型反復の無駄（系統を削らず直せる）: `[agent前提]` suppressed の復帰が全26 runで発生／Fable「実害なし」の実コード照合での覆り9件／investigator の事実誤り12件（pr-1155-r2 の二重起動stale copy含む）／決定論 `mutable_auto_property`・`passthrough_property` の純スタイル偽陽性5件／同一の到達不能主張（研究画面ホイール装備切替）が5系統×2回破棄。

## 3. 判定

1. **「体数削減」は弱いレバー。** 金を食っているopus reviewerが採用の主役（swarm単独53%）で、採用ゼロ組は既にsonnet（1体 実単価≈$1.2）。ゼロ組を全部落としても 1本あたり **Opus換算 −$20〜35（vltk後ベース$310の6〜10%）・実単価 −$5〜7**。やるなら衛生（起動ゼロ2本の削除・0/18の3レンズ）として、規模は小さい。
2. **最大はオーケストレータ空転 $194〜240**（bd `moorestech-vltk`・別件）。これが済むと1本≈$310で、その**72%が swarm（reviewer＋lens）**。
3. **swarmを削らずに減らす唯一の構造的レバーは「1体のターン数×ctx」。** 読み込み65%は各体が独立に同じ変更箇所をBashで掘っているから。オーケストレータ（またはスクリプト）が「変更後ファイル全文＋囲む関数」を1ファイル（`expanded.md`）に前展開し、reviewer/lensは**それを1回Readしてから**外側（呼び出し元・前例）だけBashで当たる規律にすれば、27ターン→10ターン級で **swarm費用 −40〜50%（−$90〜110/本）** が見込める。探索が本体の `callsite-tracer`・`removed-invariant`・`precedent-alignment` は除外。採用率が落ちないかは次の2 runで replay比較（08-16プレイブック原則8）。
4. **削るより直す方がROIが高い壊れ方が2つ**: Codexが無人レビューで10本連続0所見（封じ込めPATHに `codex` 不在の誤診＝memory既知＋OAuth 401）、dead_member ILゲートが24/26 runで縮退（review worktreeにdotnet不在）。08-16監査ではCodexが採用40で最多系統だった。安い系統が止まったまま高い swarm が肩代わりしている。
5. opus→sonnet 追加降格（単独採用0のopus 5本＋lens hardcoded-content-enumeration）は実単価 −$25/本だがOpus換算では0。5時間枠の重みが単価比例ならば効くが、replay検証込みで工数が要るため今回は見送り候補。

推奨順: vltk（空転）→ #4（Codex・dotnetの復旧）→ #3（前展開の実験）→ #1（衛生）。#5は保留。

- rundir: analysis/2026-08-20-diet-reassessment/
