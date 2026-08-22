# mapObject独立散布entriesにスポーン距離帯（bands）を持たせる

## Context

現行 v8 マスタ（`../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/generation.json`）では、
チュートリアル「小石を3個拾う」（`challenges.json` の `mapObjectPin` が指す `c74efe49-…` 小石）を生成する
エントリが存在しない。grassland/forest の `objectConfig.entries` は全て `prefabs: []` で、
`ObjectPlacementGenerator.GenerateForBiome` は空 GUID のエントリをスキップするため、小石は一切置かれない。
`MapObjectPin` はプレイヤー最寄りの該当 GUID を探すだけで、存在しなければ LogError になる。

一方、鉱脈には既にスポーン地点中心の距離帯がある。`OreEntry.bands[]`（`OreBand.outerRadiusMeters` 昇順、-1 は無限）を
`SpawnDistanceRingPlanner.BuildRings` がリング `[Inner, Outer)` へ変換し、`OreEntryPlacer` がリングごとに Poisson 散布を回して
クラスタ中心のスポーン距離（`TerrainDimensions.SpawnWorldX/Z` 基準のワールド座標距離）でリング判定する。

mapObject の独立散布（`BiomeObjectConfig.ObjectEntry`）は flat な `density`（非クラスタ時・1ha あたり）と
`clusterCount`（`useClusterMode` 時）で量が決まり、スポーン距離の概念が無い。

## Decision

`biomeObjectConfig.entries[]` に鉱脈と同型の `bands[]` を導入し、flat な `density` と `clusterCount` を bands 内へ移す。

- `bands[]` の各要素: `outerRadiusMeters`（-1 = 無限・最外周）と `density`（1ha あたり）。
  `outerRadiusMeters` 昇順でリング化し、リングごとに Poisson を回してリング内の候補だけ採用する
- 量の指定は `density` 1本へ統一し `clusterCount` は持たせない。クラスタモードの中心数も同じ `density` から決める
  （面積非依存の個数指定は近傍リングでほぼ 0 個に丸まり、本 ADR の目的が達成できないため）。
  [[2026-08-21-散布バンドの量指定はdensityへ統一しclusterCountを廃止する]] が本節のこの点を上書きする
- リング判定の基準点: 非クラスタ散布は各候補点そのもの、クラスタモードはクラスタ中心（鉱脈と同じ）
- その他のパラメータ（noise・slope・scale・sink・objectsPerCluster・clusterRadius・木距離）はエントリ共通のまま
- `clusterEntries`（階層岩クラスタ）と `treePlacement` は変更しない
- 既存 master の全 entries（8 バイオーム）を `bands=[{outerRadiusMeters:-1, density:現値}]` へ機械変換する。
  クラスタモードの entry は `density = clusterCount / 100` 相当へ換算する。
  スキーマ上 flat `density`/`clusterCount` は削除し、optional や既定値フォールバックで吸収しない
- 小石は grassland と forest の entries に `prefabs=[小石]`、`bands=[{近傍リング, density>0}, {-1, density 0}]` で追加する。
  近傍リングの半径・密度の具体値は master 上の調整値（agent 前提・初期値は plan に記載）
- スポーン半径 15m の `SpawnPlacementExclusionStage` は維持する

出所: ユーザー裁定 2026-08-21 原文「map object等の生成に『スポーン地点からの近さによる生成確率』を実装したい。veinにもあるやつ」
→ 選択「独立散布entriesのみ」／原文「個数は指定しない。他と同じようにノイズの頻度みたいな感じで指定したい」／
最外周は選択「生成しない」。[[2026-08-21-スポーン距離帯は独立散布entriesのみに持たせる]]
[[2026-08-21-小石は近傍帯のみで密度指定し最外周は生成しない]]

## Considered Options

- **独立散布 entries のみ（採択）**: 小石要件を満たし、スキーマ波及を entries 配列に限定できる
- **entries + clusterEntries（棄却）**: 階層岩クラスタの primary clusterCount も帯制御。序盤の大岩密度も調整可能だが改修範囲が広がる
- **entries + clusterEntries + 木（棄却）**: 木はノイズ合成密度で決まるため帯の掛け方が別設計になり 2 系統化する
- **最外周にも低密度で撒く（棄却）**: 依頼原文「スポーン地点に生成されればそれでいい」に反する
- **個数指定（棄却）**: 他 entries と量の指定方法が割れる。ユーザーは density 指定を選択

出所: ユーザー裁定 2026-08-21（AskUserQuestion 2問の選択・自由記述）

（以下は初回提示で拒否され再提示しなかった案。裁定ではなく経緯として記録）
- スポーン専用の配置設定（`spawnObjectConfig`）を新設してバイオーム無関係に撒く案は、ユーザーが「veinにもあるやつ」と
  既存帯方式を指定したため採択されなかった

## Consequences

- 量の意味が「全域一様」から「スポーン距離リングごと」へ変わる。既存エントリは単一無限リングへ機械変換するため、
  非クラスタ散布の挙動は不変。クラスタモードは `clusterCount`（面積非依存の個数）から `density`（1ha あたり）への
  換算を挟むため厳密には不変でなく、タイル面積 1000x1000m の想定で一致する換算値になる
- 5x5 タイル生成ではリング判定がワールド座標距離なので、タイルをまたぐリングも鉱脈と同じく正しく切れる
- 小石は近傍リング内でも 15m クリアランス・バイオームマスク・ノイズで間引かれるため、実数は密度から目減りする。
  チュートリアルが要求する 3 個を下回らないよう密度は余裕を持たせる
- `spawnWorldPosition` は探索で動くが、帯はそれに追従する（鉱脈と同じ基準点）
- ObjectIndependentPlacer に距離帯ループが入り、クラスタモード/非クラスタの両経路が帯ごとに回る
