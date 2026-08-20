# スキットの世界非表示は共通interfaceへ載せ替える

決定: `ISkitWorldObjectControl`（Environment外の世界オブジェクト）を新設し、mapObject datastore と 露頭 datastore の両方に実装させ、composite（`IReadOnlyList` 注入・`ITutorialWorldPin` と同型）で束ねる。既存 `ISkitMapObjectControl` は削除して載せ替える。

棄却案:
- `ISkitOutcropObjectControl` を1本足すだけ（露頭は消えるがカテゴリ名指し列挙が残り、次の表示物でまた取りこぼす）
- MapObjectGameObjectDatastore から露頭へ伝播（責務の逆流）

理由: 今回の不具合は「Environment外に置かれる表示物をコマンドが名指し列挙している」構造そのものが原因。共通概念で束ねればDI登録だけで新対象が乗る。

リンク: docs/adr/0016-skit-hides-world-objects-through-shared-interface.md / bd moorestech-kvl
