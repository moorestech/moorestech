# mapObjectの配置データはTransform相当のフル3要素を運ぶ

生成パイプライン（Game.MapGeneration）は樹木のスケール・回転・沈み込み（sink）を計算しているが、`PlacedMapObject` が guid+座標しか持たないため `MapInfoJson`・`va:mapData`・クライアント Instantiate の全区間でこれらが破棄され、全 mapObject が同一サイズ・同一向きで表示されていた。**配置データは Unity の Transform と同じ3要素（位置 Vector3・回転・スケール Vector3）を全区間で運ぶ**ことにする。sink は生成時に Y 座標へ畳み込み、bendFactor（Unity Terrain 木専用の風なびき）は GameObject 配置に適用先が無いため破棄する。

設計原則どおり optional フォールバックは使わず、`map.json`（generated 出力・template 手作りの両方）、`MapObjectLayoutMessagePack`、クライアントの Instantiate を一括更新する。template マップは移行スクリプトで rotation=identity / scale=1 を付与する。

## Considered Options

- **TreeInstance 相当の最小3値（rotationY / scaleWidth / scaleHeight）** — MapMaking の見た目再現には過不足ない最小形だが却下。
- **Transform 相当のフル3要素（採用）** — 将来の斜面整列オブジェクト等にもそのまま使える汎用形。

## 出所

- フル Transform の採用: ユーザー裁定 2026-08-16「transformと同じものを返す。つまり、位置、回転、大きさの3値」（最小3値の推奨を却下）
- sink の Y 畳み込み・bendFactor 破棄: agent前提（bendFactor は Terrain 木専用で GameObject に適用先が無い。風表現は BK シェーダ側にあり、描画環境同期はスコープ外のユーザー裁定 2026-08-16 と整合）
- 全区間一括更新・フォールバック禁止: agent前提（AGENTS.md 設計原則「変更の波及を恐れない」）
