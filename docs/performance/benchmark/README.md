# パフォーマンス・ベンチマーク

改善のたびに同じ条件で計測し、結果を JSON で残して前後を比較するための仕組み。

## 何を固定しているか
| 条件 | 内容 |
|---|---|
| マシン | windows-desktop（i7-12700／RTX 3060 Ti） |
| 実行形態 | Unity Editor の PlayMode で「ローカルでプレイ」 |
| ワールド | 固定したベースラインセーブを、計測のたびに作業用フォルダへ複製して読み込む（遊んで状態が変わったセーブは使わない） |
| 待ち方 | Renderer 数の変化が15秒で0.1%以内になってから、さらに20秒待つ（読み込み直後の初期同期バーストを除外） |
| 計測窓 | fps（Profiler なし）10秒 → Profiler 6秒 → 受信イベント件数 6秒。互いの計測負荷が混ざらないよう別々の時間窓で取る |

最適化モード（Debug／Release）・品質設定・解像度は固定せず、結果 JSON の `environment` に記録する。比較スクリプトは、これらやベースラインセーブのハッシュが基準と違うと警告を出す。

### ベースラインワールド
| 名前 | 置き場（windows-desktop） | save.json の SHA256 | 規模 |
|---|---|---|---|
| `2026-10-22-world_1` | `C:\moorestech-perf\baselines\2026-10-22-world_1\` | `6BF6222FFCDA5CD068F10E48EB73BC68F2D1128DB10E084360FE7738184A5445` | Renderer 約21.7万／状態イベントを出すブロック約1,750個 |

テスターのワールド（`%APPDATA%\.moorestech\Saves\world_1`）を 2026-10-22 にコピーしたもの（`save.json`・`map.json`・`world.json`）。セーブ形式が変わってもロード時のマイグレーションで読める。ただ、マイグレーションで盤面が変わった場合は、新しいベースラインを作り、旧ベースラインとの比較をやめる。

## 計測のしかた
1. windows-desktop で計測したいブランチを checkout し、Editor でコンパイルしておく（`uloop compile --project-path G:\GitHub\moorestech\moorestech_client`）。Editor が落ちている場合の起動方法は [調査ログの再計測手順](../2026-10-22-windows-desktop-low-fps/README.md#再計測の手順) を参照。
2. Mac から実行する：
   ```sh
   docs/performance/benchmark/run-benchmark.sh <label>
   ```
   `<label>` には変更内容を表す短い名前を付ける（例: `baseline`、`dedupe-envelope`）。結果は `results/<日時>_<label>_<コミット>.json` に保存される。
3. 同じ条件で**3回**測る（1回ごとのぶれが大きいため）。
4. 比較する：
   ```sh
   docs/performance/benchmark/compare.py results/*_baseline_*.json results/*_<label>_*.json
   ```
   - 同じ label の結果は1グループにまとめ、平均と（最小〜最大）を出す。
   - 最初の label が基準になる。基準との範囲が重ならないほど離れた指標にだけ、✅（改善）か ⚠️（悪化）が付く。
   - Markdown の表が出るので、PR の説明にそのまま貼れる。

### 注意
- **ProfilerMarker は `perf/event-dispatch-profiler-markers` ブランチにしか無い。** マーカーの無いコミットを測ると `PacketExchange.*`・`BlockState.*` の行が「—」になる。fps・フレーム時間・イベント件数は、マーカーが無くても比較できる。
- 計測中は Editor の作業用ワールドへの差し替え（環境変数 `MOORESTECH_DEFAULT_WORLD_DIRECTORY`）が有効になる。スクリプトの最後で解除するが、途中で止めた場合は `snippets/cleanup.cs` を実行して解除する。解除しないと、その Editor で以後に通常プレイしたとき作業用ワールドが読まれる。
- Profiler の窓は短く保つ（このワールドで長く記録すると Editor がメモリ不足でクラッシュした実績がある）。
- **fps は Editor の表示状態で大きく変わる。** 2026-10-22 のベースライン4回では、同じコード・同じワールドで 6.3〜6.5fps の回と 10.6〜11.1fps の回に分かれた。差は受信処理ではなく、表示待ちや PlayerLoop の外の Editor 処理（合わせて約50ms/フレーム）で、誰かが画面をリモートで見ていたかどうかなどが原因と推測している（未確認）。
- 環境に左右されにくいのは「受信処理 µs/件（応答を除く）」（4回とも 30〜31µs）と「受信イベント 件/秒」。改善の効果はまずこの2つで判断し、fps は参考にする。

## 結果 JSON の中身（schemaVersion 1）
| キー | 内容 |
|---|---|
| `commit` / `branch` / `trackedChangesOnHost` | 計測したコード。`trackedChangesOnHost` が 0 でなければ未コミットの変更を含む |
| `baselineWorld` | 使ったベースラインワールドと save.json のハッシュ |
| `environment` | Unity バージョン・最適化モード・品質・vSync・解像度・GPU・CPU |
| `scene` | Renderer 数・GameObject 数 |
| `fps` | Profiler なしの fps とフレーム ms 平均 |
| `profiler.frameMs` | Profiler 記録中のフレーム時間の平均・p50・p95・最大 |
| `profiler.markers` | マーカーごとの1フレームあたり totalMs・selfMs・calls。主要マーカー、`PacketExchange.*`・`BlockState.*` の全部、self 上位20を含む |
| `events` | 受信イベントの件数/秒・payload バイト/秒、状態イベントを出したブロック数。`byKind` はタグ種別ごとの内訳で、ブロック状態は状態キーの組み合わせ別 |

## スニペット一覧（`snippets/`）
| ファイル | 役割 |
|---|---|
| `enter-world.cs` | 作業用ワールドへ差し替え、画面スリープを抑止して Editor ウィンドウを通常表示に戻し、前回クラッシュ報告を閉じてローカル開始 |
| `scene-stats.cs` | 定常判定用の Renderer 数など |
| `environment.cs` | 計測条件 |
| `fps-start.cs` / `fps-collect.cs` | Profiler なしの fps 窓 |
| `profiler-start.cs` / `profiler-collect.cs` | Profiler 窓 |
| `events-start.cs` / `events-collect.cs` | 受信イベントの件数窓（受信 Subject を一時的に差し替える） |
| `cleanup.cs` | ワールド差し替え・画面スリープ抑止の解除と Profiler 停止 |

## 結果一覧
| 日時 | label | コミット | fps | PlayerLoop ms | 受信イベント 件/秒 | 受信処理 µs/件 | メモ |
|---|---|---|---:|---:|---:|---:|---|
| 2026-10-22 18:01 | baseline | 09b546dc98 | 6.46 | 116.7 | 18,687 | 29.9 | |
| 2026-10-22 18:07 | baseline | 09b546dc98 | 6.25 | 123.5 | 17,027 | 31.1 | |
| 2026-10-22 19:02 | baseline | 09b546dc98 | 10.60 | 83.8 | 17,340 | 30.8 | 表示待ち 0ms |
| 2026-10-22 19:08 | baseline | 09b546dc98 | 11.05 | 88.4 | 16,317 | 30.6 | 表示待ち 0ms・画面スリープ抑止あり |
