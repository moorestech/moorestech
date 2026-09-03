# 0035. 新規ワールドの既定マップモードを generated にする

- Status: Accepted
- Date: 2026-08-26

## Context

最新master `06a49c1bc` のmacOS Releaseビルドを出展モード（`MOORESTECH_EVENT_MODE=1`）で起動すると、
クライアント初期化が `InvalidOperationException: MapObject near-field instantiation skipped 43 instance(s)`
で失敗し、`GameShutdownEvent` を経てメインメニューへ戻される。ゲームが始まらないため、
オープニングスキットも最初のチュートリアルも到達不能だった。

原因は製品導線が使う既定マップモードにある。`StartServerSettings.MapMode` の既定は `template` で、
新規ワールドはオーサリングマップ `moorestech_master/server_v8/map/map.json` をコピーして作られる。
このマップが参照する mapObject guid 4種（`684eb2c1` / `d133b579` / `356cc324` / `39ba8217`、計100配置）は
mapObject マスタ195件に存在せず（moorestech_master HEAD `dcd2058` でも欠落）、
`MapObjectLayoutInstantiator` が prefab 解決に失敗、`MapObjectInstantiationRunner` が skip>0 で例外を投げる。

`generated`（自動生成マップ）は `--standaloneTerrainQa` とEditor専用の Generated world play ボタンからしか
到達できず、製品ビルドの通常導線からは選べない状態だった。

## Decision

`StartServerSettings.MapMode` の既定値を `template` から `generated` へ変える。
「未指定＝自動生成」を唯一の既定とし、製品のローカルプレイもstandaloneサーバーも同じ世界の作り方になる。

出所: ユーザー裁定 2026-08-26 原文「マップ自動生成でプレイしたい」→ 選択「CLI既定をgeneratedへ」

### seed

未指定時の `DefaultGeneratedSeed = 196` 固定を維持する。全プレイヤーが同じ世界で始まるため、
チュートリアル・スキットの見え方が再現しQAしやすい。

出所: ユーザー裁定 2026-08-26 選択「196固定のまま」

### 壊れたTemplateマップの扱い

`server_v8/map/map.json` の欠落guidは今回修正しない。製品導線が自動生成へ移れば起動不能は解消する。

出所: ユーザー裁定 2026-08-26 原文「触らない、イベント向けだから仮対応許容」

## Considered Options

- **CLI既定を generated にする（採択）** — 既定が1個になる。今回の事故は「製品導線だけ誰も通していなかった」
  ことで表面化したので、既定を分裂させない
- **製品導線（`CreateLocalServer` / `LocalGameLauncher`）だけで Generated を指定（却下）** —
  CLI既定がTemplateのまま残り、standaloneサーバーで作った世界と製品で作った世界が別物になる
- **Templateモードごと廃止（却下）** — `EditModeInPlayingTestUtil.LoadMainGame` がTemplateを
  高速な決定論的ワールドとして使っており、廃止すると全EditModeInPlayingTestが地形生成を踏む

## Consequences

- Templateを明示している既存呼び手（`EditModeInPlayingTestUtil.LoadMainGame`、
  `LoadMainGameWithMapMode` の4箇所）は変わらない
- 引数なしのstandaloneサーバー起動も自動生成になる。既存ワールドがあれば従来どおりそれをロードする
  （`WorldProvisioner.EnsureWorld` は新規作成時のみモードを見る）
- 新規ワールドの初回起動が地形生成の分だけ遅くなる（PR #1255 で176秒→30秒に短縮済み）
- `server_v8/map/map.json` のデータ不整合は残る。Templateモードで起動した場合は依然として起動不能

## Links

- [[.decisions/2026-08-26-新規ワールドは自動生成マップで開始する.md]]
- bd moorestech-vq12（最新masterのReleaseビルドとストーリー/チュートリアル確認）
- ADR 0030 mapobject-near-field-first-instantiation
