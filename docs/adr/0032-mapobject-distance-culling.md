# ADR 0032: 通常mapObjectは350mで距離カリングし遠景ランドマークを残す

- Status: Accepted
- Date: 2026-08-24
- 関連: bd moorestech-ara / `.decisions/2026-08-24-mapObject遠景ランドマークはmaster区分で350mカリングから除外する.md`

## Context

MainGameにはmapObject由来の`MeshRenderer`が128,766個、`MapObjectGameObject`が30,764個存在し、
`MapObjectGameObjectDatastore`は後着生成完了後も全個体を距離無制限で描画対象に残す。
URP設定最適化後の同一ゲーム状態で、通常mapObjectだけを350mで非表示にすると15.7msから12.4msへ改善した。
一方、mesaの崖・岩山系440個を残すと14.3msとなるが、遠景ランドマークとして必要なシルエットを維持できる。

距離カリングの除外を`mapObjectName`の前方一致で判定すると、表示上の役割が名前へ隠れ、modや新規アセットが
コード変更なしに同じ役割を宣言できない。mapObject masterは既にアドレス・採掘種別・効果音など個体種の性質を持つ正本である。

## Decision

- 全mapObject masterに必須enum `distanceVisibilityType`を追加し、`cullable`または`landmark`を明示する
- 通常値は`cullable`とし、出荷v8の`BigMesa_*`・`ThinMesa_*`・`StratMesaSharp_*`・`Boulders_*`・`BigBoulders_*`を`landmark`にする
- `cullable`は現在描画中のカメラから350mを超えると描画を休止し、340m以内へ戻ると再表示する
- `landmark`は距離カリングへ登録せず、既存LODに任せて常時描画可能にする
- 距離カリングはRendererだけを切り替え、GameObject・Collider・破壊/HP同期・最寄り探索索引を維持する
- カメラ切替は`CameraManager`がUniRxで通知し、距離判定はUnity `CullingGroup`の距離band変化通知で駆動する
- 一度に多数のband変化が届いた場合は時間予算で表示反映を分散する
- mapObject wrapper生成時に全RendererのLight ProbeとReflection ProbeをOffへ固定し、生成済みwrapperもUnity Editor経由で再生成する
- 既存コミット`26d341c5c`のSSAO Source=Depth / AfterOpaque / Downsample、Shadow Distance=120、Cascades=2を同じPRへ含める

出所:
- ユーザー裁定 2026-08-24「mesaの崖や岩山系オブジェクトはこれの対象外にしてほしい」
- ユーザー裁定 2026-08-24「ok、今回の最適化タスク全体で1PRを作ってほしい」
- ユーザー裁定 2026-08-24: masterの表示区分を正本にする案と、SSAO/シャドウ＋350mカリング＋probe無効化のPR範囲を承認
- agent前提: カメラ基準・340/350mヒステリシス・Renderer限定・UniRx通知・`CullingGroup`・時間予算分散

## Considered Options

### 採択: mapObject masterの必須表示区分

modを含む各mapObject種が表示上の役割をデータで宣言でき、クライアントの汎用カリング機構へmesa固有名を持ち込まない。

### 棄却: mapObject名の前方一致

実測対象の5系列だけなら短く書けるが、名前変更と新規ランドマーク追加がコード変更になり、役割がデータから読めない。
出所: ユーザーへ提示し、master表示区分を承認したことで不採択。

## Consequences

- `VanillaSchema/map.yml`の変更により、テストmod・sandbox・`moorestech_master`の全mapObject JSONを一括更新する
- `moorestech_master`変更は別repoのcompanion PRが必要で、本repoのpinはそのpush済みコミットを指す
- カリング外の個体もサーバーイベントを受け、再表示時に最新の破壊/HP状態を反映する
- 距離外Colliderは残るため、今回の変更はMeshColliderの起動時間・メモリ問題を解決しない
- 遠景ランドマーク440個の最下位LODは実測時点ですでに選択済みであり、さらに軽くするにはimposterまたはLOD追加が別途必要
- Renderer個数由来の`UpdateRendererBoundingVolumes`固定税は残り、根治にはRenderer統合またはBatchRendererGroup化が別途必要
