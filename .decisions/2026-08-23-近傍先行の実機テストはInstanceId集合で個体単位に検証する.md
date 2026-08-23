# 近傍先行の実機テストはInstanceId集合で個体単位に検証する

- 日付: 2026-08-23
- 文脈: plan `docs/superpowers/plans/2026-08-23-mapobject-near-field-instantiation.md` Task 5 / ADR `docs/adr/0030-mapobject-near-field-first-instantiation.md` / bd: moorestech-4z88

## 決定

`MapObjectNearFieldStartupTest` の近傍アサーションは、`Object.FindObjectsByType<MapObjectGameObject>` から `InstanceId` 集合を作り、近傍layoutの全 `InstanceId` がその集合に含まれることを検証する形にする。planのStep 3が指定していた `SearchNearestMapObject` ベースの存在確認は破棄する。

## 棄却した案

planのサンプルコードどおり `SearchNearestMapObject(guid, position)` の非null検査で近傍個体の存在を確かめる案。境界の正確性は単体テスト（`MapObjectLayoutDistanceOrderTest`）が担っているとして、実機テストはスモーク程度に留める整理。

## 理由

`SearchNearestMapObject` はguid単位の最寄り探索であり、**同一guidの別個体が1つでも生きていれば通る**。同一guidは数千個規模で並ぶうえ、後着生成で遠方個体が次々と索引へ載るため、近傍個体が未生成でもアサーションが通ってしまう。これでは要件R1の受け入れ条件「EditModeInPlayingテストで近傍待機後に近傍layout全件が生成済み」を個体単位で検証できず、テストが実質的に空振りする。

planのサンプルコードは設計時のスケッチであり、plan自身の要件R1と食い違っていた。**要件を正とする。**

副次的な利点として、planが注記していた「テストワールドに初期破壊済み個体がある場合のスキップ条件」が不要になる。破壊済みでもGameObjectは残り `InstanceId` は集合に載るため、破壊状態に依存しない検証になる。

## 併せて裁定したこと

生成をskipされた個体（prefab/master欠落・スナップショット欠落・instanceId重複）宛の `MapObjectPendingStateLedger` エントリが `TryConsume` されず残る件は**現状維持**。件数はskip個体数で上限が付き実害が無く、異常系は既に `LogError` で可視化されている。

## リンク

- [[docs/adr/0030-mapobject-near-field-first-instantiation.md]]
- [[.decisions/2026-08-23-mapObject起動生成は近傍先行と時間予算分散にする.md]]
