# Task 8 報告
- AnchorIds（静的19・prefix6・生成関数6、空白はLogError+そのまま返す）を Client.Game に追加。TutorialAnchorIdMapper を削除し3箇所を付け替え（RecipeItemPrefix 定数も AnchorIds に持たせた）。
- Client.Ui に TutorialAnchor / TutorialAnchorRegistry（照会型・static状態なし）/ TutorialAnchorResolution、asmdef に Client.Game を追加。
- テスト: AnchorIdsWebParityTest（L0・3件）、TutorialAnchorRegistryTest（L1・5件）。compile ErrorCount 0。対象正規表現で 10/10 Passed。
- 注意: BuildMenuEntry は D-11 どおり string kind。_CompileRequester.cs は未ステージ。
