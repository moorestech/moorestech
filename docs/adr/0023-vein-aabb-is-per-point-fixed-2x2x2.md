# 鉱脈AABBは点単位・点中心の固定サイズにする

## Context

自動生成される鉱脈のAABBは `VeinPlacementCore.BuildVeins` がクラスター内メンバー点の min/max を畳んで作っていた。
v8マスタは `maxObjectsPerCluster: 1〜2` / `clusterRadius: 10` なので、実際に出る鉱脈は「点1個 → 1×1×1」か
「点2個 → 最大20×?×20（Yは2点の地形高低差依存）」の二択になり、サイズが不揃いだった。

移植元の MapMaking (`TmpUnityPjt/MapMaking`) を確認したところ、あちらの `OrePlacementGenerator` は
メンバー点ごとに prefab を1体 Instantiate する点単位方式で（`Cluster = null`）、AABBを畳む処理は存在しない。
サイズは配置される prefab の `ItemMapVeinGameObject.bounds` が持ち、ベースの `BronzVeinArea.prefab` が
`m_Extent: (1,1,1)` すなわち **size 2×2×2**（`VeinSet_Stone` のみ 3×1×3 に上書き）。
つまりクラスター畳み込みは moorestech 移植時に足された差分であり、サイズ不揃いはその副作用だった。

## Decision

鉱脈の生成を MapMaking と同じ点単位へ戻し、AABBは点を中心とした固定サイズにする。

- メンバー点1個につき `PlacedVein` を1件出す（クラスター畳み込みを廃止）
- `Min = p - (1,1,1)` / `Max = p + (1,1,1)`。inclusive なので `Max - Min = 2`・実体は1辺3セルで、点はAABBの中心に来る
- 全 vein 一律。MapMaking で `VeinSet_Stone` だけ 3×1×3 だった例外は再現しない
- `maxObjectsPerCluster` / `clusterRadius` / `minDistanceBetweenOres` は MapMaking の値のまま据え置く

出所: ユーザー裁定 2026-08-20 原文「これをどのveinでも2x2x2に固定したい」「アで、また、今はまず見た目と鉱脈自体の
数のゲーム上のバランスを見たいから正確さ優先」→ 選択「ア: Min = p-1, Max = p+1（点がAABB中心）」

## Considered Options

- **ア（採択）**: `Min = p-1, Max = p+1`。MapMaking の bounds 表記 2×2×2 の忠実移植。点がAABB中心
- **イ（棄却）**: `Min = p, Max = p+1`。inclusive で実効8セルの素直な2×2×2。点がAABBの角になり対称性を失う
- **A（棄却）**: クラスター単位のまま AABB だけ 2×2×2 に潰す。鉱脈数は現状維持だが `maxObjectsPerCluster` が死に、
  MapMaking の点単位構造から更に離れる

出所: ユーザー裁定 2026-08-20（上記の選択で ア を採択）

## Consequences

- `ItemMapVeinDatastore.GetOverVeins` の判定は inclusive なので、1鉱脈が覆うブロック座標は 3×3×3 = 27セルになる。
  `Max - Min = 2` と1辺3セルは同じAABBの別表記であり、以後もこの2語で表す
- クラスター内の点が2個あるバンドでは鉱脈が2件出るため、鉱脈数と露頭数が現状（v8で約1775本）の最大2倍近くになる。
  まず MapMaking 忠実な状態で見た目と本数バランスを実機確認したいので、マスタ側で本数を絞る調整はしない
  （出所: ユーザー裁定 2026-08-20 原文「今はまず見た目と鉱脈自体の数のゲーム上のバランスを見たいから正確さ優先」）
- 露頭はAABBのXYZ中心へ置かれるため（[[2026-08-19-露頭はvein-AABBのXYZ中心へ固定配置する]]）、
  露頭の位置は生成された点そのものになる
- 非ジェネレーター層（`Game.Map` の Datastore、`VeinLayoutMessagePack`、クライアント露頭、手動オーサリング）は
  `veinGuid` + Min/Max しか知らないため無改修。変更はジェネレーター内に閉じる
- 同一点から±1広がる以上、非重なりは `minDistanceBetweenOres` が支える。スキーマ既定値を 1.5 から 4 へ上げ、
  `GenerationMasterUtil` が 4 未満の帯をロード時に弾く（出荷JSONは全件4のため実データの変更は無い）
- `PlacementSceneOffset.ToSceneSpace(List<PlacedVein>, ...)` は Min と Max を独立に `RoundToInt` しており、
  `Mathf.Round` の round-half-to-even により半整数シフトでサイズが1ずれうる。サイズ固定と非重なりの双方を
  不変条件にするため、窓原点シフトを整数へ1度だけ丸めて全veinへ同じ値を引く形へ直す
  （vein ごとに丸め直すと隣接AABBの間隔が1縮み、ノイズ空間で確立した非重なりが壊れるため。出所: agent前提・既存バグの是正）
