---
name: postmortem
description: 事後検証(ポストモーテム)。「体制・工程があったのになぜ防げなかったか」を一次資料で実測し、要するに何がダメだったか・でどうするかを一言で答え、再発防止策は当時の実環境で検証する。対象はレビューすり抜けのコードバグに限らず、意図の取り違え・転写事故・運用事故・エージェントプロセスの失敗など全般。Use When — 「ポストモーテムして」「なんでこうなったのか検証して」「なんですり抜けたの」「これは体制の失敗？しょうがなかった？」「再発防止策を考えて」。復旧・修正そのものは対象外(済んでから使う)。
---

# postmortem — 事後検証

「体制があったのになぜ防げなかったか」に証拠つきで答え、体制への手当てを最小介入で設計し、当時の実環境で効くことを確かめる。**ユーザーは本文を読まない前提で書く** — 判断材料になるのは末尾の「要するに」だけ。本文は自分の整理のために書いてよい。

調べ方・裁定の枠組み・対策の作り方は縛らない。事案に合わせて自分で組み、下の「使える道具」は選択肢として使う。縛るのは次の 7 つだけ。

## 硬い規則(7 つ・すべて過去の差し戻しから)

1. **裁定を出すまで適用しない(HARD GATE)。** 裁定と「要するに」を提示して GO を得るまで、ファイル編集・コミット・PR・push を一切しない。裁定と対策を同ターンに混載しない(先に対策を出すと裁定が対策に引っ張られる)。PR 作成・push はユーザーの明示指示があるときだけ。
2. **未検証は冒頭に明示する。** 対策 N 件中 M 件が未検証なら、報告の 1 行目に `⚠ 対策 N 件中 M 件は未検証` を置く。限界節や本文中の一文に埋めない。「再発防止できた」と言えるのは、対策が事故当時の工程に届く置き場にあり、当時の入力で、事故を止めた出力経路(Critical・再質問・plan の項目等)に到達したときだけ。
3. **検証の入力は事故当時の実環境に寄せる。** 当時の実 diff 全体・context・当時版スキル(`git show`)・同じモデル・cwd は導入コミット固定。単離した断片(当該コミットだけ)で合格を宣言しない — 注意が集中して検出力しか測れない。実環境から離した点があれば、その点と理由を検証行に書く。
4. **検証は最小本数。** 合否を決めるのに要る agent だけ起こす(既定 n=1)。旧版対照・誤爆側・n≥2 は「この事案で何の情報が増えるか」を 1 行言えるときだけ。何本回すかは提示して選ばせる。表を埋める目的で agent を起こさない。
5. **一次資料で実測し、探索は logs repo から。** 「〜したはず」で語らない。どの工程がどの入力で何を見たかを transcript・git・実行記録で確定する。セッション考古学はプロジェクトの logs repo(moorestech なら `../moorestech_logs`、README の手順: git blame → コミット↔セッション対応表 → claude/codex 生ログ)から。`~/.claude/projects/` 直読みは同一マシン・進行中セッションの近道としてのみ可(codex セッションを取りこぼす)。transcript 全文読み・台帳の深掘りは read-only サブエージェントへ委譲する(本体 context を焼かない・先入観を混ぜない)。
6. **過去事案の台帳と照合し、締めに追記する。** 裁定の前に `registry.md`(置き場所は [references/recurrence-registry.md](references/recurrence-registry.md))を機構語で grep し、初発か系統再発かを裁定に含める。系統再発なら前対策の不発理由を確定し、新設より既存対策の修理を優先する。締めに 1 事案 1 ブロックを追記する — 省くと次回の照合が盲目になる。
7. **問いの主語はユーザー原文。** ユーザーが最重要と言った問い(「なぜ計画段階で出せなかったのか」等)を裁定の中心に置く。他セッション経由の依頼文・自分が立てた枠組みは文脈扱いで、ユーザーの問いを上書きしない。事実関係が揺れているなら裁定より先に事実を確定する。修正そのものはこのスキルの外(別セッション・別ブランチ)。

## 出力契約

裁定提示(GO 前)と最終報告の両方で、末尾に必ず次を置く。100 字以内は厳守 — 超えるなら本質を掴めていない。

