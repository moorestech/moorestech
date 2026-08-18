決定: map.ymlの`terrainSurroundEffectType`（PR #1145新設・MapObjectKindSplitterの分類正本）はmapmaking-visual-parity-v2 planでは削除せず、bd moorestech-pt8（地形見た目のサーバー焼き移設）でSplitterごと自然死させる。それまでplan Task 3の新規mapObject約94件にはkindから機械決定（tree→treeRootPatch、rock/pebble→rockBareGround）で必須出力する。

棄却案: 本planで即削除しMapObjectKindSplitterの分類元を差し替える案（soundEffectType相乗り復帰／転送レイアウトへ種別キー追加／addressablePathプレフィックス導出のいずれか）。

理由: 代替分類はどれもpt8で捨てる過渡実装の作り直しにしかならない。soundEffectType相乗りはPR #1145レビューで潰した設計への逆行。pt8後はサーバーが生成時に配置元prototype/objectConfigを知るためマスタ側の分類フィールド自体が不要になる。

リンク: docs/superpowers/plans/2026-08-17-mapmaking-visual-parity-v2.md（委譲先テーブル） ADR-0012 [[2026-08-17-PR1145のクラスタ3キーは後で消える前提で現状維持する]]
