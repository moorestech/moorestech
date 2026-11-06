# 写さない Web テストの処遇

段階ごとの削除 PR で、消えた Web のテスト名（Playwright・vitest）と増えた NUnit のテスト名をスクリプトで突き合わせる（計画 `docs/uitk-migration/plan.md` §4.1）。写さないテストはここに 1 件 1 行で理由を書く。目視で済ませるものは証跡のパス、捨てるものは理由。見た目の意図は `docs/uitk/TODO.md` の参照キャプチャで辿る。

| 束 | Web のテスト（ファイル・テスト名） | 処遇（目視・捨てる） | 証跡のパスか理由 | PR |
|---|---|---|---|---|
| 3 | `src/shared/placementTarget/selectableTarget.test.ts` | 辞書解決kindはidをguidとして写す | 捨てる | 配信の kind 文字列を表示種別へ写す処理は Web の配信契約だけのもの。C# は `IPlacementTarget` の型そのものが種別で、Kind→型の対応は既存 `Client.Tests/PlaceSystem/PlacementTargetFactoryTest` の「TryCreateはKindごとに対応する型とIdを持つターゲットを生成する」が持つ | #1595 |
| 3 | `src/shared/placementTarget/selectableTarget.test.ts` | blueprintCopyはidを持たない共通種別になる | 捨てる | 同上。C# の `BlueprintCopyPlacementTarget` は表示名を辞書キー 1 つで解き（`PlacementTargetDisplayNameTest`）、id の有無は表示名に効かない | #1595 |
| 3 | `src/shared/placementTarget/selectableTarget.test.ts` | 保存BPだけが原文labelのrawになる | 捨てる | 同上。原文の名前を返すのは `BlueprintPlacementTarget.DisplayName` だけで、`PlacementTargetDisplayNameTest`「保存したブループリントは利用者が付けた名前をそのまま返す」が固定する | #1595 |
| 3 | `src/shared/placementTarget/selectableTarget.test.ts` | ホットバー枠のDTO形状もそのまま受け付ける | 捨てる | UITK のホットバーは配信 DTO を経ず `IPlacementTarget` を直接持つので、DTO の形の互換が無い | #1595 |
