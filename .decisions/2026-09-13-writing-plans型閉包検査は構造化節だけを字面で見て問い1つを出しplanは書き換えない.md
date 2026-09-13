# writing-plans 型閉包検査は構造化節だけを字面で見て問い1つを出し plan は書き換えない

2026-09-13 裁定（6論点一括）。

## 決定
writing-plans（spec-architecture-review 部）に Phase 2.6「型閉包・重複・ADR矛盾」を足す。線引きは次の6点。
1. 実行は **fresh-context の subagent** に委譲し、本体が plan 作成中に読んだ既存ファイル一覧を渡す（本体は自分の書いた形を理由ごと読んでしまう）。
2. 「既存に同役割の判定がある」発見は **第3バケツ「本PR外のリファクタ提案」** としてレビュー依頼文で人間に渡す。plan のタスクにはしない。
3. 発火条件は **数えられる字面パターン**に限定する（`references/type-closure-patterns.md` の表）。表に無い形を「悪そう」で足さない。
4. 検査対象は plan の **構造化6節**（Files / Interfaces / Produces・Consumes / 配置表 / コードブロック / 判断記録・やらないこと）のみ。散文節は読まない。
5. 出力は **人間への問い1つ**（AskUserQuestion）に留め、plan は書き換えない。「このままでよい」も判断記録に残す。
6. 「plan は正しかったが実装が逸脱した」分（照合63件中8件）は writing-plans ではなく **SDD task-reviewer-contract の射程**。別PRで「plan の Interfaces と実装の型差分」を1行足す。

## 棄却案
- **散文節も含めて LLM の判断で検出する** — 棄却。false positive でレビューの信頼が壊れる。照合した63件の発火点はすべて構造化節にあり、散文を読む必要が無かった。
- **発火時に plan を自動修正する** — 棄却。検査1〜4（前例が明確な配置）と違い型の形は設計判断そのもので、前倒しすべきは「問い」であって「答え」ではない。
- **重複発見を plan タスクに足す** — 棄却。範囲の膨張は別の失敗になる。人間が着手可否を決める。
- **impl-deviated 分も同じ拡張PRに入れる** — 棄却。計画側と実装レビュー側の変更を混ぜると、レビュー側の線引きが計画側の裁定に引きずられる。

## 理由
採用済み critical 239件の分類で、plan 段階で防げる候補は61%、最大群は層配置でなく型未閉包（63件）。その56%は plan 本文に欠陥形が既に書かれており、字面で数えられた。既存の検査1が覆うのは層越境の5件分のみ。詳細は `writing-plans/references/incidents.md #T1`。

## リンク
- 引き継ぎ: `docs/superpowers/plans/2026-09-13-計画プロセス系スキル剪定と-writing-plans-再設計-検討引き継ぎ.md`（論点1〜3）
- 分類・照合原本: `moorestech_logs/harness/writing-plans-extension/`
- 継承した規律: spec-architecture-review「迷ったら ok に倒し、注目点として書く」
- bd: moorestech-kpb1
