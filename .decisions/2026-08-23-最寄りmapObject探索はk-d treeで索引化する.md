# 最寄りmapObject探索はk-d treeで索引化する

2026-08-23 ユーザー裁定

## 背景（症状）

「木を伐採して原木を入手する」チュートリアル中にクライアントが重い。
`MapObjectPin.Update()` が毎フレーム `MapObjectGameObjectDatastore.SearchNearestMapObject` を呼び、
`_allMapObjects`（2002件）を全走査していた。フィルタ前に `MapObjectGameObject.MapObjectGuid`
（`=> new(mapObjectGuid)` ＝ string→Guid パース）を必ず読むため 2002回/フレームのGuidパースが走る。
guid一致した個体（木663本）にだけ `transform.position` と `.magnitude` が走るので、
小石(23個)チャレンジより木(663本)チャレンジが体感で重くなる。

露頭側（`OutcropGuidIndex`）は guid別Dictionaryまでは実装済みだが、
mapVein 1775件 / 11 guid ＝ 平均161件をやはり毎フレーム線形走査しており同じ病気。

## 決定

最寄り探索を **k-d tree による空間索引**に置き換える。挙動（毎フレーム厳密な最寄りを取り直す）は変えない。

出所: ユーザー裁定 2026-08-23 原文「本質的な探索の高速化をしたい。今回の場合一対多の構成なんだから、
すべてのMapObjectをチャンクごとのdicrionaryに格納して、プレイヤー側は自分のいるチャンクの
マップオブジェクトのリストから距離チェックするとかでよくない？」→ 追加原文「固定セルじゃなくて
もっと賢い分割手法がつかえないの？」→ 選択「k-d tree」

## 棄却案

- **均一グリッド＋同心リング拡張**（サーバ `Game.MapGeneration/.../Util/SpatialGrid.FindMinDistance` と同形・前例一致）
  棄却理由: ワールドは 5000m×5000m（`TerrainGenerationConfig` terrainWidth 1000 × gridSizeX 5）で、
  対象はバイオーム偏在（forest/woods/jungleは密、desert/alpineは空）。
  探索コストが「最寄りまでの距離」の2乗に乗る（600m先なら ring19 = 1,521セル走査、ほぼ全部が空セル）。
  森で破綻しない cellSize と砂漠で破綻しない cellSize が両立しない。
- **quadtree**
  棄却理由: 探索コストは k-d tree と同等だが、動的な挿入削除という利点が今回効かない
  （点集合は起動時生成後は不変・破壊のみ）。ノード容量と最大深度という定数が2つ残り、
  子ノードオブジェクトで実装行数も増える。

## 決め手

点集合が完全に静的（起動時に全件Instantiate、以後は座標不変・破壊のみ・追加なし）なので
k-d tree の唯一の弱点（挿入削除）が発生しない。かつ探索コストがマップの広さにも
「最寄りがどれだけ遠いか」にも依存せず、チューニング定数がゼロ。

## リンク

- 対象: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs`
- 対象: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGuidIndex.cs`
- 呼び出し元: `Client.Game/InGame/Tutorial/MapObjectPin.cs` / `VeinPin.cs`

## 追加裁定: 適用範囲は mapObject と露頭の両方（2026-08-23）

汎用のk-d tree索引を1つ作り、`MapObjectGameObjectDatastore`（2002件/7guid）と
`OutcropGameObjectDatastore`（1775件/11guid）の両方を載せる。`OutcropGuidIndex` はその利用側になるか廃止する。
索引は「木」「露頭」というドメイン語彙を知らず、座標と候補可否だけを知る。

出所: ユーザー裁定 2026-08-23 選択「汎用索引を1つ作って両方に使う」

棄却案:
- **mapObjectだけやる** — 露頭は161件/guidで実害が小さいため。棄却理由: 同じ形の最寄り探索実装が2つ並存し、
  5km全域マップでは鉱脈もバイオーム偏在で同じ問題を起こす
- **汎用索引を作り露頭は別PR** — 検証を1系統ずつに切り分けられる。棄却理由: bdのreadyが100件溢れており、
  後続PRが放置されて二重実装が残る蓋然性が高い

## 追加裁定: 破壊の反映は dirty フラグ→次の探索で再構築（2026-08-23）

破壊イベントで該当guidに dirty を立てるだけにし、次にそのguidを探索したときに生存個体だけで木を再構築する。
索引は座標しか知らず「破壊済み」「候補可否」という上位概念を持たない（判断は Datastore 側で行い、索引にはプッシュする）。

出所: ユーザー裁定 2026-08-23 選択「dirtyフラグ→次の探索で再構築」

棄却案:
- **索引は不変にして探索時に IsAvailable で候補外す** — 実装は最も単純。棄却理由: 拠点周辺を伐採し尽くすと
  最良値が∞のまま降りるので枝刈りが効かず、伐採本数に比例して探索が重くなる。索引が上位概念を知ることにもなる
- **論理削除＋死率25%で再構築** — 再構築回数を抑えられる。棄却理由: 25%に根拠が無く、
  663点の再構築が60µsしかかからないので回数を抑える動機自体が弱い。索引内部に状態が増えテスト面も膨らむ

## 追加裁定: 今回のスコープは索引化まで（2026-08-23）

k-d tree索引と、同時に見つかった地雷（死コードのLookAt削除・LogErrorの1回化・Guidパース済み保持・
WorldPinStateStoreのLINQ除去）までを今回のPRとする。Webオーバーレイへの毎フレーム配信
（JSONシリアライズ＋WebSocket publish）は未実測なので今回は触らず、索引化後に実測して残っていれば次のPRで扱う。

出所: ユーザー裁定 2026-08-23 選択「索引化まで。実測してから次を決める」

棄却案:
- **Web配信の間引きまで一気に** — 棄却理由: epsilon拡大も配信レート固定もピン追従のなめらかさを落とすトレードオフで、
  効果が未実測のまま見た目を下げる判断になる
- **先にProfilerで実測してから設計確定** — 棄却理由: 2002回/frameのGuidパースと全走査は測るまでもなく不要であり、
  索引化は実測結果によらず入れる価値がある

## agent前提（原則・前例・実測から自明として質問せず決めた事項）

- 距離は3Dのまま維持。k-d treeも3次元で構築（出所: agent前提。`SearchNearestMapObject`・`OutcropGuidIndex`とも現状3D距離で、挙動を変えないため）
- 索引は座標を構築時に焼き込む（出所: agent前提。mapObject・露頭とも Instantiate 後に transform を動かす箇所が存在しないことを確認済み。毎フレームの `transform.position` ネイティブ呼び出しが消える）
- `MapObjectPin`/`VeinPin` の `transform.LookAt` + `Quaternion.Euler` は削除（出所: agent前提。`MapObjectPin.prefab` はTransformとスクリプトのみでRendererが無く、`GameSystem.prefab` 側のインスタンスにも子の追加が無い＝死コード）
- `MapObjectPin` の対象不在 `Debug.LogError` は「対象ごと1回」に（出所: agent前提。`VeinPin.cs:73` の同型前例に揃える）
- `MapObjectGameObject.MapObjectGuid` はパース済みGuidを保持（出所: agent前提。`=> new(mapObjectGuid)` は呼ぶたびstring→Guidパース）
- `WorldPinStateStore.SetPin`/`CreateData` のLINQアロケーションを除去（出所: agent前提。毎フレーム走る経路のため）
