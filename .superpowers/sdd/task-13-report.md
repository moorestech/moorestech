# Task 13 レポート: vein手掘り統合検証

## 結果

ADR-0007のvein手掘りをライブv8マスタで検証し、必須録画smokeまで完了した。
録画シナリオは次へ保存した。

`.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/vein-hand-mining-smoke.cs`

## 統合検証中に検出・修正した不具合

### ForUnitTestの鉱脈itemGuid不整合

- 症状: 指定regex 131件中、`GearMinerMiningTest`と`MinerMiningTest`が失敗した。
- 根因: IronVeinの`veinParam.itemGuid`が計画正本の`...0001`から`...0002`へずれ、採掘機マスタの対象itemと一致しなかった。`VanillaMinerProcessorComponent`の`_defaultMiningTicks`が0のままになっていた。
- 修正: fixtureと`MapVeinMasterTest`期待値を`...0001`へ戻した。
- 再現: 修正前は単独3件中2件FAIL、修正後3/3 PASS。

### 遠方Terrain未ロードで露頭初期化全体が失敗

- 症状: 初回の必須smokeで1772件中1689件の地表probeが外れ、`OutcropGameObjectDatastore`がready前に例外終了した。
- 根因: 統合元の旧datastoreに残っていた「未解決を全件収集してthrow」が、Task 9計画と既存裁定から逸脱していた。v8の鉱脈レイアウトは地形範囲より広いため、未ロード遠方座標ではRaycast非ヒットが正常に発生する。
- 正本: `.decisions/2026-08-04-露頭の地表未解決はAABB高さフォールバックで設置する.md`。既存commit `5c81dcabd`から裁定本文もcurrent branchへ移植した。
- 修正: 地表非ヒット時もスキップせず、`(MinY + MaxY + 1) * 0.5`のAABB中心高さで生成を継続する。件数はInfoログへ残す。
- TDD: `OutcropPositionResolverTest`を先に追加してAPI欠損2件のREDを確認し、地表解決/未解決の2経路をGREEN化した。
- 実機結果: ready約12秒、1772/1772露頭生成、Info fallback 1746件。

## テスト結果

| 検証 | 結果 |
|---|---|
| `uloop compile --project-path ./moorestech_client` | Error 0 |
| `SkitFailureCleanupTest`（default引数除去後） | 3/3 PASS |
| `OutcropPositionResolverTest|OutcropMiningTargetTest|OutcropGuidIndexTest` | 6/6 PASS |
| `Mining|MapVein|MapObject|CliConvert|GetMapData` | 133/133 PASS |
| `EditModeInPlayingTest` | 16/16 PASS |

EditModeInPlayingTestはworktree固有のNodeランタイム欠損をメインworktreeからgit管理外APFS cloneで補った。
補完前は`PlayerStartsOnBuiltTerrainTest`のみNode binary missingで失敗、単独1/1 PASSを確認後、Test Frameworkの既知`NewScene during play mode`フレークを1回観測した。Unityを停止・再起動したfresh runで16/16 PASS。

## 必須録画smoke

ライブmaster:

- main pin: `094d242be9509565393efc5aad5b467bda247222`
- external worktree: `/Users/sakastudio/hermes-agent/data/repos/moorestech-master-worktrees/vein-hand-mining/server_v8`
- preflight: 5/5 PASS

最終成果物:

- result: `moorestech_client/PlaytestResults/20260805_005249/vein-hand-mining-smoke/result.json`
- recording: `recording.mp4`、4,057,084 bytes、8.93秒
- screenshots: `01-stone-outcrop-front-focus.png` / `02-stone-outcrop-angle-focus.png` / `03-stone-mined.png`

最終resultは`Success=true`、28 Assert/UntilすべてPASS、`ErrorLogs=[]`。

検証した実経路:

1. ライブv8の鉱脈マスタ11種をロード。
2. 11種すべての実Addressableを`GameObject`として解決。
3. MainGameシーンの`OutcropGameObjectDatastore`起動と固定レイアウト1772件の全生成を確認。
4. 石の斧をホットバー1へ付与・選択し、装備枠1へ移してサーバー選択装備を同期。
5. Stone露頭へ正面・45度からワープし、薄いColliderで両方向のフォーカス成立を確認。
6. InputSystemの左クリックを1.2秒保持し、本番採掘FSMから`va:mining`を送信。
7. サーバー応答後に石インベントリが増加。

スクリーンショットは3枚とも実プレイ視点で、アバター・地形・HUD・石の斧・石露頭が描画されている。採掘後の3枚目ではhotbarに石x1とPASSオーバーレイを目視確認した。

Consoleには既存ライブデータ由来のBush BrokenPrefabと欠損mapObjectログが残るが、最終シナリオ区間の`result.json`はErrorLogs 0で、vein手掘り起因のError/Exceptionは0件。

## スコープ注記

mooreseditor側のスキーマ追随は本planのスコープ外。新フィールドを旧プラグインキャッシュのまま編集すると白い空箱ノード化する既知の罠があるため、追随後はアプリ再起動が必要。
