# コンテキスト肥大とやり直しによる消費の定量（直近5日）

対象: `recs.pkl`(assistantターン46,892件、requestId先勝ちdedupe済み) + 生transcript(`~/.claude/projects/**/*.jsonl` 1,699ファイル/806MB)。Opus換算 `(in*15+cc*18.75+cr*1.5+out*75)/1e6`。5日総額 $13,006（前回調査の$14.5kと母数差はdedupe条件の違い）。

## 1. ターンあたりコンテキスト長(in+cc+cr)の階級分布

| bucket | ターン数 | 割合 | cost$ | cost割合 |
|---|---:|---:|---:|---:|
| <50k | 4,533 | 9.7% | 1,177 | 9.1% |
| 50-100k | 19,079 | 40.7% | 3,613 | 27.8% |
| 100-150k | 11,173 | 23.8% | 2,815 | 21.6% |
| 150-200k | 5,212 | 11.1% | 1,786 | 13.7% |
| 200k+ | 6,895 | 14.7% | 3,615 | **27.8%** |

ターン数では200k+は15%だが、コストシェアは50-100k帯と並んで最大の27.8%（トークン単価が同じでも母数=in+cc+crが効くため）。200k+の上位はセッション×役割で見ると：

| session(8桁) | role | n | cost$ | first prompt |
|---|---|---:|---:|---|
| 8c9679f0 | parent | 736 | 536 | subagent-driven-development実行 |
| 98c6cecd | sub(通常対話) | 528 | 208 | pr-independent-review |
| 13473a7c | parent | 290 | 139 | mapmaking-visual-parity plan |
| e4613c38 | sub(通常対話) | 292 | 139 | /model系(短命だが200k+多数) |
| 679c84db | parent | 191 | 113 | /effort系 |
| f97ec9dd | parent | 198 | 109 | subagent-driven-development実行 |
| 22bef2fb | sub(通常対話) | 302 | 108 | pr-independent-review(PR1176) |
| 738f0c32 | parent | 160 | 90 | pr-adjudicated-apply |
| 0cdc450c | parent | 128 | 89 | /clear系 |

SDD実行(subagent-driven-development)とpr-independent-reviewの長寿命オーケストレータに200k+が集中。

## 2. compact/要約とセッション後半コスト

- 実signal(`isCompactSummary:true`、`type:"summary"`)で判定すると、親セッション219件中**6件のみ**でcompact発生(計8回)。文字列grepでの"compact"ヒットは会話内の言及(bd/hooksの説明文)がほとんどでノイズ大。
- 8時間超の長寿命セッション8件で前半/後半の1ターン平均$比較：全8件中7件で後半が悪化（f97ec9dd 0.297→0.584=1.97倍、679c84db 0.378→0.668=1.77倍、875132d8 1.76倍、014c3805 1.70倍、73bdba09 1.64倍）。唯一改善は635c6ca7(0.91倍、compact発生あり=8件中1件)。→ **長時間セッションはcompactせず素通しでターン単価が悪化し続ける**のが支配的パターン。

## 3. やり直し（同一PR/plan複数起動）

`pr-independent-review`(PR番号)・`pr-adjudicated-apply`・SDD plan一致で複数起動グループ9件、合計launch数=104、総額$2,701.7、うち**「最終launchでない=完走に至らなかった」launchの合計 $1,745.4（65%）**。

| group | launches | total$ | 非最終launch合計$ | 主因 |
|---|---:|---:|---:|---|
| PR-review 1176 | 19 | 978 | 505 | LIMIT_DEATH連発(11:50pm/4:50am枠) |
| PR-review 1189 | 19 | 542 | 396 | 同上 |
| PR-review 1145 | 14 | 437 | 437(=100%) | 大半が`OAuth session expired`即死 |
| PR-review 1155 | 3 | 331 | 58 | LIMIT_DEATH1回のみ |
| PR-review 1178 | 15 | 231 | 227 | LIMIT_DEATH + 確認待ち中断複数 |
| PR-review 1179 | 3 | 94 | 94 | weekly limit死 |
| PR-apply 1157/1167/1175 | 各3-5 | 30/29/28 | 0/19/9 | 混在 |

上限死(LIMIT_DEATH: 最終assistant textが"You've hit your ... limit")が非最終launchの**59件**を占め最大要因。PR1145はOAuth失効("Failed to authenticate: OAuth session expired")の連続再起動で$0コストのlaunchが12回並ぶ（すぐ死ぬため課金は少ないがオーケストレーション上のやり直し回数としては最大）。

## 4. ツール結果サイズ（>2000文字のtool_result、5日累計）

| ツール | 件数 | 合計文字数 | 平均文字数 |
|---|---:|---:|---:|
| Bash | 13,361 | 79,060,834 | 5,917 |
| Read | 6,027 | 60,105,259 | 9,972 |

Bash内訳(コマンド種別・末尾コマンドで分類、"other"=多段cd&&チェーン内の未分類実行):
`cat/sed`(2,081件,12.1M) > `grep`(951件,4.6M) > `git diff/log/show`(159件,0.93M) > `ls`(143件,0.56M) > `find`(83件,0.38M) > `gh`(52件,0.28M) > `python`(68件,0.27M) > `uloop`(22件,0.10M)。gh/uloopは件数・総量とも小さく、支配的なのは`cd <長いworktreeパス> && cat/grep/git diff`系の日常調査コマンドとRead。

「サイズ×以降の再送ターン数」概算(そのセッション内で以降何ターン素通しで再送されたか)の上位はほぼ全てReadで、docs/plan系Read1件が最大: 8c9679f0セッションのplanファイルRead(27,507字)が残り831ターンに渡って乗り続け概算再送**2,286万字相当**。次点はagent-aa/agent-a3(worktree内plan、各624/589ターン残存、1,300万字級)、13473a7c(mapmaking plan、507ターン残存、1,279万字級)。**大きなplan/docファイルの初回Readを長寿命セッションの早期に行うと、その後の全ターンに固定コストとして乗り続ける**のが最大の増幅源。

## 結論（8行要約用の根拠）
- 200k+コンテキストのターンはSDD実行・pr-independent-reviewの長寿命オーケストレータに集中し、コストシェア27.8%を占める。
- compactは219セッション中6件しか起きておらず、8時間超セッションの7/8で後半ターン単価が1.6〜2倍に悪化＝compact未実施が主因の疑い。
- やり直し(同一PR/plan再起動)の非最終launch合計は$1,745（対象9グループ内65%）、原因は主にLIMIT_DEATH(59件)とOAuth失効連鎖(PR1145で12回)。
- ツール結果はBash(79M字)とRead(60M字)が支配的。Read側は大きなplanファイルの早期Readがセッション残りターン全体に再送され続け、単発Readで2,200万字相当の累積再送を生むケースが最大。
