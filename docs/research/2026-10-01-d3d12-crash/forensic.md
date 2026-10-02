# 元クラッシュの法医学的調査

## 結論と確度

**元クラッシュは未再現で、根本原因は未確定。** 確定できたのは、Unity 6000.3.8f1 の通常描画の command-list Close相当（vtable slot 9）の呼出しが `E_FAIL (0x80004005)` を返し、その結果を Unity が致命扱いした経路である。

録画の書込み失敗、Fence の未達警告、最後のポーズ画面の撮影要求は同じ実行に存在する。しかし、それらを失敗した command list/resource の同一性で結ぶ記録はない。「録画停止がGPUクラッシュを起こした」「メモリ不足でGPUが故障した」「CEFが原因だった」は確定事項ではない。

本書の「一致」は照合した範囲に限定する。PDB・PEの識別情報一致、固定Gitソース一致、実配布物全体のbyte一致、元実行中のobject状態は別の証拠である。

## ダンプとシンボルから同定した失敗

元ダンプの例外は `0x80000003` の breakpoint で、D3D12 Submission Thread 上の Unity 致命処理にある。GPU命令そのものがCPU例外を投げた状態を採取したわけではない。

UnityPlayer のダンプ内 CodeView と PDB の GUID/age、対応DLLの CodeView が一致した。対応する公開シンボルを用い、通常の `GfxTaskExecutorD3D12::DoExecute` から `CheckDeviceStatus` に入る戻り位置を確認した。

その直前は command list の COM vtable の `+0x48` を this のみで呼び、戻り値 EAX をそのまま `CheckDeviceStatus` に渡す命令列である。公開ABIの slot 9 は `ID3D12GraphicsCommandList::Close` に対応し、周囲の提出準備処理とも一致する。先行Resetの古いEAXを誤って読んだという説明ではない。

ただし、元のCOM object本体とvtableを含むheapは保存されていない。APIの同定はABI・一致シンボル・周囲の処理によるものであり、元objectの内容を直接読んだ結果ではない。

`CheckDeviceStatus` が device removed reason を問い合わせるのは別のHRESULTの分岐であり、今回のE_FAILはその分岐を通らない。ログの `Device failed` という文言だけで、TDRや実際のデバイス除去を確定しない。

## Closeが返したE_FAILの意味

元モジュール記録と対応する D3D12Core のDLL/PDBを照合し、Close の状態処理を限定確認した。Close は、既に保持した失敗HRESULTを返す経路と、Close時点の追加検証で失敗する経路の両方を持つ。

| 静的に確認した経路 | 元実行で不足する情報 |
|---|---|
| 記録用slotがない状態で直接E_FAILを返す | Close入口のobject状態。終了後にもslot状態が変わるので、事後値だけでも不十分 |
| 開いたrender passや未完了queryの検証で失敗を記録する | 対象状態と最初の診断メッセージ |
| 先に保持した失敗を返し、下位Close dispatchを省略する | 最初に失敗を保持した命令、HRESULT、list identity |
| 下位処理へdispatchした後、保持結果を返す | 元のdispatch先・resource状態・診断履歴 |

保持処理は既存の失敗を後の失敗で上書きしない。このため「最後に呼ばれたCloseの検証が最初の故障」とは限らない。一方でClose内部にも追加検証があるので「全エラーはCloseより前に発生した」ともいえない。

列挙した分岐を全てのエラー経路の網羅とみなしていない。元heap、最初のvalidation error、対象list/resourceがないため、記録中の遅延エラーとClose内部の検証失敗は判別できなかった。GPU使用中のallocator Resetが後のClose失敗に影響した可能性も、これだけでは肯定も否定もできない。

## 全threadの確認と限界

151 thread 全ての印字されたstackを分類し、後に一致する D3D12Core / d3d11on12 のシンボルで該当箇所を補った。これは一時点の限定深度のstackであり、全履歴や全managed frameの復元ではない。

| 観測 | 読み取れること | 読み取れないこと |
|---|---|---|
| mainのpresentation待機 | 採取時のpresentation待ち | 先行Fence警告の呼出元や対象resource |
| submission threadのcrash handler待機 | 致命処理後の待機。例外contextは別途Close失敗を保持 | 新しいGPU待機やデッドロックの証明 |
| graphics workerのcommand-stream入力待機 | CPUコマンド入力を待っている | GPU仕事が全て完了したこと |
| 4本のD3D background scheduler待機 | 一致シンボルで通常のcondition待機まで復元 | 特定Fenceの未達やproducer停止 |
| d3d11on12のBatchedContext worker待機 | バッチworkerが待機中 | 待機objectやCEFとの対応 |
| GC/job/driver/その他workerの待機 | 採取時の待機分類 | 全driver深部の正確なunwind、以前の不正命令 |

