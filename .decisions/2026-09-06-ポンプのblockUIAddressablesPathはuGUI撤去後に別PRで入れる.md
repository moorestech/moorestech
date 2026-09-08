# ポンプのblockUIAddressablesPathはuGUI撤去後に別PRで入れる

- 日付: 2026-09-06 / 出所: AskUserQuestion（PR #1323・ADR 0051 Task 6）
- 決定: 本PRではmoorestech_masterを変更せず、uGUI撤去PRのマージ後に plan値 `Vanilla/UI/Block/MachineBlockInventory` を入れる別PRを出す。ポンプが開けるのはそれまで待つ
- 棄却案: PR #1319 と同じ回避値 `ElectricPoleNetworkInfoUI` を両ポンプへ今入れる（無関係な電柱UIプレハブを一瞬ロードする暫定状態になる）
- 理由: SubInventoryState のuGUIプレハブロードが残る現状でplan値を入れるとMachineBlockInventoryViewがIMachineParam前提でNREになり open 通知が届かない（plan Step 1 の停止条件）
- リンク: docs/adr/0051-pump-ui-and-vein-footprint-parity-with-miner.md / docs/superpowers/plans/2026-09-05-pump-ui-and-vein-footprint-parity.md Task 6
