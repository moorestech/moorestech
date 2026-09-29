# Unity テストの失敗率と所要時間の調査（2026-09-29）

## 結論

2026-09-15〜09-29 UTC の `Unity Test` CI 126 実行では、最終結果が成功 76、失敗 36、キャンセル 14 だった。キャンセルを除く失敗率は **36/112 = 32.1%**。ただしこれは「Unity 自体の不安定率」ではない。失敗 36 件のうち 22 件は CI に存在しない有料アセットを新しいテストが参照した同一の設計不備、4 件は C# コンパイルエラーだった。このアセット依存は 09-25 の `59e7fc1df` で固定フィクスチャへ置き換えられた。09-26 以降はキャンセルを除く 31 実行中、成功 30、失敗 1（3.2%）である。期間が短く、恒常的な 3.2% と解釈してはいけない。

速度面は構造的な問題が残る。成功実行 [36540392345](https://github.com/moorestech/moorestech/actions/runs/36540392345) では 9 shard のジョブ時間合計が 100.9 分、うち Unity 実行 step が 72.2 分、NUnit XML が記録した実行区間は 21.9 分だった。**Unity 実行 step の開始から NUnit 開始までだけで 47.1 分（9 shard 合計）**かかる。9 shard が並列なのでユーザーの待ち時間は 16.5 分だが、各 shard が独立して Editor を起動・読み込みする固定費が大きい。

## 調査方法と分母

- `gh run list --workflow run_test.yml --created '>=2026-09-15' --limit 500` の 126 実行を母集団とした（最古 09-15 05:16 UTC、最新 09-29 08:03 UTC）。実行ごとの最終 attempt、12 前後のジョブ、失敗 step を Actions API で取得した。再実行 51 件は初回 attempt のジョブも取得した。
- 最終失敗 36 件と、再実行で最終成功した 15 件の初回失敗ログを読み、NUnit XML の失敗名・メッセージ、C# エラー、40 分打ち切り、Docker 終了コードを区別した。単なる `Unity Test` workflow の赤をエンジン障害には数えなかった。
- 所要時間は Actions の step 時刻で算出した。最終 attempt のみを見た集計と、`createdAt` から `updatedAt` までの全 attempt を含む待ち時間を明記して区別した。具体的な NUnit 区間は成功実行 1 件の 9 artifact を検査した。
- ローカルの `uloop run-tests` には全呼び出しを一意に数えられる中央台帳がない。このレポートの失敗率は **GitHub CI の率**であり、ローカル Editor の失敗率ではない。ローカルについては既存障害記録と手順を調べたが、率は提示しない。

## 失敗の内訳

分類は重複する。たとえば有料アセット依存テストが失敗した実行で、別 shard が 40 分ハングした例が 2 件ある。

| 失敗の種類 | 最終失敗 run 数 | 根拠と解釈 |
| --- | ---: | --- |
| 非公開親 prefab が CI にない | 22 | `Redwood/Grass1`、`BirchTree02` 等の `Preview asset is missing`。`client-play-1` と `client-remainder` に集中。[例 35844213388](https://github.com/moorestech/moorestech/actions/runs/35844213388) は 11 件中 1 件、1646 件中 5 件が失敗。両 shard の失敗を独立した Unity 障害として数えるべきでない。 |
| C# コンパイルエラー | 4 | [36156360181](https://github.com/moorestech/moorestech/actions/runs/36156360181) は `Core.Update` の参照エラーが全 9 shard へ拡散。[35506774729](https://github.com/moorestech/moorestech/actions/runs/35506774729) は `heightOffset` 引数欠落。 |
| 40 分の shard 実行打ち切り | 3 | [35455018021](https://github.com/moorestech/moorestech/actions/runs/35455018021) は `client-remainder` の実ハング。他 2 件はアセット失敗と同時発生。Actions の `Detect Unity shard runner hang` 失敗 step で判定。 |
| その他の NUnit テスト失敗 | 7 | PlayMode 遷移テストの 180 秒 timeout、テストの分類ガード失敗、列車テストの `Tree prefab ... missing` 等。入力や対象テストが異なり、一括して Unity エンジン故障とはできない。 |
| Unity/Docker が終了コード 134 で停止 | 1 | [35876651960](https://github.com/moorestech/moorestech/actions/runs/35876651960)。NUnit 結果を書かず停止。ログだけでは Unity、ネイティブ依存、テストコードのどれが abort したか確定しない。 |
| Web UI の vitest のみ | 1 | [35434647273](https://github.com/moorestech/moorestech/actions/runs/35434647273)。Unity shard は失敗していない。 |

最終 attempt の shard 別では `client-remainder` が 123 ジョブ中 33 失敗、`client-play-1` が 123 ジョブ中 29 失敗だった。共通のアセット欠損やコンパイルエラーが両方に現れるため、この 2 数値を足して独立した障害件数にはできない。

アセット依存の因果は確認できる。Addressables のアドレスと `Assets/AddressableResources/.../Grass1.prefab` 自体は追跡されているが、prefab variant の親 GUID `63e885cd...` は git 管理外の `Assets/PersonalAssets/moorestech-client-private/.../Grass1.prefab` にある。CI はこの private repo を checkout しない。`EditorTerrainAssetLoader` は `AssetDatabase.LoadAssetAtPath<GameObject>` が null の場合に例外を出す。これは [2026-08-19 の既存裁定](../../.decisions/2026-08-19-有料アセット依存テストはIgnoreCIでCIから外す.md) と同形の故障であり、09-25 の `59e7fc1df` は外部依存検査とテスト専用アセットを導入した。

別の既知ハングは Beads `moorestech-7gsc` の記録で PlayMode 起動時の同期 `Camera.Render()` 内に特定され、通常フレーム描画へ変更済み。09-18 の [ADR 0063](../adr/0063-ci-hang-shard-timeout-and-icon-capture-instrumentation.md) で 75 分ジョブ打ち切りより先に 40 分 step 打ち切りを入れた。今回の初回失敗ログにも、後述の再実行成功例にもこの世代のハングが含まれる。現在の主因として再断定する根拠はない。

## 再実行の効き方とコスト

126 実行のうち **51 件（40.5%）**が attempt 2 以上に進んだ。最終結果は成功 15、失敗 31、キャンセル 5。成功した 15 件の初回失敗は、5 件が shard 打ち切り、8 件が NUnit の 180 秒 timeout（PlayMode 系が中心）、残り 2 件はログから原因を確定できない。従って自動再実行には実際の回復効果があった。一方、失敗 31 件のうち繰り返し現れた同一アセット欠損やコンパイルエラーをそのまま再試行しても成功しない。

現行 [.github/scripts/ci-auto-rerun.cjs](../../.github/scripts/ci-auto-rerun.cjs) は `Unity Test` の初回失敗を、失敗内容を問わず再実行する。今回のアセット欠損による最終失敗 22 件のうち 17 件は再実行済みだった。その 17 件の最終 attempt のジョブ時間は合計約 **2,229 runner 分**。これは Actions ジョブ実行時間の合算で、課金額ではない。全ジョブが同じ原因だけを処理した時間とも言えないが、同じ決定的失敗を抱えたコミットを繰り返し起動した規模を示す。初回失敗を無条件で再試行する規則は、原因が明白なコンパイルエラーと外部資産欠損に対して時間を浪費する。

成功 1 回目の実行時間中央値は **16.6 分**（61 件）、最終失敗の中央値は **46.3 分**（36 件、再実行待ちを含む）。再実行を含む成功 76 件の中央値は 17.6 分である。失敗実行の長さには再実行や 40 分 timeout が混ざるので、純粋なテスト計算時間と同一視できない。

## どこに時間を使っているか

[36540392345](https://github.com/moorestech/moorestech/actions/runs/36540392345) の XML とジョブ時刻を突合した。3625 テストが 9 shard で実行された。`NUnit 開始前` は Unity Test step 開始から XML の `start-time` まで。クライアント PlayMode shard で 6.5〜7.8 分、server shard で 4.0〜5.5 分かかった。

| 区間 | 9 shard 合計 | 読み方 |
| --- | ---: | --- |
| すべてのジョブの実行時間 | 100.9 分 | 並列 runner の累計。workflow 壁時計は 16.5 分。 |
| Unity Test action step | 72.2 分 | Editor 起動・import・テスト・結果出力。 |
| うち NUnit 開始前 | 47.1 分 | action step の 65%。最も大きい固定費。 |
| NUnit XML の実行区間 | 21.9 分 | テストランナーが記録した区間。PlayMode 内部の細かな所要時間を完全に説明する値ではない。 |
| NUnit 終了後 | 3.3 分 | action step 内の結果処理等。 |
| Unity Test step 以外 | 28.7 分 | checkout、Library cache restore、セットアップ、artifact 等。 |

成功ジョブの中央値は `client-play-1/2/3` が 10.2/10.4/10.9 分、`client-remainder` が 8.7 分、`server-remainder` が 13.9 分だった。checkout だけでも各 shard の中央値は約 1.4 分である。`client-play-3` は 12 テストの XML 実行区間が約 7 秒なのに、Unity Test step が 7.5 分、ジョブ全体が 11.1 分だった。独立した Editor 起動を shard ごとに払う現構成では、少数のテストを別 shard に置くほど runner 時間が増える。

## 推奨する次の検証

1. **既存修正の効果を継続監視する。** 09-25 の外部依存検査導入後、09-26〜29 の非キャンセル 31 実行は 30 成功・1 失敗。次の 2〜4 週間で run、shard、失敗 step、NUnit 失敗名を週次集計し、現在の率を確定する。特に [36243154920](https://github.com/moorestech/moorestech/actions/runs/36243154920) の `Tree prefab at index 1 is missing` はテスト固定フィクスチャ化の漏れか調べる。
2. **再実行前に決定的失敗を識別する。** C# エラー、NUnit の明白な資産欠損、外部依存検査の失敗では再実行を抑え、40 分打ち切り、プロセス abort、180 秒 PlayMode timeout は再試行対象に残す。step 名だけでは NUnit の内容を判定できないため、ログまたは結果 XML に基づく分類を別途実測してから変更する。
3. **軽い PlayMode shard の統合を比較実験する。** 9 shard は待ち時間を短くする一方、今回の成功例では 47.1 runner 分を NUnit 開始前に使った。`client-play-1/2/3` のような少数テスト shard を 1〜2 本へ統合した候補を、同一コミット・同条件で現行 9 shard と比較する。指標は壁時計、runner 分、テスト網羅、順序依存、ハング率。単純な 3→1 なら起動回数は減るが、テスト間干渉の実測前に恒久変更しない。
4. **ローカルの失敗率を測れる形にする。** `uloop run-tests` の呼び出し単位で開始・終了時刻、対象フィルタ、テスト数、NUnit failure、domain reload 中、timeout、Editor 接続失敗を記録する。現状のセッション transcript から呼び出しの重複や中断を除いた分母は復元しにくい。CI とローカルの率を混ぜずに比較できる台帳が必要。

## 制約

この調査で Unity Editor を新規起動して再現実験は行っていない。多数の CI 実行の観測調査であり、個別のハング機構を再現で確定したものではない。GitHub Actions の `cancelled` 14 件には PR 更新による正常な旧実行キャンセルが混ざるため失敗率の分母から除外した。09-26 以降の改善は修正後の観測として有力だが、PR 内容・実行負荷も変わっており、因果効果を単独で証明する比較実験ではない。
