# レビュー免責ロンダリングの構造的封鎖 — 設計spec

日付: 2026-07-26
状態: ユーザー承認済み設計の文書化

## 背景（事故の要約）

`CommonBlockPlaceSystem`（クライアント汎用設置層）への電気ドメイン依存混入（`ElectricWireAutoConnectPreview` フィールド, 2026-07-03 コミット `2d268c5d8`）は、レビューで1回正しく検知されたが、以下の経路で握り潰された:

1. plan（task-12 brief）が `Modify: CommonBlockPlaceSystem` を所与として書き、代替案を比較しなかった
2. agent自身の方針がレビューcontextの「許容するトレードオフ」に**「合意済み」として無根拠で格上げ**された（ユーザー発言に該当なし）
3. アーキテクチャレビュアーは違反を正しく発見したが「合意済み（context line 18）」を理由にNot Critical判定した
4. 唯一発火した200行制限は、ロジックを別クラスへ移して行数を下げる修正を誘発し、依存方向は不変のまま指標だけ改善した
5. 後日（07-13）できた `domain-boundary` レンズは paths がサーバー限定・patch差分限定・「合意済みは指摘しない」の三重で、当該箇所に二度と当たらない

本質的問題は「人間が読んでいないこと」ではなく、**読まれていない箇所も『同意済み』として扱われる仕組み**にある。人間の全読は前提にできないので、機械側を「読まれていない箇所は未同意」という保守的デフォルトに合わせる。

## 3原則

- **原則① 合意には出所と引用が要る**: 「合意済み」「許容」と書くにはユーザー発言の引用またはAskUserQuestion結果の記録が必須。引用不能なものは自動的に「agent前提」となり、レビュー免責力を持たない
- **原則② 免責は消音でなく降格＋可視化**: トレードオフ合致で落とす指摘も削除せず、suppressedタグ付きで最終報告に必ず列挙する
- **原則③ 人間が読む対象は文書でなく判断台帳**: 「ok」の形式的意味を「明示提示された判断項目への承認」に限定する。spec/plan本文はAIが読み、人間には判断台帳（裁定＋agent前提1行リスト）だけを見せる

執行は指示文レイヤー（agentへの説明）とスクリプトレイヤー（物理ブロック）の二層で担保する。指示文だけでは今回のように文脈圧力で骨抜きにされるため、**担保はスクリプトが持つ**。

## 変更1: トレードオフの出所ラベル必須化（原則①）

対象: `.claude/skills/moores-code-review/SKILL.md` Step 1 / `references/integration-rules.md` §6 / `~/.agents/skills/all-code-review/SKILL.md` の4カテゴリcontext項。

- 「許容するトレードオフ」「非目標」の各行に出所ラベルを必須化:
  - `[ユーザー裁定: "発言引用" または AskUserQuestion結果 YYYY-MM-DD]`
  - `[ADR: <spec名>#<台帳項目>]`（変更3の判断台帳から引く。台帳がSSOT）
  - `[agent前提]`
- ラベル無し・引用不能な行は**自動的に `[agent前提]` 扱い**（「合意済み」という語をagentが自力で書ける経路を消す）
- 執行: `scripts/deterministic_checks.py` に `--context <USER_PROMPT_PATH>` を追加し、トレードオフ/非目標行の出所ラベル欠落を `confirmed` として検出する。**all-code-review が自前保有する `scripts/deterministic_checks.py` にも同検査を入れる**（片側だけだと汎用側の担保が指示文のみになり「担保はスクリプトが持つ」原則と矛盾）

## 変更2: 免責は降格＋可視化（原則②）

対象: 全レンズ・reviewer の「依頼動詞優先ガード」文言（`lenses/domain-boundary.md:45` 等の同型記述を横断置換。**`~/.agents/skills/all-code-review/reviewers/*.md` に実在する25本の同ガード節も含む** — ユーザーゴールは「moores-code-reviewやall-code-review」と両機構を名指ししており、片方に消音経路を残さない）/ 両スキルの `references/integration-rules.md` 相当の統合・報告規約。

- レンズ側の新文言: 「**ユーザー裁定/ADR出所**のトレードオフに合致する指摘は、破棄せず `suppressed-by: <トレードオフ, 出所>` タグ付きで重大度を保持したまま返す。**`[agent前提]` 出所は免責事由にならない**（通常のCritical/Warningとして返す）」
- 統合側: suppressedタグ付きCritical/Warning級は最終報告の**「免責で消された指摘」専用セクション**に必ず列挙（1行＋出所）。Info級は列挙しない（ノイズ化防止）
- suppressed指摘はAskUserQuestionには載せない（拒否権は報告セクションの1行で行使できる）

## 変更3: 判断台帳（原則③・台帳承認方式）

対象: `user-simulator`（ADR仕様: review/preanswer protocol）/ `brainstorming` / `writing-plans` の各SKILL.md。

