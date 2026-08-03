# Task 10 レポート: connectTool表示名のWeb解決統一＋placement表示名集約（D1案A・C17・C10・D10案A）

コミット: `51a5b41ff feat: connectTool表示名をWeb側Guid辞書解決へ統一しBuildMenu再pushと表示名分岐複製を撤去`

注記: 本ファイルはplan 1（craft-tab系）のtask-10レポートを上書きしている（旧内容はgit履歴 `d97cd4d9d:.superpowers/sdd/task-10-report.md` から復元可能）。

## 何を実装したか

1. **ホスト側のconnectTool表示名解決を撤去（D1=案A）**
   - `WebBuildMenuEntryCatalog`: `Localize.GetContent(ContentLocalizationKeys.ConnectToolName(...))` を削除しGuidのみ渡す（`using Client.Localization` / `Mooresmaster.Localization.Generated` も除去）
   - `WebBuildMenuEntry.CreateConnectTool(Guid, IReadOnlyList<RequiredItem>)`: label引数を削除しblockと同形（Label=null）へ
   - `BuildMenuTopic`: `_languageSubscription` フィールド・`Localize.OnLanguageChanged` 購読・disposeを削除（言語切替の再pushを撤去）
   - `PlacementModeDtoFactory`: `ConnectToolPlacementTarget` を `SelectedTargetType="connectTool"` + 新フィールド `SelectedConnectToolGuid`（D形式）へ。`MasterHolder.ConnectToolMaster.GetElementOrNull(...).Name` 参照が消滅（C3の表示面も根絶）。`"raw"` はユーザー命名blueprintとtrainCarだけへ縮退
2. **表示名解決の集約（D10=案A）**
   - `buildMenuGrouping.ts` に `SelectableTarget` 型と `localizeSelectableTargetName(target, translate)` を新設（**新規ファイルは作らず**同ファイル内）
   - `localizeBuildMenuEntries` は entryType→target 写像（`entryTarget`）を経て集約関数へ委譲。三項連鎖を排除
   - `PlacementModeHud.tsx`: 三項連鎖を廃し `localizeSelectableTargetName(selectedTarget(data), t)` の1呼び出しへ。`selectedTarget` はwire identity→target のswitch写像のみ（表示名解決の分岐複製は無し）
3. **契約（zod）追従**
   - `buildMenu.ts`: connectToolをblock同形の `BuildMenuDictionaryResolvedEntryDataSchema`（`entryType: z.enum(["block","connectTool"])` / `label: z.never().optional()`）へ移動。残りは `BuildMenuTrainCarEntryDataSchema`（label必須）
   - `ui.ts`: `PlacementModeDataSchema` に `connectTool` variant（`selectedConnectToolGuid: z.string().uuid()`）を追加。`raw` にblueprint/trainCar限定のコメントを付与
4. **fixture / 契約テスト追従**
   - 共有wire fixture `build_menu_snapshot.json` のconnectTool entryから `label` を除去（C# `WireContractTest` のDTOも同時更新）
   - 新規共有fixture `placement_mode_connect_tool.json`（C# `WireContractC2Test` とwebui `wireContract.test.ts` の双方が参照）
   - mock-host: buildMenu fixtureへconnectTool entry（label無し・Guidのみ）、`contentLocalizationFixtures` へ `connectToolNameKey(WIRE_CONNECT_TOOL_GUID) = "電線接続ツール"`、topicControlsへ `placementConnectTool` シナリオ
   - `BuildMenuProductionContractTest`: connectToolのlabel期待をentryKey(Guid)+label無しへ
   - `validators.test.ts`: connectToolの「Guidのみ受理」「非Guid拒否」「ホスト解決label拒否」へ差し替え（trainCarは従来通りlabel付き）
5. **ADR 0006追記**: 決定5に「D1=案AでconnectToolをWeb解決へ昇格」を追記し、帰結節の「trainCar/connectToolはLabel維持」をtrainCarのみへ修正

## TDDの証拠

### RED（Step 1→2）

`npx vitest run src/features/buildMenu`

```
 ❯ src/features/buildMenu/buildMenuGrouping.test.ts (18 tests | 5 failed) 5ms
   × localizeBuildMenuEntries > connectToolはraw labelなしでGuid導出キーから表示名を解決する
     → expected undefined to be '電線ツール' // Object.is equality
   × localizeSelectableTargetName > blockはblockNameKeyで解決する
     → localizeSelectableTargetName is not a function
   × localizeSelectableTargetName > connectToolはconnectToolNameKeyで解決する
     → localizeSelectableTargetName is not a function
   × localizeSelectableTargetName > blueprintCopyはtyped UI keyで解決する
     → localizeSelectableTargetName is not a function
   × localizeSelectableTargetName > rawはユーザー命名文字列をそのまま返す
     → localizeSelectableTargetName is not a function
 Test Files  1 failed (1)
      Tests  5 failed | 13 passed (18)
```

想定通りの理由: 集約関数が未実装のため `is not a function`。connectTool entryは旧実装が `entry.label`（未配信）を読むため `undefined`。

HUD側 `npx vitest run src/features/modeHud`

```
 ❯ src/features/modeHud/PlacementModeHud.localization.test.ts (4 tests | 1 failed)
   × PlacementModeHud localization > connectTool GuidをGuid導出キーの表示名へ解決する
     → expected 'Selected: undefined' to be 'Selected: Wire Tool'
```

想定通りの理由: 旧三項連鎖はconnectToolを `data.selectedName`（未配信）へ落とすため `undefined`。

### GREEN

