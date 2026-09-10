# ItemSelectModal は「Web化しない」の意であって未配線なら削除する

- **日付**: 2026-09-08
- **文脈**: uGUI退役 PR2 のレビューで、`ItemSelectModal.SelectItem()` / `ItemSelectModal.Instance` の呼び出し元が master でも PR 後でも repo 全体で0件と実測された（参照は `DebugObjects.prefab` のコンポーネント配置のみ）。この到達不能な経路を生かすために PR2 は `Client.DebugSystem/ItemSlot/` に新規ディレクトリ1つ・約210行・Addressables ロード経路1本を新設していた。2026-09-05 の裁定「デバッグUI（DebugSheet / ItemSelectModal / TrainUnitDebugOverlayPresenter）は現状維持」が *Web化しない* と *存置する* のどちらの意味かで結論が反転する。

## 決定

「現状維持」は **Web化しない** の意であり、未配線なら削除する。`ItemSelectModal.cs`・`Client.DebugSystem/ItemSlot/` 3ファイル・`DebugObjects.prefab` の `ItemSelectModal` 子オブジェクト・Addressable 登録 `Vanilla/UI/ItemSlotView` と `AddressableResources/UI/ItemSlotView.prefab` を削除する。

## 棄却した案

- **存置する（現状PRのまま）**: 裁定の文面どおり残し、ADR か plan に「未配線だが将来のデバッグ導線として残置」と明記して次の削除PRが迷わないようにする。

## 理由

呼び出し元0件の経路のために uGUI/TMP/EventSystems 依存を1系統維持し続けるのは、「移行済み画面uGUIの残骸を消し切る」という PR2 の目的と逆向き。DebugSheet 本体（`TrainUnitDebugOverlayPresenter` 含む）は配線が生きているので裁定の趣旨は損なわれない。

## リンク

- [[2026-09-05-デバッグUIはuGUI現状維持]]
- [[2026-09-05-uGUI撤去は抽出PRと削除PRの2本に分ける]]
