# 近傍レンジは活性ゲートでなくincludeInactiveで守る

- 日付: 2026-08-23
- 文脈: `feature/mapobject-near-field-instantiation` / D1（[[2026-08-23-最終レビューの設計判断7件の裁定.md]]）の適用不能が判明したことによる裁定の補正
- bd: moorestech-4z88

## 決定

D1の案A（生成を世界オブジェクトの活性状態に従属させる）は**後着レンジのみ**に適用する。近傍レンジは活性ゲートを掛けず、代わりに案B（`MapObjectGameObject.cs:95` の `GetComponentsInChildren<MapObjectRayTarget>()` と `:158` の `GetComponentsInChildren<Transform>()` を `includeInactive:true` へ揃える）を保険として併用し、非活性下で生成されても初期化と破壊処理が成立するようにする。

## 棄却案

- 近傍レンジにも活性ゲートを掛ける（＝D1案Aの素直な適用）
- plan Task 6 の `MapObjectRayTarget` lazy解決を先行させる
- スキット側で `worldObjectEnable:false` を近傍生成完了まで遅らせる／開幕スキットの発火を `WaitAllAsync` 後へ移す
- 近傍個体の破損を許容し別タスクへ送る

## 理由

**近傍レンジに活性ゲートを掛けるとローディングが永久に明けない**（検証済み）。開幕スキット `100_start_game.json` は commands[10] で `worldObjectEnable:false`、復帰は commands[32]。その間の `text` コマンドは `TextCommand` がクリック待ちの無限ポーリングで進行する。さらに `SkitFireManager` は `IPostInitializable` で `starter.StartGame()` 内＝`WaitAllAsync` より前に発火するため、近傍生成とスキットの非活性窓は必ず重なる。無人実行（録画プレイテスト・プレイテストDSL）では永久ハングになる。

一方でゲートを外すと、その窓で生成された近傍個体は `GetComponentsInChildren`（既定 `includeInactive:false`）が空を返して rayTarget 未初期化のままになり、**セッション中ずっと採掘もフォーカスもできない**。近傍個体はプレイヤーの足元にある個体なので影響が大きい。

よって近傍は「非活性でも初期化が成立する」形（案B）で守る。代償は「非活性でも初期化が成立する」という不文律をprefabの将来の子に課すこと。

Task 6 の lazy 化は rayTarget 側しか救わず、`:158` の破壊時の子走査（破壊済みなのにコライダーと見た目が残る）が残るため単独では不足。スキット側での回避は発火順序に他機能も依存しており波及が読みにくい。

## リンク

- [[.decisions/2026-08-23-最終レビューの設計判断7件の裁定.md]]
- [[docs/adr/0030-mapobject-near-field-first-instantiation.md]]
