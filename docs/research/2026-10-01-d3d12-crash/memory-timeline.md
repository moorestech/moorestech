# メモリ計数・時系列・最後の撮影

## 元ダンプのメモリをどう読んだか

process streamはrevision 2 / flags `0x1f` / 152 bytes、system streamはrevision 2 / flags `0x3f` / 524 bytesを検査した。構造体のrevision、flags、padding、stream境界を確認して読む。生bytesが0でも、対応する有効フラグがなければ「使用量0」ではなく欠測である。

| 計数 | bytes | 留保 |
|---|---:|---|
| game private commit | 14,640,349,184 | ダンプに採取された現在値 |
| game peak private commit | 14,680,862,720 | 生存期間の最大値、ピーク時刻なし |
| Job private commit | 26,094,075,904 | Job ID・構成PIDは記録されていない |
| Job peak private commit | 32,508,932,096 | 最大値の発生時刻なし |
| system commit | 35,469,414,400 | ダンプ採取値 |
| system commit limit | 41,841,123,328 | 採取時点の上限 |
| system peak commit | 41,838,669,824 | 現在limitとの同時性なし |

クラッシュ時点の採取値では、commit余白は `limit − commit = 6,371,708,928 bytes ≒ 5.934116GiB`（約5.93GiB）ある。これは下記の「残余charge減少の条件付き下限」とは別の量である。ピーク値は現在limitの近傍だが厳密な同値ではなく、差は599ページ。別ダンプでは過去peakが現在limitを超えるため、現在limitを過去の固定値と扱わない。

gameのpeakとcurrentの差は約38.637MiB。Jobとsystemのpeakからの減り幅が近くても、同じプロセスが同じ時点に解放したことにはならない。Job欄の非ゼロ値は所属を支持するが、構成プロセスや原因までは識別しない。別の検証実行では `IsProcessInJob` が成功し所属falseだった。これは元実行の非所属を証明しない。

## 条件付きで導ける下限

同じ集計範囲で `J(t)=G(t)+R(t)`、`G(t)≤Gmax` とし、currentのJとGが共通時点cを表すなら、Job peak時点t*からの残余charge減少は次の下限を持つ。

`R(t*)−R(c) ≥ (Jmax−J)−(Gmax−G) = 6,374,342,656 bytes ≒ 5.936569GiB`

systemにも同じ加法・同期前提を置けば約5.894100GiB。game peakとaggregate peakが同時である必要はない。一方、元ダンプには各欄の採取時差やその間のcharge変化上限、Job所属履歴がないので、この条件付き下限を元の無条件の実測値にしない。

小さい全列挙計算で同期モデルの不等式を検算し、非同期採取で下限が破れる反例も保存した。これは元のメモリ推移の再現ではない。system残余は他プロセスのprivate commitだけとも限らない。下限からOOM消費者、解放時刻、最後のallocation、Close故障原因は特定できない。

## UnityのGPUメモリ表示を訂正した

一致版 `CheckMemUsage` は `QueryVideoMemoryInfo` の現在usageとbudgetを比較し、OOMでなかった旨を印字する。過去のallocation失敗履歴を取得する処理ではない。

さらにログの予約関連2フィールドはprintf引数の順が逆だった。原ログは改変せず解釈を訂正した。

| 正しいDXGIフィールド | Local bytes | Non-Local bytes |
|---|---:|---:|
| Budget | 11,772,362,752 | 16,219,271,168 |
| CurrentUsage | 3,002,650,624 | 421,736,448 |
| AvailableForReservation | 6,020,399,104 | 8,243,853,312 |
| CurrentReservation | 67,108,864 | 0 |

約3.0GBは約2.796GiBの現在usage。64MiBは予約済み量であり予約可能残量ではない。予約値を物理空きVRAMやPC全体のcommit余白に変換しない。比較相手は別GPUの検証機なので、「元GPUが直前に大量解放した」とは判断できない。

## ログ順序と実時間は別

