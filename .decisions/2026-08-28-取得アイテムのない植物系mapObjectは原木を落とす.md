# 取得アイテムのない植物系mapObjectは原木を落とす

日付: 2026-08-28

## 決定
- `map.json` の `earnItems` 空53件を削除せず、全件に「原木」を割り当てる。新規アイテムは追加しない
- 大型サボテン25件（Cacactus / Grocactus / Saguaro / Senita）は hp100・interval10 据え置きで 原木1〜4（既存の木と同一）
- 低木・草花28件（ブッシュ / Opuntia / Mountains Bush / Olivebush / Savanna Bush / Brittlebush / DryGrass / Peanut / WildflowersYellow）は hp10・interval10 で 原木1個固定、miningTools を「石の斧25 + 石器10」へ統一して1振りで採れるようにする
- Opuntia はウチワサボテンで背が低いため低木側
- 未配置22件は earnItems を埋めるだけ。generation.json への配置は別タスク
- `MapObjectMasterUtil.Validate` に earnItems 空のエラー検査を追加し、テストを1本足す

## 棄却案
- 未参照22件を削除して配置済み31件だけ埋める — ユーザーは削除でなく「適切なアイテムを設定する」を選んだ
- 空53件を全てマスタから削除 — サボテン・草花が世界から消える
- 木質系は原木・草花系は木の棒で描き分け／植物繊維等を新規追加 — 原木で統一を採択
- 53件とも hp据え置き・原木1〜4 — 草花1本から最大40原木が出て木を切る意味が消える
- hp据え置きで全件1個固定 — 草花の10回振りの重さが残る
- サボテン全部（Opuntia含む）を大型側 — 低いウチワサボテンに10回振りを要求する
- 草花13件を PickUp にして道具不要で拾える — 木を切る動機が弱まる
- Unityで各prefabのbounds高さを実測して自動分類 — 実測工程を足さず裁定で線引きした
- マスタ修正のみでコードは触らない — 将来また空が生まれても検知されない

## 理由
殴っても何も落ちないmapObjectが53件あり、うち31件は実際に生成されてプレイヤーが空振りする。items.json に植物系アイテムが無いため、既存の原木で統一するのが用途不明のゴミアイテムを増やさない最短路。見た目の大きさとリターンを合わせるため、背丈で2段階に分ける。

## リンク
- docs/adr/0037-plant-mapobjects-drop-log.md
- docs/adr/0036-rock-mapobjects-drop-stone-not-pebble.md（同じmap.jsonのドロップ整理の岩版）