```
要するに何がダメだったか: <100 字以内。欠陥の本質クラスを一言で>
でどうするか: <100 字以内>
変更箇所: <ファイル> — <なぜ必要か 1 行>(1 行ずつ)
検証: <対策ごとに 合(根拠 1 行) / 否(致命度) / 未検証(理由)。実環境から離した点があれば明記>
```

「要するに何がダメだったか」は裁定ゲートの時点で出す — ユーザーが欠陥クラスを言い直す(「本質はマスタ値のハードコードでは」)のはここで起きるので、対策の前に直してもらう。ユーザー側の要因(原文の曖昧さ等)があれば、その 1 行も添える。

## 一次資料の在り処(moorestech の例。他 repo は同じ役割のものを探す)

**logs repo `../moorestech_logs`** — 作業前に同 repo の README を読む(レイアウト・考古学手順・消失事故の落とし穴)。

- `map/commit-sessions.tsv` — sha → agent / session_id。コミットとセッションを結ぶ唯一の結合層。`grep <sha>`
- `claude/<slug>/<session>.jsonl` と同名ディレクトリの `subagents/`(subagent transcript)・`tool-results/` — subagent を含めないと集計が約 1/3 に過小。AskUserQuestion の提示全文とユーザー回答は `tool_use` / `tool_result` に verbatim で残る
- `codex/rollout-*.jsonl` — codex セッション(codex-implement・codex-audit 等)はここにしか無い。`grep -rl <session_id> codex/`
- `harness/postmortem/registry.md` — 再発照合の台帳。`harness/postmortem/<日付-slug>/` — 過去事案の fixture・被験体出力・results.md(検証の型の実例)
- `harness/pr-independent-review/runs/pr-<n>/` — 独立レビューの実入力そのもの: `context.md`・`contract.md`・patch・`agents/`(全観点の出力)・`adjudications.json`(ユーザー裁定)・`digest.md`・`codex-*.final.md`。**フルスケール検証の素材はここ**。`records/pr-<n>.md` はシャドー台帳、`improvement-queue.md` は改善キュー
- `harness/moores-code-review/records/`・`eval-log.md`、`harness/moores-grill-with-docs/`(backtest・questions)、`harness/user-simulator/`(datasets・improve) — 各工程の実行記録
- `beads/issues.jsonl` — bd のスナップショット(grep 用。正本は `bd show`)

**コード repo**

- `.decisions/` — ユーザー裁定の蒸留(決定・棄却案・理由・日付)。**事故がここに焼き込まれていないか**(誤解釈が「裁定済み」として保護されていないか)、棄却案は実際に提示された案か、を必ず見る。当時版は `git show <sha>:<path>`
- `docs/adr/`・`docs/superpowers/plans/`(plan と Self-Review)・`docs/research/` — 計画工程の成果物。ADR に対する QA は ADR の誤りを検出できない(自己参照)
- `.agents/skills/<skill>/` — 工程の定義。**発火条件・入力源は当時版をファイルで読む**(`git show <当時sha>:.agents/skills/...`)。moores-code-review の observation(lenses/reviewers/verifiers/post-checks)・`references/integration-rules.md`・`output-contract.md` が「誰が何を見る契約だったか」の正本
- `bd show <id>`(description / notes に裁定・handoff・LEARN)、`bd dep tree <id>`、`bd list --all | grep`。`.decisions` は bd から `[[ファイル名]]` で参照される
- `git log -S"<核心シンボル>" -- <path>`(導入コミット)、`git log --grep=<skill名>`(レビュー実施コミット)、`git show <sha> --stat`(そのレビュー回が見たファイル集合)

**外部**

- 裁定サイト `https://review.moores.tech/pr/<番号>` — 独立レビューのダイジェストとユーザーの裁定コメント(「後でポストモーテム」と書かれた件はここで見つかる)。`gh pr view <n> --comments` で PR 本文・レビューコメント
- `~/.claude/projects/<slug>/<session>.jsonl` — 同一マシン・進行中セッションの近道。パース規約は `~/.agents/skills/agent-log-analysis/references/log-formats.md`(Claude / Codex 両対応のスクリプトあり)
- `~/.agents/records/postmortem/registry.md` — 他 repo(cmux-connector 等)の事案台帳。同じ機構の事故は repo を跨いで再発する

