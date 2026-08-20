# 有料アセット依存テストはIgnoreCIでCIから外す

2026-08-19裁定 / PR #1179 (feat/mapmaking-species-parity)

決定: `MapObjectAddressableLoadTest` と `MapObjectRayTargetTest` にクラス単位で `[Category("IgnoreCI")]` を付け、CIの `-testCategory "!IgnoreCI"` で除外する。検証はローカルEditorで行う。リポジトリ初のIgnoreCI使用例。

理由: 116種のラッパープレハブは `Assets/PersonalAssets/moorestech-client-private/BK/...` のプレハブのバリアントで、`PersonalAssets/` は `.gitignore:79` で除外され `run_test.yml` は `moorestech_master` しかcheckoutしない。CIには親プレハブが存在せず `AssetDatabase.LoadAssetAtPath` が必ずnullを返す。パスやAddressable登録の誤りではない（アドレス→アセットパス照合は通過している）。

棄却案: CIで `moorestech_client_private` をcheckoutする案は、`.moorestech-external-revisions.json` にpinが既にあり `moorestech_master` と同形で追加できるものの、有料アセットrepoをCIへ取得する容量・時間・ライセンスの負担を避けて棄却。PersonalAssets非在時に `Assert.Ignore` する案は、CIで常にスキップされる実態がIgnoreCIと同じで検出ロジックの分だけ複雑になるため棄却。

上書きした先行方針: `docs/superpowers/plans/2026-08-04-pr1104-unity-test-ci-repair.md:16` の「検証範囲を狭める `IgnoreCI` は追加しない」。今回は「CIに物理的に存在しない有料アセットへの依存」であり、CIで実行可能なものを意図的に狭める行為とは性質が異なるとして上書きする。
