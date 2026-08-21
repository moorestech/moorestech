# MapMakingとの見た目同一化は樹種ごとの個別mapObject登録で行う

generated ワールドの木・岩・草の見た目を MapMaking プロジェクト（TmpUnityPjt/MapMaking の MapGenerator プリセット）と同一にする。マスタのスキーマは「1 mapObjectGuid = 1 プレハブ = 1 見た目」であり、この原則を維持したまま、**MapMaking の各バイオームプリセットが参照する BK Pure Nature の樹種・岩（約60樹種＋岩9系統）をそれぞれ個別の mapObject として登録し**、generation.json の `treePlacement.prototypes[].mapObjects[]` をプリセットと同じ構成にする。1つの mapObject に見た目バリエーション機構を足す案は採らない。

- 同期対象バイオームは有効4（Forest/Grassland/Savanna/Mesa）をプリセットどおり、Jungle/Woods は旧スキーマに残る樹種リストを意図として移植（バイオーム自体は無効のまま）。disabled エントリは意図的オフとして外す。
- MapMaking がtreePlacementで植えている岩（Boulder/Stone/Pebble系）も同じ機構で含める。
- 新規 mapObject の hp・ドロップ・採掘設定は既存値の複製で統一し（木=既存「木」、岩=石ドロップ系）、マスタはスクリプト生成する。バランス調整は後から JSON 編集で行う。
- クライアント側は Bush.prefab と同型の「BK プレハブをネストするラッパープレハブ」を樹種ごとに機械生成して Addressables 登録する。
- 草（Terrain detail）は generation.json に既に MapMaking プリセット由来のデータが入っており、欠けている treeDistanceFilter / objectDistanceFilter をクライアントが受信済み mapObject 配置から距離場を構築して有効化する。
- template（手作り）マップと既存「木」（Birch）は見た目現状維持。同一化は generated ワールド専用。

## 出所

- スコープ（樹種・スケール回転・草を同期、描画環境は対象外）: ユーザー裁定 2026-08-16「1 それは直したい 2も直したい 3 プリセットの意図を汲みたい 4 それはまぁ別に」
- バイオーム範囲・岩を含める・採掘設定の複製・草の距離場復元・template現状維持: ユーザー裁定 2026-08-16（.decisions/ 同日レコード群参照）
- 個別 mapObject 方式（バリエーション機構を作らない）: agent前提（既存スキーマ原則「1 guid = 1 見た目」の維持。generation 側の等確率選択配列が樹種分散の前例）
- ラッパープレハブの機械生成: agent前提（Bush.prefab の先行パターン踏襲。約70個の手作業は事故源）
