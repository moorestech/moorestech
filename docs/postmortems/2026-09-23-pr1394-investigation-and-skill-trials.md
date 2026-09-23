# PR #1394 の発生原因とポストモーテム改善試行の記録

2026-09-23 時点。対象は[不具合修正 PR #1394](https://github.com/moorestech/moorestech/pull/1394)と、その振り返りを改善しようとした[PR #1395](https://github.com/moorestech/moorestech/pull/1395)。これは採用済みの新ルールではなく、当時の資料、対話で受けた指摘、提案と実走結果を次の判断に残すための記録である。ゲームの修正は別セッションの対象であり、この文書はコード変更を指示しない。

## 何が起きたか

【観測】7月の特殊設置移行計画は、サーバーでの建設コスト検証・消費と、クライアントのメニュー選択への移行を明記した。一方、選択可能でも素材が足りない状態を鉄道の各設置プレビューへ伝える責務は、計画の受入条件になっていない。[7月のプラン4](../superpowers/plans/2026-07-05-satisfactory-placement-plan4-special-systems.md)では、サーバーテストが不足時の拒否を扱うが、クライアントの成功確認は設置・消費を中心にしていた。導入コミット [`bec6eb289`](https://github.com/moorestech/moorestech/commit/bec6eb289) と [`2b4b9eba8`](https://github.com/moorestech/moorestech/commit/2b4b9eba8) も、この分担に沿う。計画全文とコミットの照合は[初回原因調査の保存資料](https://github.com/moorestech/moorestech_logs/blob/2b44219fe925b5d60f7c7e6c5986351758a25f1c/harness/postmortem/golden-align/2026-09-22-pr1395-accepted/inputs/original-cause.md)にある。

【観測】8月の[設置不可理由ツールチップ計画](../superpowers/plans/2026-08-21-placement-block-reason-tooltip.md)は全 PlaceSystem を対象としたが、橋脚単体の理由行を出さないことを「判定が無い」というエージェント前提で決め、既存ギャップとして扱った。横展開調査で橋脚の欠落も見つかり、[Issue #1221](https://github.com/moorestech/moorestech/issues/1221)に残った。8月22日のユーザーは Issue 作成と類似漏れの調査を依頼したのであって、橋脚の不足表示を不要と裁定していない。

【観測】8月30日の[`c8182c0e8`](https://github.com/moorestech/moorestech/commit/c8182c0e8)は、レール接続時の素材不足を計算・送信可否に反映した。しかし橋脚ゴーストはその前に着色され、最終的な接続可否が色へ戻らなかった。橋脚単体と車両にはクライアントのコスト判定が残っていなかった。テストは主に計算結果を検証した。9月2日の[PR #1299 の専用レビュー](https://github.com/moorestech/moorestech_logs/blob/2b44219fe925b5d60f7c7e6c5986351758a25f1c/harness/pr-independent-review/runs/pr-1299/agents/rev-core-any-user-intent-fulfillment.md)は要求を「達成」、Critical なしとした。他観点は緑の橋脚を Warning として検出したが既存扱いに留めた。レビュー自体が無かったわけではない。

【推論】発生の主因は設計上の欠落である。7月の計画どおりに実装しても、「素材不足の判定結果がプレビューに届く」経路がない。8月の局所修正にも、判断側から表示側への受け渡しが欠けた。実装上の漏れとレビューの達成誤判定は、不具合を広げ、残存させた別の要因である。「正しい設計から単に実装者が逸脱した」だけでは説明できない。

【あるべき設計案】設置候補の位置・向き・接続先を求めた後、具体的な操作を知る側が地形・接続・必要素材を合わせて操作全体の可否と理由を決め、その結果をプレビュー、理由表示、クリック時の送信判断へ渡す。橋脚とレールを一度に作るなら、部品ごとの早い着色で完結させず、操作全体を評価する。巨大な共通クラスを必須とする案ではない。7月の方式変更を実装タスクへ分割する前に、「所持アイテムによる選択保証が消えると、選べても素材不足という状態が生まれる」と提示し、その状態を誰が判定し誰が利用するか計画へ入れるべきだった。8月の修正でも同じ責務を先に示す必要があった。[当時の対話から抽出した承認済み回答](https://github.com/moorestech/moorestech_logs/blob/2b44219fe925b5d60f7c7e6c5986351758a25f1c/harness/postmortem/golden-align/2026-09-22-pr1395-accepted/inputs/golden.md)にこの提案の原文を保管した。

## 対話で受けた五つの指摘

1. 「設置経路ごとに不足時の色・理由・送信停止を揃える」は今回の症状に閉じた完了条件で、多様な事故を防ぐ工程改善になっていない。
2. 計画どおりでも発生する設計欠陥なのか、正しい設計からの実装逸脱なのかを先に判定すべきである。発生原因と、後段レビューが見逃した原因も分ける。
3. 正しい設計の責務・結果の受け渡しと、それをどの設計段階でどう提示すべきだったかを述べるべきである。
4. 「今後気をつける」では足りない。対策を提案するなら、変更先のスキル・節・指示文・検証の通過条件まで具体化すべきである。
5. 検証 subagent へ渡す依頼・資料・コード・スキル・モデルを示し、復元入力と当時環境の再現、計画の新規作成と完成計画のレビューを混同しない。

最後にユーザーは、SKILL 本文を増やしてモデルの思考を狭める懸念と、抽象文のままでは効かないという懸念を示し、具体例を使った few-shot 試験を求めた。本書に案を記録することは、その案の SKILL 採用を意味しない。

## 提案した対策と、未採用・未実証の範囲

| 案 | ねらいと具体内容 | 現在の扱い |
|---|---|---|
| 症状別の完了条件 | 設置経路ごとに不足時の色・理由・送信を確認する。 | 最初の回答。ユーザーが汎用性不足を指摘。今回の画面確認には使えても、工程改善の中心案として不採用。 |
| 計画前の保証移管 | 既存の `writing-plans/SKILL.md` のタスク分割前で、変更前コードから「旧保証元 → 新保証元 → 結果を必要とする利用側」を予定変更ファイルの外も含めて調べ、欠けた責務・伝達を設計する。 | ユーザーとの壁打ちで具体化された中心案。計画の新規作成試験は未実施。この PR では適用しない。 |
| 専用の意図充足レビューの修理 | 既存の `moores-code-review/reviewers/core-any-user-intent-fulfillment.md` で、計算・引数追加ではなく要求結果まで実行経路を追う。具体的な接続欠落は Critical、追跡不能なら達成未確認と不明箇所を示す。 | 中心案の後段。PR #1299 の誤判定回を再試行する検証は未実施。この PR では適用しない。 |
| 全般レビュー等への置換 | 専用レビューで起きた誤判定を、全般レビューや統合工程だけで捕まえる案。 | 後続の round-01 が中心案をこの案へ置き換え、独立評価で FAIL。追加調査の可能性はあるが代替は未承認。 |
| postmortem への短い5行 | 上の五つの指摘を各1行で SKILL の「裁定する」節へ追加。 | PR #1395 の初版。実走では5要件が揃わなかった。本文追加の効果量は旧版対照なしでは不明。 |
| postmortem の長文出力契約 | 五つを初回回答の必須欄へ移し、工程、指示文、投入表、判定条件まで規定。元の「裁定と対策を同ターンに混載しない」規則も変更。 | 後続セッションが PR #1395 に実装。承認済み回答との1事例1走行の意味照合は PASS。多様な事故への一般化や実際の工程対策の効き目は未実証。ユーザーの本文肥大化への懸念を受け、この PR から差分を撤回。 |
| 架空事例3件の few-shot | 保存の部分失敗、期限切れ招待の実装逸脱、通知停止の意図誤転写という別分野の回答例を別紙へ置き、SKILL 本文は短い参照だけにする。 | 三つの実走はすべて所望の五点に未達。例自体や few-shot 一般の無効性は証明していない。候補は未採用。 |

中心案の文言と合格条件、さらに検証入力案の詳細は[承認済み回答の原文](https://github.com/moorestech/moorestech_logs/blob/2b44219fe925b5d60f7c7e6c5986351758a25f1c/harness/postmortem/golden-align/2026-09-22-pr1395-accepted/inputs/golden.md)に残した。上表は提案の記録であり、別セッションで進む実装修正や後続 PR の状態を代弁しない。

## SKILL 候補を実走した結果

試験はいずれも PR #1394 の同じ調査依頼から、新規 tmux/Codex で起動した。点数は別々の回答を診断した値で、厳密な同条件 A/B 実験の改善率ではない。`exit 0`・完走・SKILL 読込は内容の合格と別に扱う。

| 候補と資料 | 被験体が読んだもの・観測結果 | 評価と限界 |
|---|---|---|
| [初版5行、最初の実走](https://github.com/moorestech/moorestech_logs/blob/2b44219fe925b5d60f7c7e6c5986351758a25f1c/harness/postmortem/2026-09-23-pr1395-live/evaluation.md) | 5行を読んだが、対策は症状別、設計責務・指示文・入力案が不足。 | 43/100、FAIL。現行の再発台帳から後日結論が露出し、独立した再発見とは扱えない。 |
| [初版5行、ログを過去版に固定した再試行](https://github.com/moorestech/moorestech_logs/blob/2b44219fe925b5d60f7c7e6c5986351758a25f1c/harness/postmortem/2026-09-23-pr1395-live-clean/evaluation.md) | 5行全文読了。導入当時の計画欠陥、正しい結果の受渡し、具体的な検証入力はなお不足。 | 30/100、FAIL。後日の Beads 概況と試行ブランチ名が自動注入されたため完全盲検ではない。 |
| [後続の長文版、初期5基準試験](https://github.com/moorestech/moorestech_logs/blob/2b44219fe925b5d60f7c7e6c5986351758a25f1c/harness/postmortem/2026-09-22-pr1395-improve/summary.md) | 以前の試験は50/100、変更後は5基準で100/100と報告。 | この100点は承認済み回答の意味を証明しなかった。完成計画レビューへのすり替え、別回のレビューでの代用を見落とす採点だった。43/30点試験とはモデル・入力条件が異なり、点数を直結しない。 |
| [長文版の承認回答照合 round-01](https://github.com/moorestech/moorestech_logs/blob/2b44219fe925b5d60f7c7e6c5986351758a25f1c/harness/postmortem/golden-align/2026-09-22-pr1395-accepted/evals/round-01/run-01.md) | 計画生成と対象レビュー回を復元したが、専用意図充足レビューを全般レビューへ置換し、接続欠落 Critical を任意の判断へ弱めた。 | FAIL。stdout 終端回収にも不整合があったが native 最終回答は保存された。 |
| [同 round-02](https://github.com/moorestech/moorestech_logs/blob/2b44219fe925b5d60f7c7e6c5986351758a25f1c/harness/postmortem/golden-align/2026-09-22-pr1395-accepted/evals/round-02/run-01.md) | 新規 tmux/Codex `gpt-6-astra`、508.63秒、正常終了。承認済み回答の必須意味と禁止事項を独立照合し blocker なし。静的監査も一般則の整合性を PASS とした。 | **PASS は一事例の回答一致に限定**。計画・レビュー対策そのものの実効性、他事案への汎用性、子エージェントの完全な非汚染は未証明。[収束記録](https://github.com/moorestech/moorestech_logs/blob/2b44219fe925b5d60f7c7e6c5986351758a25f1c/harness/postmortem/golden-align/2026-09-22-pr1395-accepted/CONVERGENCE_SUMMARY.md)参照。 |
| [few-shot 1: 裁定節から参照](https://github.com/moorestech/moorestech_logs/blob/2b44219fe925b5d60f7c7e6c5986351758a25f1c/harness/postmortem/2026-09-23-pr1395-fewshot/evaluation.md) | リンクと別紙の存在は見たが、事例本文を読まず終了。 | 28/100、FAIL。事例を読んだときの効き目はこの回では測れていない。 |
| [few-shot 2: 冒頭で事例を読む](https://github.com/moorestech/moorestech_logs/blob/2b44219fe925b5d60f7c7e6c5986351758a25f1c/harness/postmortem/2026-09-23-pr1395-fewshot-read/evaluation.md) | 架空3例を全文読了。設計欠陥の判定は改善したが、責務、追加指示文、投入物が不足。 | 48/100、FAIL。既存の同ターン対策提示禁止との緊張が影響した可能性はあるが、原因と断定できない。 |
| [few-shot 3: 禁止文を削除](https://github.com/moorestech/moorestech_logs/blob/2b44219fe925b5d60f7c7e6c5986351758a25f1c/harness/postmortem/2026-09-23-pr1395-fewshot-gate/evaluation.md) | 事例全文読了。設計欠陥は判定したが、対策は症状寄りで、責務と検証入力が不足。 | 53/100、FAIL。`git log --all` で後日の改善コミット件名が露出し、完全盲検ではない。 |

few-shot の別紙は[試験時の3事例](https://github.com/moorestech/moorestech_logs/blob/2b44219fe925b5d60f7c7e6c5986351758a25f1c/harness/postmortem/2026-09-23-pr1395-fewshot-read/tested-examples.md)に保全した。few-shot の三試行は「今回の候補の今回の出力が未達」を示すが、事例の書き方を変えた場合や別事案での効き目は未測定。長文版 round-02 との点数比較や勝敗判定もしない。

## 検証担当へ渡す資料案と再現の限界

【計画作成を試す場合】7月の計画作成直前のコード、当時のユーザー依頼・確定済み裁定、先行仕様と申し送り、当時版 `writing-plans` に提案した一般則だけを加えたものを渡し、当時と同じ範囲の計画を新規作成させる。完成したプラン4、後日の Issue・PR #1394・今回の原因説明・採点基準は渡さない。候補生成と操作可否を誰が判断し、結果が利用側へ届くかが計画に現れれば通過。完成計画を渡して欠点を指摘させるのは別能力の「計画レビュー」試験である。

【専用レビューを試す場合】[PR #1299 の保存済み実入力](https://github.com/moorestech/moorestech_logs/tree/2b44219fe925b5d60f7c7e6c5986351758a25f1c/harness/pr-independent-review/runs/pr-1299)で、全差分・context・contract・当時版専用 reviewer に一般則だけを加える。元の誤判定が起きた回を使い、橋脚の表示へ判定結果が届かないことを要求未達の Critical として示すかを判定する。追跡不能なら達成未確認と具体箇所を返す。別のレビュー回で似た欠陥を見つけても、元の誤判定を止めた証明にはならない。

【観測限界】最初の壁打ち時点では7月の依頼原文・生ログを発見できず、当時再現とは呼べなかった。後続の[原ログ抽出](https://github.com/moorestech/moorestech_logs/blob/2b44219fe925b5d60f7c7e6c5986351758a25f1c/harness/postmortem/golden-align/2026-09-22-pr1395-accepted/inputs/recovered-july-lines.jsonl)で `/writing-plans` 依頼と圧縮された有効文脈の一部は発見された。ただし以前の全文会話、未コミット状態、注入スキルの厳密な版、同一モデル・外部環境まで完全に戻したとは証明していない。復元した資料での試行は復元入力による検証と記載する。両工程の対策試験はまだ実施していない。

## 現時点の判断と残課題

【確定】この PR は知見の保存に用途を絞る。SKILL 本文は現在の `master` と同一に戻し、対策の指示文、few-shot 別紙、ゲームの実装変更を取り込まない。長文版の限定的な出力一致も、few-shot 候補の三回の未達も記録として残す。

【未検証】計画前の保証移管ルールと、専用意図充足レビューの修理が、実際にそれぞれの当時入力で欠落を止めるか。別種の事故にも効くか。短い具体例を改稿すれば、本文を増やさず所望の回答になるか。これらを未実施のまま「再発防止済み」とは呼ばない。次に試すなら、証明したい工程と失敗判断を固定してから、入力・伏せる情報・合格出力を先に記録する。