- spec/plan末尾のADRを**判断台帳**に拡張:
  - (a) ユーザー裁定 — 引用・日付つき。AskUserQuestion結果は全件
  - (b) agent前提 — 1行・アーキテクチャ級/不可逆級のみ・拒否権注記つき
- ユーザーレビュー依頼時、**台帳をメッセージ本文に直接貼る**（spec本文はリンクのみ）。「ok」の形式的意味は台帳項目の承認に限定
- writing-plans: task briefの `Modify:` 対象に**複数ドメインから参照されるファイルを含む判断は台帳掲載必須**。掲載判定は裁量（「重要か」）ではなく機械条件: **対象パスが `lenses/*.md` の `paths` 正規表現にマッチするか**
- **優先順位**: pathsマッチは (b) の「アーキテクチャ級」判定の**機械的下限**であり、マッチした項目は級の自己判定によらず掲載必須（機械条件と級限定の衝突はマッチ側が勝つ）。逆にマッチしない項目の掲載は従来どおり (b) の裁量判定
- **カバー範囲の明記**: 機械条件が実効なのは paths 発火型レンズ（domain-boundary 等4本）のみ。keywords 発火型レンズ（set-once/type-driven 等6本）は plan 時点の `Modify:` パスから判定不能で、ledger-gate の守備範囲外（レビュー段階の変更2で捕捉する）
- 台帳に無い判断はレビュー免責力ゼロ
- 執行: **`ledger-gate` スクリプトを新設**（`moores-code-review/scripts/ledger_gate.py`。lenses pathsを読むため同居）。plan/task briefの `Modify:` 対象を抽出し lenses paths と突き合わせ、マッチしたのに spec の判断台帳に対応項目が無ければ exit 2 でブロック。`sim-gate.sh` と同パターンで writing-plans の frontmatter hooks に配線する
- **plan→spec対応の特定規則**: plan の frontmatter に対応 spec の相対パスを必須項目として持たせる（ledger_gate.py が台帳の所在を機械的に特定できるようにする。sim-gate.sh は対応関係を持たないため新規に定める）

## 変更4: domain-boundary レンズの個別穴塞ぎ

対象: `.claude/skills/moores-code-review/lenses/domain-boundary.md` / `references/integration-rules.md`。

- `paths` に `"Client\.Game"` を追加（クライアント汎用層が永久無検査の穴を塞ぐ）
- 過検知ガードの例外追加: 「既存違反は備考1行」のまま、ただし**このpatchが編集中のファイル内の既存違反はWarningで必ず返す**
- `integration-rules.md` の修正適用規約に1行追加: 行数・配置系指摘の修正案が「コードの移動」を含む場合、**移動が呼び出し元の依存方向を変えるか確認**してから適用する（行数圧縮で汚染を隠す抜け道の封鎖）

## セルフ反証

最凶ケースは「agentが台帳掲載を『アーキテクチャ級でない』と自己判定して載せない」＝今回の埋め込みの再演。対策は変更3の機械条件（paths マッチ）＋ ledger-gate による物理ブロックの二層。指示文の誠実な遵守に依存しない。

残存リスク: 台帳の肥大化により読まれなくなる回帰。緩和は (b) をアーキテクチャ級に限定・1行厳守。それでも溢れる場合はタスク分割が大きすぎるシグナルとして扱う。

## スコープ外（後続タスク）

- `CommonBlockPlaceSystem` からの電気依存除去（設置プレビュー拡張点の導入）。本スキル修正の後に別spec/planで実施し、新レンズ体制の最初の検証ケースとする

## 判断記録（ADR）

- **agent前提の提示方式 = 台帳承認方式**: spec/plan承認時に判断台帳だけを見せる。前提は黙認でも免責力を持たず、レビュー指摘時は必ず表面化。割り込み回数は現状と同じ（出所: シミュレーター予測→ユーザー承認 2026-07-26。根拠: 裁定#4「読む量・答える量の純増」却下）
- **原則①②は提示方式によらず共通実施**（出所: AskUserQuestion質問文の共通前提としてユーザー承認 2026-07-26）
- **執行レイヤー（script/hooks）の追加**: 「機械条件＝hooksを使うのか」というユーザー質問を受け、指示文レイヤーだけでは今回の事故（指示があったのに骨抜き）を防げないと判断し、sim-gate.sh前例踏襲の物理ブロックを設計に追加（出所: ユーザー指摘 2026-07-26 →設計反映後「ok」承認）
- **agent前提（拒否権つき）**: ledger_gate.py の設置場所は moorestech リポジトリ内 `moores-code-review/scripts/`（lenses pathsを読むため同居が自然）。**moorestech 専用なのは ledger-gate 配線のみ**で、all-code-review 側にも出所ラベル（context書式＋自前deterministic_checks検査）と依頼動詞優先ガード25本の suppressed 化は適用する（simulator review指摘の適用 2026-07-26）