元 `Player-prev.log` は生bytesをLFで区切った行番号を使う。CR単独も改行として数える読み方では途中から番号がずれる。

| LF行 | 記録 |
|---|---|
| 570–571 | ffmpeg ENOMEM、writer側BrokenPipe |
| 574 | Fence Wait(2837)、観測2836 |
| 576–577 | Fence Wait(337361)、観測337359、FrameStatistics警告 |
| 582–583 | 録画Unavailable、video欠損 |
| 584–585 | Fence Wait(359163)、観測359162、Device failed 80004005 |
| 598–599 | Unrecoverable D3D12 device error、Crash |

ffmpeg stderrとwriter警告は別スレッドから集約される。表はログへの記録順で、別プロセス内部の原因発生順を全順序で保証しない。Fence値もserver tickやframe番号ではない。3件を同一Fenceとして数値差から時間へ換算しない。

保存retentionログの「保持開始tick + 2400」は保存像を取ったtickであり、非同期書出し・完了queue処理後のログ出力時tickではない。serverの20tick/秒は論理時間で、処理遅延やtick復元がある。したがって「枯渇からESCまで約3分」という初期推論は撤回した。

dump headerの2026-09-26 15:49:41 UTCと、報告bundle作成15:52:11 UTCは異なる時刻である。後者をクラッシュ発生時刻にしない。セッション名由来の時刻もworldロードや論理tick開始ではない。

## packet・snapshotから分かる最終capture

75ファイル、31,336 recordsの保存packetを検査した。形式はUInt64 tick / Int32 length / payload。保存対象はclient→serverの受信requestで、server→client response/event全量ではない。現在UTC・monotonic clockを最初のFence警告へ対応づけるアンカーは確認できなかった。

最後のPause記録はtick43785、capture requestと実在snapshotはtick43786。最大tick以後に保存されたrequestは当該captureの1件で、次のpacketファイルは空だった。これは以下を同時に証明しない。

- server responseがclientへ届いたこと。
- completion eventが送信・受信・採用されたこと。
- Screenshotの描画・画像ファイル保存が完了したこと。
- 最後のFence警告や失敗Closeがその画像の命令に属したこと。

requestのSequenceId、server CaptureId、C#のbeginCount、workspace ID、native Screenshot/list/fenceは別の識別子。保存資料にはそれらを貫く共通IDがない。最終captureのserver処理の証拠は強まったが、最後の撮影と失敗命令の間は埋まっていない。

「録画区間を確定できません」はメニュー入場だけでなく送信後再取得でも出得るため、警告文字列単独は物理ESCキー押下の固有記録ではない。別のPause記録との照合を根拠にする。

## 動画・描画時刻警告の限界

保存された15本のMP4にはh264、1280×720、10fps等の情報がある。媒体検査で保存サンプルをデコードできても、失敗した未保存フレームや元ffmpegの生存時間は復元できない。segment内timestampがリセットされ、原bundleに `frames.tsv` がないため、PTSやファイルmtimeを原Fenceの実時刻へ直接結合できない。

`GetFrameStatistics` の警告は、成功した問い合わせの非ゼロ描画時刻が同値を繰り返した分岐を示す。APIのHRESULT失敗、ユーザーのresize、DWMクラッシュ、CEF resource再生成の証拠ではない。

## 非公開証拠索引

以下は証拠ルートからの相対パス。

- `investigation-20260929/job-counters-findings.md`
- `investigation-20260929/memory-peak-bound-findings.md`
- `investigation-20260929/memory-bound-integration-review.md`
- `investigation-20260929/gpu-memory-interpretation.md`
- `investigation-20260929/memory-label-review.md`
- `investigation-20260929/timeline-findings.md`
- `investigation-20260929/packet-clock-evidence-review.md`
- `investigation-20260929/final-capture-completion-evidence-review.md`
- `investigation-20260929/frame-statistics-findings.md`
- `investigation-20260929/media-validation/media-validation-review.md`