別プロセスのCEF server/helperの健康状態は、ゲーム内にそのstackが見えないことからは判断できない。GPU queue上のWaitも、CPU threadが同じAPI内に止まっている必要はない。

未同定JIT frameについてもFunctionTableとMemoryListの範囲を検査した。対象関数の収録は199/387 bytes、callerは0/794 bytesで、managed method名や撮影世代は復元できなかった。FunctionTable末尾の2,360 zero bytesは用途未同定として残し、paddingと断定していない。

## ログとメモリ計数の解釈

元ログには rawvideo demux のENOMEM、続く書込みのBroken Pipe、その後の録画Unavailable、Fence未達とClose E_FAILがある。各エラーを発したffmpegのPID/世代や、失敗した書込みAPI、コミット解放時刻はない。

クラッシュ時のゲームprivate commitは約13.635 GiB、Job privateは約24.302 GiB、Job peakは約30.276 GiBだった。元ダンプのJob区画の有効bitは確認したが、Job ID・構成PID・ピーク時刻は保存されていない。ピーク差の近似から「ffmpegが約6 GiBを解放した」とは帰属できない。

クラッシュ時に空きcommitがあることは、以前のENOMEMと両立する。逆に以前のENOMEMだけでは、後のE_FAILが直接の確保失敗とも、枯渇主体がffmpegともいえない。「枯渇解放から約3分後」という初期説明も、解放時刻の実測ではない。

## Monoについて除外できた範囲

Broken Pipeの発生箇所を調べる過程で、候補Monoバイナリの `MonoIO.Close` を内部呼出しtableから特定した。近いexport名や文字列検索だけで関数を推定していない。

確認したnative entryはout-errorを0に初期化し、`CloseHandle` が失敗した場合だけ `GetLastError` を転記する。成功時に古いエラー109を拾う分岐はなく、このentry内にWrite/Flush/Drain呼出しもない。従って「成功したnative Closeが古い109を誤報した」という限定的な説明は支持されない。

これはMono全体の無欠陥証明ではない。元ダンプはこのentryの命令byteを含まず、候補との対応はmodule metadataに依存する。元のmanagedライブラリ全byte、FileStream状態、OS handle状態、writerのWrite/Flush/Closeのどこでthrowしたかは未観測である。writer例外後の状態やGPU故障への因果も、Mono候補を絞っただけでは決まらない。

## 最後の撮影との接続が止まった理由

元のPause、撮影に伴うserver要求、そのtickのsnapshotまでは対応する。しかし保存streamはclient→serverで、response採用やnative Screenshot消費のtraceではない。CaptureIdやserver tickはGPU list識別子ではない。

Screenshot→D3D12描画→Flushの静的経路は確認したが、最後の要求がどのlistに入り、そのlistが失敗したかを結ぶrecordがない。wrapper、COM pointer、executorの必要なheapも欠落している。

crash bundleは一時Screenshot workspaceを回収する設計ではない。画像がないことは撮影未発行・未完了の証明にならず、ScreenshotのMissing項目がないことも成功証明にはならない。

追加の原実行資料なしに同じstackやmoduleを読み直しても、このruntime identityと最初の失敗履歴は埋まらない。今回の終了時点では、既存資料だけから原因を区別できる未実施の具体的解析を得られなかった。

## 非公開証拠索引

基準は調査reproディレクトリ。証拠固定commitは `5dad531e25f8af56764967643145ae17b3e14a1d`。

- `CURRENT-STATE.md`
- `investigation-20260929/forensic-findings.md`
- `investigation-20260929/core-close-static-review.md`
- `investigation-20260929/forensic-object-limits.md`
- `investigation-20260929/job-counters-findings.md`
- `investigation-20260929/reporter-os-symbols/all-threads-independent-review.md`
- `investigation-20260929/reporter-os-symbols/graphics-threads-independent-review.md`
- `investigation-20260929/mono-exact-implementation/mono-exact-close-review.md`
- `investigation-20260929/mono-exact-implementation/integration-review.md`
- `investigation-20260929/original-next-evidence-20260930/original-gap-audit.md`
- `investigation-20260929/screenshot-registration-causal-refinement.md`
