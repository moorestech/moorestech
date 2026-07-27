# レビュー免責ロンダリング封鎖（スキル改修一式） レビュー記録 (2026-07-26)

## 対象
- base: `0e702e08d^`（=`e76c5de08`） / reviewed head: `922a100e5`（適用後final diffは `0296be8cb` まで）＋ ~/.agents `956351c`（適用後 `87b2738`）
- ブランチ: tree1（スキル改修。C#変更なし）
- context要約 — ゴール: agent自作合意による指摘握り潰し経路の封鎖・okの意味を台帳承認に限定 / 非目標: CommonBlockPlaceSystemコード修正 / 許容トレードオフ: basename一致・suppressed可視化はCritical/Warning級のみ / 制約: sim-gate前例踏襲・発火条件ガード無変更

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | なし | confirmed 0・candidates全0（--context含む） |
| precedent-alignment（opus代替） | あり | `[ADR:]`無検証でagent前提の付け替えロンダリング残存（実演: 本レビューcontext自身の偽ADR3行が素通り）・散文トレードオフがfail-open・eval未追従 |
| core-any-file-directory-organization | なし | scripts/平置きは既存規約適合。ミラーツリーの既存ドリフトをInfo記録 |
| core-any-implicit-value-meaning | あり | checks_contextのseen_sections==0が片方見出し欠落を素通し（実走再現）・ledger_gateの空文字3状態同居で虚偽診断 |
| core-any-user-intent-fulfillment | なし | 依頼動詞5対すべて達成根拠確認。Warning: [ADR:]無検証・suppressedが§3自動適用未除外・all-code-review同期漏れ2点 |
| Fable全般（opus代替） | あり | suppressed-byの伝送路が共通出力契約に未定義（優先宣言により確実に落ちる）・§3/§2.5でユーザー合意が黙って覆される退行 |
| Codex外部監査 | あり | LABEL_RE任意文字受理＋[ADR:]表面ラベル免責（実測突破）・TARGET_REがcheckbox/bold素通し（実測）・plan消滅でFileNotFoundError |

## 適用した修正
- checks_context v2: [ADR:]を台帳まで実解決（非実在・agent前提参照・doc不在を検出）・[ユーザー裁定]構造検証（引用orAskUserQuestion+日付）・散文行検査・カテゴリ別fail-closed・confirmedスキーマをchecks_static準拠に（3系統一致＋Codex） → `0296be8cb`
- Step 4共通出力契約に `suppressed:` 専用節新設（重大度を行頭保持・Critical/Warning節と分離=案A採用。§3自動適用/§2.5昇格との衝突を構造的に回避） → `0296be8cb`
- §2.6にsuppressedの自動適用/昇格除外＋報告固定形式 `- [Critical|Warning] … — suppressed-by: <トレードオフ, 出所>` → `0296be8cb`
- ledger_gate v2: frontmatter限定spec抽出・台帳境界（次の##まで）・TARGET_REのcheckbox/bold対応（`**Modify:**`のfail-openを検証で発見し追修正）・lens extensions考慮・plan消滅prune・空stdin/TMPDIR=""耐性・spec不在の明示診断 → `0296be8cb`
- eval追従: synthetic 4contextへ出所ラベル・README Layer1に--context・期待#34/#35・log1行 → `0296be8cb`
- moores側result-state-propagation:47の契約完全形化・precedent-alignmentへ「spec記載≠合意」復元・SKILL.mdモデル表のClient.Game追従・Step6.5の--context非再実行明記 → `0296be8cb`
- all-code-review同期: checks_context v2・Step2.5 confirmed列挙・Step5契約suppressed節・Step7報告節・§2.6除外 → `87b2738`
- post-checks追随: caller-orchestrationへ合意出所根拠1文復元・eval/README旧マーカー指示更新・all-code-review docstring言語統一 → 記録後コミット

## 設計判断（AskUserQuestion裁定）
- なし（suppressed伝送形式=案A・ADR検証=スクリプト担保の2件は、spec裁定済み原則「担保はスクリプトが持つ」とfail-safe方向の一意決着として適用。異議あれば差し戻し可能な旨を最終報告に明記）

## 免責で消された指摘
- suppressed: 0件（全観点とも通常判定。トレードオフ免責の発動なし）

## 破棄した指摘
- 「変更4はpaths追加だけではレンズ本文がクライアント汎用層を識別できない」（spec review時の判事予測C）— 反証役が破棄（観点1の記述は機構非依存）
- 台帳basename一致の弱さ（Codex言及）— [agent前提]トレードオフとして明示済みのため指摘外（Codex自身も指摘扱いにせず）

## 事後結果（マージ後追記可）
-

## メタ
- セッションID: 40a85bb6-7fe1-459d-941e-c40d72e8a58d（branch: ab125225）
- スキップ系統: なし。ただしfable枠上限によりprecedent-alignmentとFable全般は**opus代替**（fable本来担当。報告に明記）
- 備考: レビュー対象がレビュー機構自身のため、新設した出所ラベル検査・suppressed契約を本レビューのcontextでdogfooding（初回contextの偽[ADR:]参照3行と黙認の裁定格上げ1行をv2検査が検出→修正済み）