- `npx vitest run src/features` → `Test Files 30 passed (30) / Tests 185 passed (185)`
- `npm test`（全体） → `Test Files 80 passed (80) / Tests 529 passed (529)`
- `npx tsc -b` → exit 0 / `npm run lint` → 0 problems / `npx tsc -p e2e/tsconfig.json --noEmit` → exit 0
- `uloop compile --project-path ./moorestech_client` → `Success: true / ErrorCount: 0`
- `uloop run-tests --filter-type regex --filter-value ".*(BuildMenu|PlacementMode|WireContract).*"` → `TestCount 48 / PassedCount 48 / FailedCount 0`
- `npx playwright test --config e2e/playwright.config.ts --grep "buildMenu|i18n"` → `10 passed`
- `npx playwright test --config e2e/playwright.config.ts e2e/tests/modeHud` → `2 passed / 2 failed`（新規「配置対象connectToolをGuidだけの配信から辞書表示名へ解決する」はpass。失敗2件はベースライン既存）

### ベースライン比較（既存赤の切り分け）

フルe2e（`npx playwright test`）は変更後 `11 failed / 118 passed`。自分の変更を `git stash push -- <パス明示>` した状態で同じフルe2eを実行しても **同一の11件が失敗・118 passed**。内訳はmodeHud×2 / recipe×3 / connection / research×2 / skit / commonHud / train で、いずれも英語文言期待（mock既定localeがjapanese）や視覚パリティ系のベースライン赤。本タスクによる回帰は0件。

この既存赤のため、当初 `operation-mode-hud.spec.ts` の既存テスト内に足したconnectTool検証は「line 20の英語期待で先に落ちて到達しない」死んだアサーションになっていた。ロケール非依存の独立テストへ移し、実際に実行されてpassすることを確認済み。

## 変更したファイル

ホスト（C#）
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/WebBuildMenuEntryCatalog.cs`
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/WebBuildMenuEntry.cs`
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuTopic.cs`
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/PlacementModeTopic.cs`

C#テスト・共有fixture
- `Client.Tests/WebUi/BuildMenuProductionContractTest.cs`
- `Client.Tests/WebUi/WireContractC2Test.cs`
- `Client.Tests/WebUi/WireContractTest.cs`
- `Client.Tests/WebUi/WireFixtures/build_menu_snapshot.json`
- `Client.Tests/WebUi/WireFixtures/placement_mode_connect_tool.json`（+ Unity生成 `.meta`）

Web
- `src/bridge/contract/schemas/buildMenu.ts` / `schemas/ui.ts`
- `src/bridge/contract/validators.test.ts` / `wireContract.test.ts`
- `src/features/buildMenu/buildMenuGrouping.ts` / `buildMenuGrouping.test.ts` / `index.ts`
- `src/features/modeHud/PlacementModeHud.tsx` / `PlacementModeHud.localization.test.ts`
- `e2e/mock-host/fixtures.ts` / `fixtures/contentLocalizationFixtures.ts` / `topics/topicControls.ts`
- `e2e/tests/regression/buildMenu.spec.ts` / `e2e/tests/modeHud/operation-mode-hud.spec.ts`

ドキュメント
- `docs/adr/0006-mod-localization-guid-derived-keys-web-side-resolution.md`

## 自己レビューの所見

- **ホスト側にconnectTool表示名解決が残っていない**: `grep -rn "ConnectToolName" moorestech_client/Assets/Scripts` の非テストヒットは `MasterSourceTextCollector`（原文＝フォールバック元の収集。決定5・6どおり残すのが正）のみ。`Client.WebUiHost` 配下の `Localize.` 参照はLocalizationTopic / 辞書エンドポイント / BP名モーダルだけで表示名解決は0。`ConnectToolMaster` 参照も解放判定の `All` 列挙のみ
- **HUDの三項連鎖の消滅**: `PlacementModeHud.tsx` は集約関数の1呼び出し。残る `selectedTarget` はwireフィールド名（selectedBlockGuid / selectedConnectToolGuid / selectedName）→ target型の写像であり、キー導出・辞書引きの複製は無い
- **言語切替の追従経路**: BuildMenuTopicの再pushは削除。`LocalizationTopic`（locale+revision）→ Web辞書再取得 → 再描画という既存経路にBuildMenu/配置HUDが乗る形（block/item/researchと同一）。`i18n.spec` がこの経路の生存を確認
- **前例一致**: BlockGuid移行・Task 9のfluid移行と同じ「zod variant追加 → 表示側の辞書解決 → 共有fixture更新 → C#契約テスト更新」の手順を踏襲
- **ファイル規模**: 全ファイル200行以下（最大 `WebBuildMenuEntry.cs` 119行、`buildMenuGrouping.ts` 92行）。新規ファイルは共有fixture JSON 1件のみ
- **契約の型強度**: connectToolは `label: z.never().optional()` によりホストがlabelを再送し始めたらpayloadごと弾かれる（回帰の再侵入を型で防止）。負テストも追加

## 問題や懸念事項

1. **フルe2eのベースライン赤11件**（modeHud×2 / recipe×3 / research×2 / connection / skit / commonHud / train）は本タスク前から存在し、stash比較で同一集合であることを確認済み。多くは「mock既定localeがjapaneseなのにspecが英語文言を期待」で、単体・全体どちらでも落ちる既存負債（plan外Warning群と同様に別対応が妥当）
2. **mock-hostのbuildMenu fixtureにconnectTool entryを追加した**ため、`e2e/capture-buildmenu.ts` 等の手動パリティ撮影で輸送カテゴリのスロットが1つ増える（既定表示は物流カテゴリなので既定キャプチャには出ない）。基準を再撮影する場合はこの差分を意図として扱うこと
3. trainCarは正準source未定のためLabel維持のまま（ADR決定5の追記で範囲を明記）。将来 `trainCar.<guid>.name` を定めれば `raw` はユーザー命名blueprintのみへ縮退できる
