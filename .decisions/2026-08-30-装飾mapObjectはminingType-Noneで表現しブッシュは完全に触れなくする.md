# 装飾mapObjectはminingType: Noneで表現しブッシュは完全に触れなくする

出所: ユーザー裁定 2026-08-30 原文「インタラクトできないmap objectを作りたい（bushがピンされるのがうざい）」→ Q1選択「A 完全な装飾物」、Q2選択「miningType に None を追加」

## 決定
- ブッシュ系mapObjectは完全な装飾物にする。攻撃対象にならず、HPバー・ツールチップ無し、ドロップ無し、サーバーはダメージ要求を弾く。結果としてearnItemピン候補からも外れる（原木は木のみから）
- マスタ表現は `map.yml` の `miningType` に第3値 `None` を追加（`PickUp | Mining | None`）。`None` で `earnItems` が非空ならマスタ検証で弾く

## 棄却案
- 採掘可のままピン候補からだけ除外するフラグ — 原木導線を残すが「触れるのに誘導されない」二重状態が増える
- earnItemピン解決を主産物条件で絞る間接方式 — 意図が暗黙
- 別軸 `interactionType` の並列追加 — Mining かつ decoration という矛盾状態が表現できる
- スキーマ据え置き・空定義で表現 — 意味が暗黙でHPバー等の見た目が残る

## 前例
- `mapVeins.handMiningType: none | minable` の switch と同形

## 追記（同日）
- クライアントはレイ判定層で除外（ユーザー選択「A」。棄却: NotInteractable outcome＋ツールチップ方式）
- 対象: ブッシュ系13件＋メサ崖・地層系26件（原文「ブッシュ系と、あとメサのでかい崖」→ 崖は「A 5グループ26件すべて」。棄却: BigMesaのみ／15件案）
- 詳細は docs/adr/0043-non-interactive-decoration-map-objects.md
