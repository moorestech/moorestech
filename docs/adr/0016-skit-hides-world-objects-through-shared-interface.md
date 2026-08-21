# スキットの世界非表示はEnvironment外オブジェクトの共通interfaceで束ねる

スキットの `inGameObjectControl` コマンドが消す対象のうち、**Environment 外に生成される世界オブジェクト**（mapObject・鉱脈の露頭）を共通 interface `ISkitWorldObjectControl` で束ね、コマンドは1フラグ `worldObjectEnable` で一括制御する。既存の `ISkitMapObjectControl` は削除して載せ替える。

## 背景: カテゴリ列挙が取りこぼしを生んだ

`InGameObjectControlCommand` は「1カテゴリ = 1 interface」で4本を名指ししていた（background / block / mapObject / entity）。地形と背景は `EnvironmentRoot` 配下なので背景フラグで一緒に消えるが、mapObject とエンティティは Environment 外に生成されるため個別に消す、という構造になっていた。

2026-08-04 に追加された露頭（`OutcropGameObjectDatastore`、シーンroot直下の独立オブジェクト配下）は**同じ「Environment 外の世界オブジェクト」でありながらどの interface にも繋がれず**、開幕スキット `100_start_game` の宇宙カット（`inGameObjectControl` で4フラグすべて false）で露頭だけが消え残った。これが「スキット中に vein が映る」不具合の実体である。

名指し列挙のままでは、Environment 外に置かれる表示物が増えるたびに同じ取りこぼしが再発する。共通概念で束ね、DI 登録だけで新しい対象が乗る形にする。

## 決定

- `ISkitWorldObjectControl { void SetActive(bool enable); }` を新設し、`MapObjectGameObjectDatastore` と `OutcropGameObjectDatastore` の両方が実装する
- `SkitWorldObjectControlGroup`（composite）が `IReadOnlyList<ISkitWorldObjectControl>` を受け、全要素へ `SetActive` を流す。DI での収集は `ITutorialWorldPin`（MapObjectPin / VeinPin を SkitManager がまとめて抑止する）と同型
- `commands.yaml` の `mapObjectEnable` を `worldObjectEnable` へリネームし、`100_start_game.json`（2コマンド）・i18n（japanese / english）・`commandListLabelFormat` を更新する
- entity は束に含めない。`ISkitEntityObjectControl` と `entityEnable` は独立のまま維持する

コマンドのフラグは次の4本になる:

| フラグ | 対象 |
|---|---|
| `backgroundEnable` | EnvironmentRoot（背景・地形） |
| `blockEnable` | ブロック |
| `worldObjectEnable` | mapObject ＋ 露頭（Environment 外の世界オブジェクト） |
| `entityEnable` | entity |

## 棄却した案

- **新 interface を1本足すだけ**（`ISkitOutcropObjectControl` を追加し、コマンドが `mapObjectEnable` を2サービスへプッシュ）— 露頭は消えるが列挙構造は残り、次の表示物でまた同じ取りこぼしが起きる
- **`MapObjectGameObjectDatastore` から露頭へ伝播**（自分の `SetActive` で露頭 datastore も消す）— 改修は1ファイルで済むが、mapObject datastore が露頭を知る責務の逆流が生じる
- **`mapObjectEnable` の名前を据え置き**— 実処理（mapObject＋露頭）と名前がズレたままになる。AGENTS.md「名前は実処理と一致させる」「変更の波及を恐れない」に反する
- **entity も含めて3種を1フラグに**— 「背景は消すがプレイヤーは映す」型の演出が作れなくなる

## 受け入れるコスト

- `commands.yaml` のリネームに伴い、既存スキット JSON と i18n の一括更新が必要（後方互換は取らない）
- デバッグシーンの `SkitTester` は mapObject / entity のダミーを生成して登録しているため、露頭 datastore のダミー登録を1件追加する

## スコープ外

`IsPlayingSkit` を見て Story ステートへ抜けるのは `GameScreenState` と `PlaceBlockState` だけで、ビルドメニューやインベントリを開いたままスキットが発火すると UI ステートが切り替わらない。別症状・別レイヤーのため本件には含めず、別 issue とする。

## 出所

- 「より抽象化し、mapobject と vein のオブジェクト両方を共通としてオンオフする新しい interface とクラスを作成し既存のオンオフから載せ替える」: ユーザー裁定 2026-08-18
- フラグ名を `worldObjectEnable` へリネーム: ユーザー裁定 2026-08-18
- entity を束に含めない: ユーザー裁定 2026-08-18
- composite ＋ `IReadOnlyList` 注入の形: agent前提（`ITutorialWorldPin` の先行パターン）をユーザーが選択
- 裁定レコード: `.decisions/2026-08-18-スキットの世界非表示は共通interfaceへ載せ替える.md` / `.decisions/2026-08-18-inGameObjectControlのフラグをworldObjectEnableへ改名する.md` / `.decisions/2026-08-18-スキット非表示の束にentityは含めない.md`
- 調査: bd `moorestech-kvl`
