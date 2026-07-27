# 操作モードHUDのクラフト枠化 再レビュー記録 (2026-07-27)

> 前回レビュー: [チャレンジHUD表示改善 再レビュー](2026-07-27-challenge-hud-visual-r2.md)

## 対象
- base: `de6c2a2bda8ced143f1a74374987b4f03abb091d` / reviewed head: `a5de61758be28453075e09430b624cc0e73b6fbe`
- ブランチ: `fix/challenge-hud-visual-pr` / PR: `#1082`
- ゴール: 配置・削除HUDをクラフトレシピと同じ共有枠へ変更し、画像とPlaywrightで検証する

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | 0 | confirmed・比較演算子・region候補0 |
| structure / state | 0 | `GamePanel variant="craft"`、意味要素、入力透過、3コピー同期を確認 |
| tests / mutation耐性 | 0 | 実hit-test、明暗背景コントラスト、状態後処理、共有枠DOM契約を追加 |
| Codex外部監査 | 0 | Medium 2件を照合し、変更対象明記と共有警告色consumer契約を追加 |
| post-check 2系統 | 0 | 根拠喪失なし。自明コメント1組を意図コメントへ修正 |
| Playwright視覚subagent | 0 | 18画像を確認し `VERDICT: OK` |

## 適用した修正
- Mantineカードを共有 `GamePanel variant="craft"` と `FadeRule` の構成へ置換。
- 警告色を明背景合成でも4.5:1以上へ調整し、既存consumerへの共有をテスト化。
- Playwrightへ枠・グリップ・入力透過・通常文／警告文のコントラスト契約を追加。
- 配置／削除画像を正規captureから再生成。

## 設計判断（AskUserQuestion裁定）
- ユーザー裁定「なんでHUD透明にするの？クラフトレシピの枠で囲って」により、前回の操作HUD面なし方針を撤回し共有craft枠を採用。AskUserQuestion 0件。

## 破棄した指摘
- 固定RGBによる背景fixture追従と追加状態画像はLow。正規captureの実背景6状態と既存topic遷移テストで今回範囲を満たすため保留。
- suppressed: 0件。

## メタ
- Codex監査セッション: `019fa3de-9388-7073-bcb7-1b7ee6fe3dc5`
- fresh検証: lint、unit 384件、build、E2E型検査、focused Playwright 2件、18状態capture、画像目視。