## 使える道具(選択肢・縛らない)

**調べる**

- **時系列表を JST で 1 本引く**: 事故の導入 → レビュー run → 裁定 → 対策コミット → 発覚。前対策のコミットより前の事案なら「対策不発」ではなく「対策前の追加事例」で、重複対策を足さない(実例 PR1323 F10)
- **工程の主語を確定する**: その文章を誰が書いたか — 本体の自筆か sonnet 委譲か、subagent の thinking が 0 字なら推論は復元不能、と実測で言う。「統合エージェントと裁定サイトの文章はコンテキストを共有していない」型の問いに答えられる形にする
- **各工程の入力源を実ファイルで確定する**: 「その工程はその情報を持っていたか」は発火条件の実測と同じ重み(実例: レビューの「ユーザー意図」の入力源が誤転写済み ADR 由来の context.md で、原文はどの観点にも入っていなかった)
- **焼き込みを探す**: 誤解釈が e2e アサーション・棄却案記録・ADR・decision として固定化されると以後「正しさ」として保護される。テスト・台帳・記録の中に事故が既成事実化していないか
- **前対策不発の 4 分類**((a) 発火せず / (b) 発火したが降格・希釈で届かず / (c) 後の編集で消失 / (d) 変種枠外)→ [references/recurrence-registry.md](references/recurrence-registry.md)。判定は read-only サブエージェントに一次資料で取らせる
- **帰責を 3 者に分ける**: ユーザー側の与件(原文の曖昧さ・裁定で省いた工程)/ エージェント個別の落ち度 / ハーネス・体制の設計。「どの点が私が悪くて、どの点がハーネスが悪いか」に直接答える形

**裁定する**

- **見逃しの 4 類型と帰責**(軸違い / 枠内最適化 / 構造的死角 / 免罪)と横断で確認する癖 → [references/playbook.md](references/playbook.md) §2。件別に帰責を言い分けたいときに使う
- **対策の工程配置**(前段 spec/plan/grill か後段 diff レビューか。置き場は事故時に実際に走った工程)→ playbook §3
- **対策ラダー**(バグ修正のみ → 既存観点へ数行追記 → 新観点(ユーザー確認)→ 発火条件変更)→ playbook §4。対策が症状特化へ寄るのを止めたいときに使う。系統再発なら新設より既存対策の修理
- **対策文の書き方**: 一般則で書き、具象例は reference へ逃がす(具象語に束縛した規約は変種で不発する。一般行＋具象行の 2 段が実績)。既存規則を直すときは主語(どの工程が読むか)まで書く — 主語が無い規則は届かない工程で系統再発する。スキルは `~/.agents/skills/` と repo 側の 2 箇所にあれば両方を同時に直す

**検証する**

- **レビュー系の検証細則**(導入コミット固定・後知恵リーク対策・cwd 固定・合格は出力経路到達・抜け道の塞ぎ方)→ playbook §5
- **対話工程・実装者ループの検証**(決定的瞬間の凍結リプレイ・台帳転写リプレイ・実装判断の凍結リプレイ)→ [references/interview-stage-backtest.md](references/interview-stage-backtest.md)。grill・計画・単独 fix セッションの判断が事故のときに使う
- **検証 agent の事案外の指摘は回収する**: 検証 agent は fixture 全体を読むので、事案と無関係な本物の欠陥が Warning に混ざる。合否だけ読んで捨てない — issue 起票か担当セッションへ転送し、記録に「回収した/しなかった」を書く(捨てた 2 件が翌日本番事故になった実績あり)
- **修正が別セッションで進行中なら、対策のうちそちらへ送るべき提案は SendMessage / bd note で転送する**(本セッションはコードに触れない)

**記録する**

- 事故 → 裁定 → 対策 → 検証結果を bd(`bd note` / close reason)へ。検証のフィクスチャ・出力・判定は logs repo `harness/postmortem/<日付-slug>/` へ即コミット・push(logs-sync 任せにしない。成果物は消える)
- 台帳 `registry.md` へ 1 事案 1 ブロック(硬い規則 6)
- ユーザー裁定が出たら `.decisions/` へ 1 件(決定・棄却案・理由・リンク)
- スキル変更のコミットメッセージには「何の事故の較正か」「新観点を足さなかった理由」を残す
