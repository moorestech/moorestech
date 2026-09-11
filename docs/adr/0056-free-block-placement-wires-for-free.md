# 0056. 無料設置デバッグ時も電線自動接続を素材消費なしで実行する

## Status

Accepted (2026-09-11)

## Context

デバッグ設定 `FreeBlockPlacement`（ラベル「Free block placement (no item cost)」）がONのとき、
`PlaceBlockProtocol` の無料設置分岐は `TryAddBlock` の直後に return し、電線の自動接続
（`ElectricWireAutoConnectService.EvaluateAutoConnect` / `ExecuteAutoConnect`）が一度も呼ばれない。
一方クライアントの `ElectricWireAutoConnectPreview` はこの設定を見ず、青線と「電線 xN」のコスト行を出す。
結果、プレビューでは接続先へ線が出るのに設置後は1本も繋がらない（2026-09-11 録画で確認）。

接続先の選定（`ElectricWireAutoConnectTargetCollector` / `ConnectToolSelector`）はサーバーとプレビューで
同じ実装を共有しており、ずれているのは無料設置の分岐だけ。

## Decision

1. **サーバーは無料設置でも配線する。** 無料分岐でも自動接続の計画・実行を行い、電線素材は消費しない。
   `FreeBlockPlacement` は「アイテムコストの免除」であって「配線の省略」ではない。
   出所: ユーザー裁定 2026-09-11 「サーバーが無料で配線する」（選択肢採択）
2. **connectTool の解放状態も無視する。** 無料設置がブロック解放を無視するのと同じ扱いで、
   electricWire の connectTool が1つも解放されていない世界でも SortPriority 最小のツールで配線する。
   解放を無視した一覧はサーバー／プレビューが同じ実装を共有する（手写し禁止。既存の
   `ConnectToolSelector.UnlockedByToolType` 共有と同じ理由）。
   出所: ユーザー裁定 2026-09-11 「解放無視で最優先ツールを使う」（選択肢採択）
3. **プレビューの表示は変えない。** 青線もコスト行も現状のまま出す。変えるのは所持数の突き合わせだけで、
   無料設置ONなら「賄えた」とみなして赤線・設置不可にしない（サーバーが素材消費なしで通す分岐と対）。
   出所: ユーザー裁定 2026-09-11 原文「デバッグのために変える必要のない処理を変えない」
   → 確認質問で「不足判定だけ無料時は素通し」を採択

## Considered Options

- **プレビューが配線を隠す**: サーバーは現状のまま配線を飛ばし、プレビュー側で無料ONなら線もコスト行も出さない。
  → 却下（ユーザー裁定 2026-09-11）。無料設置＝素のブロックだけ置く、という読みは採らない。
- **解放済みツールが無ければ配線しない**: 無料設置でも現行の「未解放なら配線せず設置のみ」分岐を維持。
  → 却下（ユーザー裁定 2026-09-11）。ブロック解放を無視するのに配線だけ解放に縛られるのは片手落ち。
- **青線は出しコスト行は消す**: 素材が減らないのでコスト行と不足判定を出さない。
  → 却下（ユーザー裁定 2026-09-11 原文「デバッグのために変える必要のない処理を変えない」）。表示は現状維持。
- **プレビューを一切触らない（不足時は設置不可のまま）**: 所持電線が足りないと無料設置でもクリックが通らない。
  → 却下（ユーザー裁定 2026-09-11）。不足判定だけは素通しする。

## Consequences

- サーバー `PlaceBlockProtocol` の無料分岐は「解放・コストを見ず設置し、配線は素材消費なしで実行」に変わる。
  計画時の所持数判定と実行時の素材消費を無料フラグで抑止する経路が `ElectricWireAutoConnectService` に要る。
- `ConnectToolSelector` に解放を無視した electricWire ツール一覧を返す経路が要り、
  サーバー（`EvaluateAutoConnect`）とクライアント（`ElectricWireAutoConnectToolSelector`）の両方がそれを使う。
- 所持数判定は仮想在庫（`ElectricWireAutoConnectVirtualInventory.CanAfford`）とサーバー `HasEnoughAll` の
  両方で無料時に真とみなす。表示（`AutoConnectNoticeLines`・`AutoConnectWirePreviewRenderer`）は無変更。
- 通常設置（無料OFF）の挙動は一切変えない。
- agent前提: 無料フラグは各経路の入口（`PlaceBlockProtocol` / `ElectricWireAutoConnectPreview`）で一度読み、
  下位へは bool で渡す（`DebugParameters` はファイルIOを伴うため、既存の「一度だけ読む」方針に従う）。

## Links

- [[2026-09-11-無料設置ON時はサーバーが素材消費なしで電線を自動接続する]]
- [[2026-09-11-無料設置の電線プレビューは表示現状維持で不足判定だけ素通しする]]
- ADR 0008 electric-wire-tool-unified-extend-and-explicit-wiring
- bd: moorestech-482d
