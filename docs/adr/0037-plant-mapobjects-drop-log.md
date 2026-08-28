# ADR 0037: 取得アイテムのない植物系mapObjectに原木を割り当てる

## Status
Accepted (2026-08-28)

## Context
`moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json` の mapObjects 195件のうち53件が `earnItems: []` で、殴っても何も落ちない。
内訳は MesaDesert のサボテン群（Cacactus1-4 / Grocactus1-8 / Opuntia1-4 / Saguaro1-5 / Senita1-8）と、各バイオームの低木・草花（ブッシュ / Mountains Bush1,3 / Oasis Olivebush1-3 / Savanna Bush1-3 / Brittlebush1-4 / DryGrass1-4 / Peanut1-3 / WildflowersYellow1-4）。
53件のうち31件は generation.json から生成されており、プレイヤーは実際に殴れて空振りする。残る22件（Cacactus / Grocactus / Opuntia / Olivebush / Savanna Bush）はどのマスタからも参照されておらず、世界に一度も出ていない。map.json 全体の未参照22件はこの22件と完全に一致する。
既存のドロップ品は ADR 0036 適用後で 石85 / 原木53 / 小石4 の3種のみで、items.json に植物・繊維系のアイテムは存在しない。
`earnItemHpInterval` は「HPがその値を跨ぐごとに1回ドロップ」の意味（`VanillaStaticMapObject.CalculateEarnedCount`）で、hp100/interval10 なら破壊までに10回、hp10/interval10 なら破壊時の1回だけ落ちる。
マスタスキーマ（VanillaSchema/map.yml）に「配列に最低1件必須」を表す機構は無く、空配列を機械的に弾く場所は `Core.Master.Validator.MapObjectMasterUtil.Validate` しかない。

## Decision
- earnItems 空の53件を削除せず、全件に「原木」(`aafce615-6c30-48c4-a29e-3c5b3266748f`) を割り当てる。植物繊維等の新規アイテム追加は行わない。
  出所: ユーザー裁定 2026-08-28 原文「取得アイテムがないmap objectを消して全部何かしらの取得アイテムを割り当てたい」→ 自由記述「適切なアイテムを設定する」→ 選択「全部原木で統一」
- 大きさで2段階に分ける。背の高いサボテン25件（Cacactus1-4 / Grocactus1-8 / Saguaro1-5 / Senita1-8）は hp100・earnItemHpInterval10 据え置きで 原木 min1/max4（既存の木と同一）。低木・草花28件（ブッシュ / Opuntia1-4 / Mountains Bush1,3 / Olivebush1-3 / Savanna Bush1-3 / Brittlebush1-4 / DryGrass1-4 / Peanut1-3 / WildflowersYellow1-4）は hp10・earnItemHpInterval10 で 原木 min1/max1。
  出所: ユーザー裁定 2026-08-28 選択「大きさで2段階に分ける」「背の高いサボテンのみ大型」
- Opuntia（ウチワサボテン）は背が低いため低木側へ寄せる。出所: ユーザー裁定 2026-08-28 選択「背の高いサボテンのみ大型」
- 低木・草花28件の `miningTools` を他190件と同じ「石の斧 damage25/attackSpeed1 + 石器 damage10/attackSpeed2」に統一する。ブッシュだけ持っていた damage5 の単独設定はここで揃える。hp10 なのでどちらの道具でも1振りで原木1個。
  出所: ユーザー裁定 2026-08-28 選択「全部斧で1振りに揃える」
- 未配置22件は earnItems を埋めるだけで generation.json は触らない。実際に生やす作業は別タスクへ送る。
  出所: ユーザー裁定 2026-08-28 選択「マスタ設定のみ・配置は別タスク」
- `MapObjectMasterUtil.Validate` に「earnItems が空の mapObject はエラー」を追加し、`MapObjectMasterValidationTest` にテストを1本足す。
  出所: ユーザー裁定 2026-08-28 選択「Validatorに空検査を追加」
- soundEffectType は全件 tree のまま据え置く。出所: agent前提（今回の裁定は取得アイテムと採掘手数に限られる）
- ブッシュと小石に残るスキーマ外キー `earnItemHps` は触らない。出所: agent前提（VanillaSchema/map.yml に無くローダーが読まないため無害）
- 低木・草花の earnItemHpInterval を 5 でなく 10 に揃える。hp10 では 5 でも 10 でも破壊時1回で挙動は同一のため、値を統一する側を採る。出所: agent前提

