# PR #1262 レンダー最適化 引き継ぎ

作成: 2026-08-24 16:52 JST  
引継元セッション: Claude Code `b1cb3808-0bcd-4a0e-9dc2-2a02c8a65f25` → Codex  
Beads: `moorestech-ara.1`（未完了作業の正本）

## 最初に行うこと

```bash
cd /Users/katsumi/moorestech
pwd
bd prime
bd show moorestech-ara.1
bd update moorestech-ara.1 --claim
git status --branch --short
git pull --ff-only
```

本体ブランチは `perf/urp-ssao-depth-shadow-tuning`、HEADは文書作成前時点で
`e2ac515cd08a140041e7e63a0fbca07dd6920071`。

## 結論

**PR #1262はまだマージしない。** 外部監査でHigh指摘が1件残っている。

- 本体PR: https://github.com/moorestech/moorestech/pull/1262
- companion master PR: https://github.com/moorestech/moorestech_master/pull/38
- masterブランチ: `perf/mapobject-distance-visibility`
- master HEAD: `274b6d9fb8828e06a27c906d6122d8504dcaa9ce`
- マージ順: **#38 → #1262**
- Squash and mergeは禁止。通常のmerge commitを使う。

## 未解決のHigh指摘

対象: `MapObjectDistanceVisibilityController.OnStateChanged`

`CullingGroup.onStateChanged`は距離band変化だけでなく、カメラの視錐台へ出入りした場合にも発火する。
現在は全イベントを`ApplyDistanceBand`へ渡すため、約3万個のmapObjectがある状態でカメラを旋回すると、
距離が変わっていない個体までqueueへ入り、今回の最適化効果を侵食する。

最小修正の方向:

```csharp
private void OnStateChanged(CullingGroupEvent sphereEvent)
{
    if (sphereEvent.previousDistance == sphereEvent.currentDistance) return;
    ApplyDistanceBand(sphereEvent.index, sphereEvent.currentDistance);
}
```

距離band未変更イベントをqueueへ積まない回帰テストも追加する。
新しいtest専用public APIはproductionへ残さないこと。

監査正本:
`../moorestech_logs/harness/moores-code-review/runs/2026-08-24-1630/codex-audit.final.md`

## 修正後の必須確認

ユーザーは「テストとコンパイルはこっちで見とく」と発言したが、別セッションへ移るため、
マージ判断前に誰が実施したかを確認し、結果をPRへ残すこと。

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObjectDistanceVisibility|MapObjectRendererVisibility|CameraManager|MapObjectProbeUsage"
uloop get-logs --project-path ./moorestech_client --log-type Error
```

加えてMainGameで次を目視・Profiler確認する。

- カメラ旋回でCPU spikeや距離更新queue滞留が発生しない
- 通常mapObjectが350m以遠で消え、340m未満で戻る
- landmark 29種が遠距離でも残る
- カメラ切替直後に新カメラ基準へ揃う
- SSAO、影、Reflection Probe無効化による許容不能な画質劣化がない

## これまでの確認結果

最終High修正より前の個別実行結果:

- スキーマ契約テスト: 1件PASS
- CameraManagerテスト: 3件PASS
- 距離カリング関連テスト: 8件PASS
- 関連回帰テスト: 21件PASS
- probe／Addressable関連テスト: 3件PASS
- コンパイル: errors 0 / warnings 0
- PR前決定論チェック: confirmed 0

PR作成直前の独立レビュー2系統は、DIを通らず破棄される`SkitTester`経路で
`MapObjectGameObjectDatastore.OnDestroy`がnull参照になる問題を検出した。
これは`e2ac515cd`で修正済みだが、そのコミット後のUnity compile/testは未実施。

## CI状態

2026-08-24 16:51 JST時点:

- #38: `MERGEABLE / CLEAN`、zws-json-checkとCodeRabbitは成功
- #1262: `MERGEABLE / UNSTABLE`
- #1262成功済み: invalid-char、Windows compile、macOS compile、Web UI test
- #1262実行中: Mooresmaster Test、EditMode Test
- #1262待機中: CodeRabbit

必ず最新状態を再取得する。

```bash
gh -R moorestech/moorestech_master pr view 38 --json mergeable,mergeStateStatus,statusCheckRollup
gh pr view 1262 --json mergeable,mergeStateStatus,statusCheckRollup,reviews,comments
```

## PRに含まれる最適化

- SSAOをDepth source・AfterOpaque・downsampleへ変更
- shadow distanceを120、cascadeを2へ削減
- SSAO radiusを`0.0375`へ調整
- UniversalRP High Qualityのreflection probe atlasを無効化
- 通常mapObjectへ350m非表示／340m再表示のrenderer限定カリングを追加
- master必須項目`distanceVisibilityType`で通常物とlandmarkを分類
- 全195 mapObject prefabと今後の生成規則でLight/Reflection Probeを無効化
- 最上位Camera変更をUniRx通知し、カメラ切替時に距離状態を再評価

計測済みの参考値:

- 初期: 20.8ms / 9,507 draw calls / 8.45M triangles / 1,250 shadow casters
- URP・影調整後: 15.7ms / 5,116 draw calls / 4.71M triangles / 519 shadow casters
- 350mカリング試験: 約14.3ms / 4,496 draw calls / 4.8M triangles / 466 shadow casters
- probe無効化試験: 15.7ms → 14.5ms

## 注意する同梱変更

コミット`127d84d52 landing podの設定`でlanding podが約15m移動している。
PR本文には同梱を明記済みだが、レンダー最適化とは独立した挙動変更である。
意図した変更かユーザー判断が取れていなければ、マージ前に確認する。

また、SSAO radiusとglobal reflection probe atlas変更はUnityが保存した差分を今回のPRへ含めた。
最終的な画質確認は未完了なので、目視確認なしでマージしない。

## 関連資料

- `docs/adr/0032-mapobject-distance-culling.md`
- `docs/superpowers/plans/2026-08-24-mapobject-render-distance-optimization.md`
- `.decisions/2026-08-24-mapObject遠景ランドマークはmaster区分で350mカリングから除外する.md`
- `../moorestech_logs/harness/moores-code-review/runs/2026-08-24-1630/`

## 完了条件

1. High指摘を修正し、回帰テストを追加する
2. 最新HEADでcompile・関連テスト・ログ確認を完了する
3. 画質と統合Profilerを確認する
4. #38と#1262の全必須チェックがgreenであることを確認する
5. landing pod同梱の意図を確認する
6. #38を通常mergeし、その後#1262を通常mergeする
7. `bd close moorestech-ara.1 --reason="..."`で結果を記録する
