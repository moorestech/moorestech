---
name: gear-sign-conversion-not-idempotent
description: 歯車ワールド符号のisReverse反転変換は冪等でない — 再実行すると二重反転する。未変換の複合プレハブ4件が残存
metadata: 
  node_type: memory
  type: project
  originSessionId: 106f7eb8-1ee7-458f-a90c-bccc4566e9ba
---

feature/gear-rotate (2026-07-05プラン) の歯車プレハブ変換スクリプト（負のワールド符号パーツの `isReverse` を反転）は**冪等ではない**。ワールド符号はTransformポーズの性質であり isReverse を反転しても変わらないため、再実行すると負符号パーツが毎回反転される（プラン本文の「変換は冪等」という記述は誤り。実際に GearChainPole/CompactGearChainPole が二重反転しかけた）。

**Why:** 将来「変換を全プレハブに再実行」する際、変換済みプレハブを含めると見た目が壊れる。

**How to apply:**
- 再実行する場合は変換済みプレハブ（Task5の10件＋フォローアップの14件、`.superpowers/sdd/progress.md` 参照）を除外するか、変換済みマーカーを別途持つ
- `directionMode == AlwaysForward` の要素は反転対象外（実行時にワールド符号を無視するため、反転すると逆に見た目が変わる）
- 未変換の複合/ネストプレハブ4件が残存: iron furnace with bellows, Rotary_mortor, Station_Cargo, Station_Locomotive（負符号パーツあり、per-instance override対応が必要な別パス）
- Animator駆動パーツの `GearRotationDirection` パラメータはネットワーク反転のみ提供、設置方向一貫性は無い（軸概念が無いため）
