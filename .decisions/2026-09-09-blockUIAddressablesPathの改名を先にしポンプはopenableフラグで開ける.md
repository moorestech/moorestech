# blockUIAddressablesPathの改名を先にし、ポンプはopenableフラグで開ける

- **決定**: `blockUIAddressablesPath` → `openable: bool` の改名（`moorestech-lnsf.3`）を先に行い、ADR 0051 Task 6（`moorestech-n49s.2`）は油井・歯車ポンプに `openable: true` を立てるだけで済ませる。
- **棄却案**: ADR 0051 の文面どおり先に plan値 `Vanilla/UI/Block/MachineBlockInventory` を文字列で入れてポンプを開けるようにし、改名は後続PRにする。
- **理由**: uGUI撤去（PR #1329 / #1332）で `SubInventoryState` の Addressable プレハブロードが消え、PR #1323 が Task 6 を見送った停止条件（`MachineBlockInventoryView.Initialize` が `IMachineParam` 前提でポンプに NRE、および PR #1319 の回避値との方式選択）が両方とも消滅した。改名を先にすれば master repo を二度触らずに済み、回避値の選択自体が不要になる。ADR 0051 も「フィールドの改名・廃止はuGUI完全撤去側が持つ」と明記している。
- **付随裁定**: 重複bead `moorestech-n49s.1` は close し `moorestech-n49s.2` を正とする（PR #1323 本文が .2 を名指ししているため）。
- **リンク**: ADR 0051 / ADR 0052 / PR #1323 / PR #1329 / PR #1332 / `moorestech-lnsf.3` / `moorestech-n49s.2`
