# 0107: 電気ベルトコンベアも開けるようにし、歯車ベルトと同じ搬送量パネルを出す

日付: 2026-11-25
状態: 採択（ADR 0086 の「電気ベルトは対象外」を置き換える）

## 文脈

電気ベルトコンベア（マスタの blockType `BeltConveyor`）は `openable: false` のため、開く操作の受け口（`BlockGameObject` の `BlockInteractable`）が付かず、詳細パネルが開かない。搬送量の集計（`BeltTransportClientCache.AttachExitStatistics`）と読み出し（`BeltThroughputReader` の `IBeltSpeedParam` 判定）は種類で絞っておらず、電気ベルトの segment も既に数えている。画面が開かないことだけが表示されない理由。

原因: 推測（根拠: マスタ ピン da69d773 の blocks.json で BeltConveyor 8種が openable: false・GearBeltConveyor 8種が true であること、BlockGameObject.cs の Openable 分岐、AttachExitStatistics が合流・内部以外の全 segment に観測者を付けるコード）

出所: ユーザー 2026-11-25「電気コンベア系でスループットのUIが出ない問題をなんとかして」

## 決定

- **電気ベルト 8 種すべて（ベルトコンベア・上り・下り・分岐器、高速の同 4 種）を `openable: true` にする。** 開くと歯車ベルトと同じ搬送量パネル（窓の切替・直近 9 秒の棒）が出る。
  出所: ユーザー裁定 2026-11-25 原文「電気コンベア系でスループットのUIが出ない問題をなんとかして」→ 選択「電気ベルト8種すべて」
- **開けるかどうかはマスタの `openable` フラグで決め、コードは変えない。**
  出所: agent前提（`.decisions/2026-09-09-blockUIAddressablesPathの改名を先にしポンプはopenableフラグで開ける.md` と、歯車ベルトが openable: true で開く既存の形）
- **合流・内部 segment は ADR 0086 と同じく数えない。** 電気ベルトでも同じ規則。
  出所: agent前提（ADR 0086 のユーザー裁定「通常segmentと分岐segmentに限定します」を電気ベルトにも同じ形で当てる）

## 却下した案

- 分岐器を除く 6 種だけ開ける（2026-11-25 にユーザーへ提示し、8 種すべてが選ばれた）

## 帰結

- 電気ベルトを開く操作で詳細パネルが開くようになる（今までは何も起きなかった）。
- 変更は moorestech_master の blocks.json と本 repo のピン更新だけ。セーブ・通信形式は不変。
