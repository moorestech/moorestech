# 手掘りの獲得は ItemEarned 通知として出し、集約は Web UI 側で行う

手掘り（MapObject 採掘・Vein 手掘り）でアイテムを取っても、プレイヤーに届くフィードバックはインベントリのスロットが黙って増えるだけで、何が何個入ったのかが分からない。獲得したアイテムのアイコンと個数（`[アイコン] +5`）を通知として出す。

出所: ユーザー裁定 2026-08-19（対象範囲／集約方式／データ形の3問）

## 決定

サーバーは獲得のたびに `NotificationCategory.ItemEarned` の通知を送り、Web UI が表示中の同一アイテム行へ加算して 1 行にまとめる。

- 発火点は `MiningProtocol.GetResponse`。MapObject と Vein の両経路が `MainOpenableInventory.InsertItem` の直前で合流しており、そこが唯一の獲得地点。`AchievementNotificationWiring` には配線しない（獲得はドメインイベントではなくプロトコルの結果）。
- 1 回の採掘で同じアイテムが複数スタックに割れても（`ItemStackFactory.CreateSplitStacks`）、ItemId で畳んで通知は 1 本にする。
- `NotificationMessagePack` に `[Key(4)] Count` と `CreateItemEarned` を新設し、`MessageId` は定数 `itemEarned.mined` に固定する。
- `NotificationService` の 3 秒クールダウンはカテゴリ単位のポリシー（`IsCooldownTarget`）にし、`ItemEarned` は対象外とする。1 ドロップ 1 通知を全てワイヤに乗せないと Web UI 側で加算できないため。
- Web UI は `category === "itemEarned"` かつ同一 `itemId` の生存行があれば `count` を加算し、行の `id` を刷新する。`id` が変わると React が再マウントするので入場アニメと生存尺（`onAnimationEnd` 駆動の除去）が最初から回り直す。

## Considered Options

- **サーバーで時間窓集約する**（却下）: サーバーに集約状態（バッファとフラッシュ時刻）を持つことになる。表示の都合による集約なので、状態は表示側に置く。
  出所: ユーザー裁定 2026-08-19
- **1 採掘ごとに 1 通知を積む**（却下）: 「同時表示数に上限は設けない」裁定（2026-08-17）と組み合わさると、連打で通知が画面を埋める。
  出所: ユーザー裁定 2026-08-19
- **既存 `Achievement` カテゴリへ相乗りし個数を `MessageParams` の文字列で送る**（却下）: スキーマ変更はゼロで済むが、「解放しました」系の達成通知と「+5」の獲得通知が同じカテゴリになり、Web UI 側で集約対象を判別できなくなる。
  出所: ユーザー裁定 2026-08-19
- **Vein 手掘りを対象外にする**（却下）: 同じ「手掘りで取る」体験が経路によって通知の有無で割れる。合流点 1 箇所で両方を賄える。
  出所: ユーザー裁定 2026-08-19

## Consequences

- 獲得通知は他カテゴリと同じ 7 秒の生存尺・左端縦中央・stage 背面（ADR 0016 / 0017）に乗る。層序とアニメは触らない。
- 加算のたびに行が再マウントされるため、入場のスライドインが再生される。数値だけを差し替える案は、除去が退場アニメ駆動である以上「生存尺のリセット」と両立しない。
- 配色は既存の既定色（`--text-high-contrast`）のまま。獲得専用の色トークンは足さない。
- `ItemEarned` はクールダウン免除なので、将来この経路に高頻度の発火点（自動採掘等）を足すとワイヤの通知量が発火回数に比例する。ブロックによる自動採掘は別経路（`VanillaGearMapObjectMinerProcessorComponent`）で今回の対象外。
