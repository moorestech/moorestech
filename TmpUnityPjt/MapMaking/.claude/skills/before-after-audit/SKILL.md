---
name: before-after-audit
description: "変更前後のスクリーンショットを撮り、外部監査で差分を確認するワークフロー。\n\nUse When:\n- 地形やビジュアルに影響する変更を行う\n- 修正前後の比較確認を求められた\n- パラメータ調整の効果を検証する"
---

# Before/After 外部監査

## 重要ルール

- **Scene Viewの視点を絶対に変えない**。ユーザーが設定したScene Viewのカメラアングルは比較の基準。視点を動かすと before/after の差分比較が無意味になる。
- **Scene Viewを第一の検証手段とする**。MicroVerseの変更（地形・テクスチャ・木・オブジェクト配置）はScene Viewで全体を俯瞰しないと効果が確認できない。
- Game Viewは固定カメラなので変更箇所が映らない場合がある。Game Viewだけで「変化なし」と判断してはならない。
- 変更適用後は**必ずScene Viewのスクリーンショットを`Read`で目視確認**してから外部監査に進む。

## 手順

1. **Before**: `uloop screenshot --window-name Scene --output-directory .artifacts/`
2. **変更を適用**（Scene Viewの視点は触らない）
3. **After**: `uloop screenshot --window-name Scene --output-directory .artifacts/`
4. **目視確認**: AfterのScene Viewスクリーンショットを`Read`で開き、変更が反映されているか確認する。反映されていなければ原因を調査する。
5. **外部監査**:

```bash
node tools/codex-audit.mjs <before.png> <after.png> \
  --ask "【評価基準】…【確認観点】…"
```

6. A評価でなければ自主修正 → 再スクショ → 再監査（視点は変えずに繰り返す）
