# スキットWeb UI再実装 レビュー記録 (2026-07-27)

<!-- 1レビュー実行=1ファイル。命名: YYYY-MM-DD-<topic>.md（再レビューは -r2 付き新ファイル＋相互リンク1行）。
     記録は不変。マージ後に判明した事実のみ「事後結果」へ追記可。設計根拠: docs/superpowers/specs/2026-07-23-review-records-design.md -->
<!-- One review run = one immutable file; only the post-merge outcome section may be appended later. -->

## 対象
- base: `e26f42318` / reviewed head: `4486bc4cc`（レビュー対象コミット。修正適用後 `b6df4c7b3` → 裁定実装 `4e15e0085`）
- ブランチ: tree2 / PR: なし
- context要約 — ゴール: WebスキットUIをuGUI正本実測とwebui-designホワイトリストに準拠させる全面再実装＋暗転順序バグ修正。非目標: Topic/Action契約・Unity側・interaction.ts・testid契約の変更、Logボタン、Transitionフェード再現。許容トレードオフ: α88%透け・暗転不透明黒・板の意図再現・stage内z層・mock-host既存の穴（全て[agent前提]）。制約: SKILL.mdホワイトリスト・200行/10ファイル・pointer-events維持・既存パネル無回帰。

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | 0 | 初回contextラベル不備5件（修正して再実行）。比較演算子候補0でverifier不発火 |
| precedent-alignment (fable) | 2 | capture-skitのmock-host内部直叩き / SKILL§5とAuto ON点灯色の不整合 |
| file-directory-organization | 0 | controls/分割はrecipe/blockInventory前例と同形 |
| implicit-value-meaning | 2 | intent型消失(Set<string>) / 背景スキット空snapshotで裸の「 : 」表示 |
| user-intent-fulfillment | 0 | 依頼4対は達成。Warning群=選択肢板の実測乖離（→裁定で案A採用） |
| ai-recurring-mistakes | 2 | stopPropagation死にガード複製。Warning=撮影QAが全て未接続状態だった |
| centralization-duplication | 1 | capture-skit直叩き（precedentと一致）。設計判断=IconButton一般化 |
| dead-code-and-scope | 0 | 参照ゼロexport無し |
| result-state-propagation | 2 | 背景空行バグ（implicitと一致）/ intent型消失（3系統目）。設計判断=演出中ツールバー |
| single-source-of-truth | 1 | 送りマーカー条件がclickOutcomeのインライン再実装 |
| Codex外部監査 | 0 (High 1) | stage内移設によるz層逆転（WorldPin/ChallengeHudが会話窓より上）→裁定でHUDゲート |
| Fable全般 | 0 | SKILL§8.12のz層断定と実装の不整合（doc修正で解消）・--z-modal宙吊り |
| comment-rationale-guard | 0 | t()迂回WHYの復元提案1件→適用 |
| comment-convention-guard | 機械的8+要判断1 | 全て適用。境界5件は根拠保全優先で残置 |

## 適用した修正
- intent型消失をSkitIntent型で復元（4系統一致: implicit/result-state/centralization/Codex） → `b6df4c7b3`
- 背景スキット空行「 : 」バグ修正＋話者名空時の区切り抑止（2系統一致） → `b6df4c7b3`
- 送りマーカー条件のclickOutcome単一評価化（single-source） → `b6df4c7b3`
- 選択肢のallowedIntents.has("select")ゲート（implicit-value） → `b6df4c7b3`
- 死にstopPropagation削除（2系統一致）・菱形svg序数依存の役割クラス化（3系統一致） → `b6df4c7b3`
- capture-skitの/__skit?stage=transition集約（3系統一致） → `b6df4c7b3`
- mock-host notification空snapshot恒久化（実host準拠・撮影QAの未接続状態問題を解消） → `b6df4c7b3`
- 背景行nowrap/ellipsis（Codex）・SlotFrameシアン残置・CSS命名PascalCase化・SKILL整合2件・コメント裁定10件 → `b6df4c7b3`

## 設計判断（AskUserQuestion裁定）
- Q: z層逆転（Codex High） / 選択肢: HUDゲート・Portal移設・許容 / 裁定: **blocking中はHUDを隠す** / 適用: WorldPinOverlay・CurrentChallengeHudにmode購読ゲート `4e15e0085`
- Q: 選択肢板の正本実測準拠 / 選択肢: 案A実測・案B簡略化明記 / 裁定: **案A** / 適用: フェード44.7px・線/菱形インセット・マーカー40px・間隔0.7px・微細寸法全トークン化 `4e15e0085`
- Q: 演出中ツールバー（初回「どういうこと？」→補足説明後に再質問） / 裁定: **正本一致でツールバー残す** / 適用: textAreaVisibleゲート分割＋stage=staging検証追加 `4e15e0085`
- Q: 面なしアイコンボタン共通化 / 選択肢: 現状・IconButton一般化・トークンのみ / 裁定: **IconButton一般化** / 適用: PanelCloseButton改名＋children prop＋回帰確認 `4e15e0085`

## 破棄した指摘
- GamePanelの:not連鎖→opt-in化（3系統言及・Codex Low）— 構造改善の裁量案でありWarning報告のみ（実害なし・既存パネル回帰リスクとの均衡）
- Codexの「Transitionをmodal(200)未満へ下げる」— 暗転は場面転換カバーで全面を覆うのが正本準拠。doc正確化で解消
- ツールアイコンbbox比1.00の完全達成 — viewBox外接化はSkip終端バーと隣接アイコンが接触する退行を実測。実効0.8で確定しSKILLに注記

## 事後結果（マージ後追記可）
- （未記入）

## メタ
- セッションID: 097d30be-59de-4dc8-94d2-48cbb9060203 / スキップ系統: なし（codex実行済み） / 備考: レビュー対象がTS/CSS/MDのみのため発火レンズはprecedent-alignmentのみ・reviewer 8本。修正適用はユーザー指示によりOpus subagent（skit-review-fixer）が実施
