# 0037. generatedワールドの内容ベースIDとスナップショット復元

- Status: Accepted
- Date: 2026-08-26

## Context

新規generatedワールドの作成は `WorldProvisioner.EnsureWorld` が同期で pass-1（生成 ≈17s）と
pass-2 先焼き（≈40s）を回してから ready になる（Editor実測 57.6s、ビルドでは97s中63.8s）。
seedは196固定で生成マスタも同一なのに、worldId が `SHA256(seed:createdAt)` で毎回変わるため
共有キャッシュ `cache/worlds/<worldId>/` は一度もヒットせず、1起動1.3GBのままリークする（実測55GB/283個）。
出展モードは起動毎にワールドをワイプするので、毎回フル生成になる。

## Decision

### A. worldId を内容から導く
`worldId = SHA256(seed:generationMasterFingerprint:generatorVersion)[0..16]`（generated のみ。template は従来どおり seed:createdAt）。
同じマスタ・同じseed・同じ生成器なら同じIDになり、共有キャッシュがワールドを作り直しても命中する。

### B. 共有キャッシュをワールドスナップショットとして扱い、新規作成はコピーで復元する
`cache/worlds/<worldId>/` に `world.json` / `map.json` / `terrain/` / `visual/` をワールドと同じレイアウトで置く。
`EnsureWorld` は生成前に worldId を算出し、スナップショット源を順に探す:
1. ビルド同梱 `<serverDataDirectory>/worldSnapshots/<worldId>/`（visual込み）
2. 共有キャッシュ `cache/worlds/<worldId>/`

命中したら `world.json` / `map.json` / `terrain/` をワールドrootへコピーして終わり（生成も先焼きもしない）。
同梱源から復元した場合は同時に共有キャッシュへも全量コピーする（クライアントの焼きはキャッシュを読むため）。
ミスしたら従来どおり生成＋先焼きし、直後に `world.json` / `map.json` / `terrain/` を共有キャッシュへ書き戻す。

出所: ユーザー裁定 2026-08-26 原文「このまま両方サクッと実装して」（A・B両方）／
「同梱スナップショットにvisual 1.2GBまで含めますか」→ 選択「visualも同梱（+1.2GB）」

### C. 旧キャッシュの起動時GC
`EnsureWorld` は `cache/worlds/` 配下で現在の worldId と異なるディレクトリを全て削除する。

出所: ユーザー裁定 2026-08-26 「起動時に自動削除」

### 同梱の作り方
`GameDataBundler` の直後に `WorldSnapshotBundler` が、共有キャッシュに現在IDのスナップショットが無ければ
一時ワールドで `EnsureWorld` を回して作り、`cache/worlds/<id>/` を `<output>/game/worldSnapshots/<id>/` へコピーする。
リポジトリには何も置かない（1.2GBをgit管理しない）。

出所: agent前提（`GameDataBundler` が `game/` を出力へコピーする前例に揃える。開発時は共有キャッシュが同じ役を担うので同梱源は不要）

### createdAt
復元時の `world.json` の `createdAt` は復元時刻で書き直す（実世界日時の記録用途であり、IDの導出には使わなくなる）。

出所: agent前提（AGENTS.md「DateTimeは実世界の日時を記録する用途」）

## Considered Options
- **world本体のみ同梱（+80MB）（却下）** — 生成17sだけ消え先焼き40sが初回に残る。ユーザーが「visualも同梱」を選択
- **キャッシュを消さない（却下）** — 55GBのリークが続く。ユーザーが「起動時に自動削除」を選択
- **シーンへ焼き込む（却下・前対話）** — TerrainData資産が同規模になりサーバー側の生成が消えない

## Consequences
- 初回起動 ≈90s → ≈30s＋コピー。2回目以降・出展モードのワイプ後も同じ
- `GenerationMasterDriftResolver` はマスタ指紋が動くと worldId 自体が変わるため、旧IDのキャッシュはGCで消え新IDで作り直す
- `WorldGeneratorVersion` はIDに含まれるため、版が上がると自動的に別キャッシュになる

## Links
- ADR 0012 / 0025 / 0035、PR #1255、`.decisions/2026-08-26-generatedワールドはスナップショット復元で起動する.md`
