# チャレンジHUD表示改善 再レビュー記録 (2026-07-27)

> 初回レビュー: [チャレンジHUD表示改善](2026-07-27-challenge-hud-visual.md)
> ユーザー裁定後の再レビュー: [操作モードHUDのクラフト枠化](2026-07-27-challenge-hud-visual-r3.md)

## 対象
- base: `4d492e0eef76d44d571a612068962e076bed37f4` / reviewed head: `4d492e0eef76d44d571a612068962e076bed37f4` + dirty（16 files、315 insertions、34 deletions）
- ブランチ: `fix/challenge-hud-visual-pr` / PR: `#1082`
- context要約 — ゴール: 操作モードHUDを面なし情報階層へ統一しPlaywrightと実画像で検証 / 非目標: Unity・通信プロトコル・ゲーム状態の仕様変更 / 許容トレードオフ: 世界背景の可読性は共有色・固定文字階層・最小限の文字影で確保 / 制約: Mantine面部品禁止・共有トークン・固定長・入力判定なし・日英コメント・200行上限

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | 0 | 最終confirmed 0、比較演算子・コメント長・配置上限候補0 |
| structure / precedent | 0 | 面なしHUD、FadeRule、トークン配置へ整合し、計画のE2Eパス不一致を修正 |
| state / Fable全般 | 0 | semantic section・見出し参照へ修正し、共有mock状態の後処理を追加 |
| tests / mutation耐性 | 0 | background-image・擬似要素・文字階層・配置不能警告まで契約化 |
| Codex外部監査 | 0 | テスト穴3件を適用し、旧14状態出力への指摘は正規18状態再撮影で解消 |
| post-check 2系統 | 0 | 根拠喪失・コメント規約違反ともなし |
| Playwright視覚subagent | 0 | 18元画像を全数確認し `VERDICT: OK` |

## 適用した修正
- Placement/Delete HUDからMantine面部品を除去し、意味要素・FadeRule・共有トークンの面なし階層へ統一 → 適用コミット `dab4253ec`
- 配置不能／削除不能警告、明暗背景、擬似要素、色・文字寸法・文字影をPlaywright契約へ追加 → 適用コミット `dab4253ec`
- captureを14状態から18状態へ拡張し、正規画像とmetricsを再生成 → 適用コミット `dab4253ec`
- `webui-design` 3コピーへ操作モードHUD規約を同期 → 適用コミット `dab4253ec`

## 設計判断（AskUserQuestion裁定）
- AskUserQuestion 0件。ユーザー指定の `$webui-design` に従い、操作モードHUDをカード面ではなく世界上の情報階層として実装した。

## 破棄した指摘
- 「正規QA出力が14状態のまま」— 最終captureを既定出力先へ再実行し、manifest 18件・操作HUD 6状態・metrics違反0を確認したため破棄。
- suppressed: 0件。

## 事後結果（マージ後追記可）
- なし。

## メタ
- セッションID: Codex外部監査ID未記録 / スキップ系統: なし
- 備考: lint、unit 384件、build、E2E型検査、focused Playwright 9件、18状態capture、画像全数レビューをfresh実行。
