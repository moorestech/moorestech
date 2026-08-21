# SDD系セッションの消費解剖

対象8セッション合計 $3,923（8c9679f0 $830 / f97ec9dd $692 / 13473a7c $610 / b80ae1be $609 / 8c9679f0除く残り4本 $1,182）。上位5本を精査。

## 1. 親/サブエージェント内訳（上位5セッション）

| session | total$ | parent-own$ | n_sub | reviewer$ (n) | implementer$ (n) | fix$ (n) |
|---|---|---|---|---|---|---|
| 8c9679f0 | 830 | 563 | 48 | 174 (32) | 35 (6) | 22 (1) |
| f97ec9dd | 692 | 141 | 66 | 338 (43) | 79 (5) | 58 (5) |
| 13473a7c | 610 | 199 | 56 | 205 (24) | 47 (5) | 50 (6) |
| b80ae1be | 609 | 53 | 83 | 405 (52) | 50 (6) | 0 |
| 875132d8 | 531 | 62 | 67 | 287 (36) | 58 (9) | 58 (4) |

**reviewer役が全体の53%（$1,409/$3,272）** — SDD最終工程で走る `moores-code-review` オーケストレータが、lens/investigator/comment-guard等をさらに自分でsubagent fan-outし、それがSDD親のsubagentsディレクトリにフラットに記録される二重階層。implementer/fixは合計19%止まり。モデルはopus/sonnet混在（reviewer系の最大単体は$79〜$392、maxctx 296k〜423k）。

## 2. 無駄パターン定量

**(a) 親コンテキスト肥大**: 8c9679f0はrequestId重複除去後で親turnが832、うち**300k超のturnが685本・$505（parent-ownの90%）**。100kバケット別: 300-400k n=193 $105 / 400-500k n=207 $142 / 500-600k n=183 $158 / 600-700k n=102 $99。コンパクション無しで会話を続けた「後半ターン税」がこのセッションの支出源そのもの。f97ec9dd/b80ae1be/875132d8は前半→後半でコストが概ね2倍（例: f97ec9dd $72→$147）、13473a7c はサブエージェント委譲が効いていて横ばい。

**(b) ファイル重複読み**: 上位5本合計でmoores-code-reviewの`runs/*/context.md`と`contract.md`が突出（各run 25〜61回読まれる）。実行ごとに新runディレクトリが切られるため生成物自体は使い回されないが、同一runの`context.md`をレビュー系subagentが数十体それぞれ読み直している（設計上意図された共有だが、読み直しコストは無視できない規模）。`implementer-contract.md`は32回。

**(c) no-opポーリング**: 当初raw集計で8c9679f0に「596turn=36%がecho/sleep/ListAgents単独」と出たが、これはログの重複行（同一requestId再記録）による誤検知。requestId dedupe後は**実質1turnのみ**で、no-opポーリングはこの5本では無視できる水準（既知の$1,122は他ファミリー由来）。

**(d) 失敗→やり直し**: retry/resume系ラベルのsubagentが b80ae1be 13体$172、f97ec9dd 10体$198、13473a7c 6体$44（8c9679f0/875132d8は0）。b80ae1beの最大2件は「Resume moores-code-review orchestrator」$107と「Apply adjudicated review fixes」$80＝レビューが一度中断・再開しただけで$187。f97ec9dd最大は「moores-code-review orchestrator (retry)」単体$159・maxctx 423k。

## 3. 削減余地の見積もり

- 8c9679f0型のコンテキスト肥大: このセッション単体で$505が300k超turnに集中。親が長時間直列作業せず適時委譲/compactしていれば7割は削減可能 → **約$350**
- retry/resume起因の無駄: 3セッション合計で直接特定できた分 → **$414**
- 上位5本合計の理論削減余地: 約**$760〜900（$3,272の23〜28%）**。reviewer役自体（$1,409）は設計通りの多層レビューで即削減対象ではないが、retry/resumeが起きなければ発生しない上乗せ分がここに含まれる。

根拠transcript: 8c9679f0, f97ec9dd, 13473a7c, b80ae1be, 875132d8（いずれも moorestech リポジトリ配下）。
