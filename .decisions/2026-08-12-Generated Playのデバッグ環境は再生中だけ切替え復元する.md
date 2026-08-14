# Generated Playのデバッグ環境は再生中だけ切替え復元する

決定: Generated Playボタン経由の再生中だけ `DebugEnvironmentTypeKey` を `Runtime` にし、旧値を退避して `EnteredEditMode` で復元する。通常再生・NoSave Playへ残留させない。

棄却案:
- プレイテストDSL(`PlaytestBootLifecycle.ConfigureFixedWorldDebugSettings`)と同型に `Runtime` を永続保存する — 前例一致だが、ボタン使用後の通常再生までRuntime環境になる残留副作用がある
- 実装せずDebug Sheetでの手動切替え運用とし制約をADRへ明記する — 保存値次第でプレイヤーが地中に埋没したままになりR1(押下でプレイ可能)が崩れる

理由: planのR4が「再生終了時に復元し通常の再生ボタン・NoSave Playボタンに影響を残さない」を要求しており、永続保存はその趣旨に反する。実機検証で `Other` 残留時にオーサリング地形がプレイヤーの約63.5m上に重畳することを実測（足元 Terrain_0_0 at Y=15.73 / 上空 Terrain_-1_0 at Y=79.29）。

リンク: [[2026-08-12-generatedワールドプレイはエディタ専用ボタンで提供する]] / docs/adr/0009-generated-world-editor-play-button.md