## Considered Options（実提示・棄却）
- 未参照の22件を map.json から削除し、配置済み31件だけにアイテムを割り当てる — 棄却（ユーザー裁定、削除しない）
- 空の53件を全てマスタから削除する — サボテン・草花が世界から消え generation.json の該当エントリも削る必要がある。棄却（ユーザー裁定）
- 木質系は原木・草花系は木の棒と既存アイテムで landscape を描き分ける — 棄却（ユーザー裁定、原木で統一）
- 植物繊維等の新規アイテムを items.json へ追加する — アイコンと用途レシピの設計が別途必要になる。棄却（ユーザー裁定）
- 53件とも既存の木と完全同一（hp/interval据え置き・原木1〜4）にする — 草花1本から最大40原木が出て木を切る意味が消える。棄却（ユーザー裁定）
- hp据え置きのまま全件 原木1個固定 — 草花の10回振りの重さが残る。棄却（ユーザー裁定）
- Opuntia を含むサボテン全部を大型側にする — 低いウチワサボテンに10回振りを要求する。棄却（ユーザー裁定）
- 草花13件を miningType=PickUp にして道具不要で拾えるようにする — 木を切る動機が弱まる。棄却（ユーザー裁定）
- Unity で各prefabのbounds高さを実測して大型/低木を自動分類する — 棄却（ユーザー裁定、実測工程を足さず線引きを裁定で決めた）
- マスタ修正のみでコードは触らない — 将来また空が生まれても検知されない。棄却（ユーザー裁定）

## Consequences
- 原木の供給が大きく増える。MesaDesert では木が生えない代わりにサボテンから原木が出るようになり、砂漠バイオームでの木材詰まりが解消する。バランス調整が必要なら min/max の側で行う。
- ブッシュの採掘が2振りから1振りへ軽くなる（石の斧 damage5 → 25）。
- Validator 追加後は map.json の195件すべてが earnItems 非空である必要がある。PickUp の4件（小石 / Pebble1〜3）は既に原木でなく小石を持っているため影響しない。
- 未配置22件は定義だけ埋まり見た目は変わらないため、この変更のうちプレイで観測できるのは配置済み31件のぶんだけ。
- 変更は `moorestech_master` の map.json と本repoの Validator・テストに分かれる。moorestech_master 側も別PRを立て、本repoの `.moorestech-external-revisions.json` のピンをそのpush済みコミットへ更新する。
- 関連: [[2026-08-28-取得アイテムのない植物系mapObjectは原木を落とす]] / ADR 0036（同じ map.json のドロップ整理の岩版）

## Follow-up: 未配置22件の配置（2026-08-28、bd moorestech-zlp7）

ユーザー指示により、別タスクへ送っていた22件の配置を同一PRで実施した。generation.json への追加は次のとおり。すべて既存の同種エントリを雛形にした agent前提。

- Cacactus1-4 → mesa の treePlacement（Saguaro を雛形。scale 0.5〜1.0、clusterNoiseThreshold 0.45）
- Grocactus1-8 → mesa の treePlacement（Senita を雛形。clusterNoiseThreshold 0.4）
- Opuntia1-4 → mesa の objectConfig（Brittlebush を雛形。scatter 1.5点/ha、scale 0.8〜1.5）
- Olivebush1-3 → desert の objectConfig（scatter 1.0点/ha、scale 0.8〜1.4）
- Savanna Bush1-3 → savanna の treePlacement（草原ブッシュ Bush1 の設定を流用）

閾値を既存より上げたのは、`TreePlacementEntry` の実装で clusterNoiseThreshold が高いほど棄却が増える（＝希少になる）ため、既存のサボテン密度を薄めずに種類だけ増やす意図。

### 調査で判明した Context の訂正

本ADRは「53件のうち31件は generation.json から生成されており、プレイヤーは実際に殴れて空振りする」と書いたが、実測では31件のうち16件（Brittlebush1-4 / DryGrass1-4 / WildflowersYellow1-4 / Peanut1-3 / ブッシュ）は `pointsPerHectare: 0` または `disabled: true` で実際には生成されていない。現に世界へ出ているのは Bush1・Bush3（草原）と Saguaro1-5・Senita1-8 の15件だけ。これらの密度再調整は本ADRの範囲外とし、別途裁定を仰ぐ。
