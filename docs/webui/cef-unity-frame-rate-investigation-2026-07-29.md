# cef-unity Web UI 更新頻度低下 調査報告

- 調査日: 2026-07-29
- 対象症状: Web UI のクラフト進捗バーが約 1 fps まで低下する
- moorestech 対象: `~/moorestech-worktrees/tree3`
- cef-unity 対象: `~/WebstormProjects/cef-unity`
- cef-unity upstream: <https://github.com/JuhaKurisu/cef-unity/tree/main>
- moorestech 固定リビジョン: `64f9a5f3019d660e89a2909a7e1ca9d342aca5b1`
- cef-unity 調査時 main: `8fc504e221927f4cffb3052bc28b4a8a8624e094`

## 1. 結論

現時点では、**Chromium 本体の不具合ではなく、cef-unity が CEF を Unity に同期させるために追加した独自のフレーム駆動・待機・転送処理が主因**と判断する。

固定版と現行 main は、どちらも Rust dependency として `cef = "145.5.0"` を使用している。両者の既知挙動差は Chromium/CEF version の違いではなく、cef-unity wrapper の実装差である。

PC 全体の高負荷は発生のきっかけ・悪化条件ではあるが、「PC が重いから仕方なく 1 fps になった」だけでは説明できない。Unity 自体は約 58〜60 fps で動作している最中にも、CEF 内の JavaScript `requestAnimationFrame` と paint だけが一桁 fps まで低下した。

最も強く支持される問題は次の複合である。

1. Unity/C# 側が 0 フレーム遅延を狙い、paint が疎な状態で Unity メインスレッドを毎フレーム最大約 7 ms busy-spin する。
2. Rust server は Unity からの BeginFrame に加え、+3 ms、+6 ms の追加 BeginFrame を最大 2 回発行する。
3. Rust server は小さな dirty rect でも画面全体を Metal で同期コピーし、Mach port へ送る。
4. macOS の CEF message pump を 1 ms 間隔、最大 1000 回/秒で回す。
5. server が遅延した場合に IPC 内の BeginFrame を全件 drain してから CEF を 1 回だけ pump するため、キューの burst、pending flush の上書き、CEF 側のフレーム drop が起き得る。
6. 長時間稼働または Play/Stop 反復に対する Mach receive port、IOSurface cache、キュー状態の寿命管理に疑いがある。
7. moorestech の固定版は、既知の `1,1,1,0` paint 欠落を修正した cef-unity commit `a08f585` より古い。

ただし、`a08f585` へ更新するだけでは十分ではない。現行 main にも C# busy-wait、1000 Hz message pump、最大 3 BeginFrame、全画面同期コピーは残っている。

## 2. 症状がゲームロジックへ与える影響

クラフト進捗は `moorestech_web/webui/src/features/recipe/logic/useHoldCraft.ts:49-68` で `requestAnimationFrame` により更新される。進捗更新とクラフト周期の判定が rAF callback 内にあるため、CEF の rAF が低下すると表示だけでなくクラフト進行自体も遅くなる。

さらに `holdCraftLogic.ts` は 1 フレームの delta を最大 `0.25` 秒へ丸める。rAF が 1 Hz の場合、現実には 1 秒経過しても 0.25 秒しか進まない。このため、CEF の低フレームレートがゲーム側で約 4 倍に悪化して見える。

## 3. 実行経路

```text
Unity EarlyUpdate（約60 Hz）
  └─ C# SendExternalBeginFrame
       └─ Rust client: ipc-channelへ同期send（応答は待たない）
            └─ Rust server: IPC bridge → 無制限mpsc
                 └─ macOS 1 ms timer
                      ├─ キューを空になるまでdrain
                      ├─ CEF BeginFrame #1
                      ├─ 条件次第で +3 ms / +6 ms flush
                      └─ CefDoMessageLoopWork
                           └─ Chromium/Blink: rAF → layout → paint
                                └─ on_accelerated_paint
                                     ├─ dirty rectを使わず全画面Metal blit
                                     ├─ waitUntilCompleted
                                     └─ IOSurfaceをMach portへ送信
                                          └─ C# PostLateUpdate
                                               ├─ paint待ちbusy-spin
                                               └─ Unity textureを更新
```

CEF が提供しているのは External BeginFrame、off-screen rendering、message pump integration などの API である。+3/+6 ms flush、Unity メインスレッドの busy-spin、1000 Hz fallback、全画面同期コピーは cef-unity 独自の方針である。

## 4. 実測結果

### 4.1 低下中の測定

| 項目 | 測定結果 | 解釈 |
|---|---:|---|
| Unity frame rate | 約 58〜60 fps | Unity 全体が 1 fps なのではない |
| 単純な視覚変化を伴う rAF | 約 7 fps | CEF 内部の callback 自体が低下 |
| CEF accelerated frame id 増分 | 約 6.8 fps | Unity texture受信だけでなく CEF paint が低下 |
| `_zeroFrameWaitMs` 10 → 0 | rAF 約 7 → 11 fps | C# busy-wait が CEF と CPU を奪い合っている |
| `_zeroFrameWaitMs` 10 → 0 | paint 約 6.8 → 9.9 fps | busy-wait 無効化で CEF 出力も改善 |
| 実際の SVG clip rect アニメーション | rAF 約 18 fps、paint 約 17 fps | SVG 自体は負荷要因だが単独主因ではない |
| 実際の React hold loopを通信だけローカル成功化 | 平均 rAF 約 27 fps、paint 約 25 fps、瞬間約 10 fps | クラフト経路で変動は再現したが固定的な 1 fps は未再現 |
| ページ状態 | `visible`、`hidden=false`、`hasFocus=true` | 背景タブ throttling は棄却 |

`_zeroFrameWaitMs` の変更は実行時だけ行い、測定後 10 ms へ復元した。WebSocket の一時差し替え、JS probe、pointer state もすべて復元した。

### 4.2 長時間稼働状態

約 1 時間以上動作していた Play session では、ページの accelerated frame id が 3.36 秒間まったく増えない静止状態でも、おおむね次の CPU 使用率を観測した。

| Process | CPU |
|---|---:|
| Unity Editor | 約 135〜160% |
| cef-unity-server | 約 95〜146% |
| CEF Renderer | 約 112〜157% |

Rust server の 1 秒 sample では、765 sample 中 755 sample で main thread が 1 ms timer callback から CEF 内部へ入っていた。

Unity の毎フレーム CEF hook だけを約 5 秒停止すると、server と Renderer の CPU は約 10〜20 ポイント低下したが、病的な高CPU状態自体は消えなかった。新規 BeginFrame の供給だけでなく、すでに蓄積した CEF 内部処理、Web page の処理、または解放されない native state が関与している可能性がある。

### 4.3 Play 再起動後

Play を再起動すると、同じ `http://127.0.0.1:25173/`、同じ `_zeroFrameWaitMs=10`、server flush 有効状態でも、約 78 秒後の CPU は次の水準へ戻った。

| Process | CPU |
|---|---:|
| cef-unity-server | 約 3.2% |
| CEF Renderer | 約 0% |

この結果から、1000 Hz timer が存在するだけで server が常に 100% になるわけではない。**長時間稼働または特定の状態遷移によって、CEF 内部または cef-unity の供給・転送状態が病的な状態へ入る**ことが重要である。

server flush 無効化トグルでも再起動直後に server 約 3.3%、Renderer 約 0% となったが、flush 有効の再起動直後も同程度だった。再起動による状態リセットが交絡しているため、この測定だけを server flush の効果証明には使わない。

## 5. 確度別の原因候補

### 5.1 C# busy-wait の正帰還

**確度: 高。A/B で寄与を確認済み。**

実環境の `MainGameUI.prefab:453-459` は `_resolutionScale: 1`、`_zeroFrameWaitMs: 10` である。

固定版 `CefUnityBrowserSample.cs:684-743` は、新しい paint 後の 60 Unity frame を probe window とし、paint が来ないフレームでは最大約 7 ms、`PeekAccelFrameId()` と `Thread.SpinWait(64)` を繰り返す。

クラフトバーはマウスを押した後、継続的な Unity input event を発生させず、JavaScript rAF だけで動く。スクロールでは `_inputSentThisFrame` により 3 frame 後に待機抑止へ入れるが、クラフトバーにはその成功経路がない。

疎な paint が約 1 秒ごとに来ると、60 frame の probe window が繰り返し更新される。60 fps 時の上限は約 `7 ms × 60 = 420 ms/秒` で、Unity メインスレッドが 1 CPU core の約 42% を polling に使い得る。

成立し得る正帰還は次の通り。

```text
paint低下
  → C#がpaint待ちで長くspin
  → CEF Rendererへ渡るCPUが減る
  → paintがさらに低下
  → probe windowが継続
```

現行 main でも `CefZeroFramePacer.cs:59-64,89-105` と `CefUnityBrowserSample.cs:593-602` に同じ設計が残る。対照となる `CefUnity.Viewer/CefFrameSource.cs:29-60` はノンブロッキング受信であり、この busy-wait を持たない。

### 5.2 固定版 damage-streak の既知の発振

**確度: 高。既知バグ。ただし単独では 1 fps を説明しない。**

固定版 `server.rs@64f9a5f:1591-1613` は、前回 BeginFrame 以降に paint が 1 回でもあったかだけで streak を更新し、3 回連続で flush を抑止する。

commit `a08f585` の本文には、旧版で抑止が定着せず、paint が `1,1,1,0` の 4 frame 周期で欠落したことが実測として記録されている。moorestech 固定版 `64f9a5f` はこの修正より古い。

このバグは規則的な 25% 欠落やガタつきを説明するが、単独で 1 fps まで落ちる原因ではない。ほかの負荷・待機・キュー遅延と組み合わさる増幅要因と考える。

### 5.3 BeginFrame IPC の burst と pump 順序

**確度: 中〜高。コード上成立するが、低下中のキュー長は未計測。**

- `client/src/lib.rs:1523-1542`: Unity frame ごとに BeginFrame command を fire-and-forget 送信する。
- `client/src/lib.rs:236-241`: 「no wait」は応答を待たないという意味で、`ipc-channel.send` 自体は同期呼び出しである。
- `server/src/main.rs:115-126`: IPC bridge が command を無制限 mpsc へ転送する。
- `event_loop/macos.rs:162-190`: timer tick はキューが空になるまで全 command を drain する。
- `event_loop/macos.rs:154-159`: 全 command を処理した後に CEF pump は 1 回だけ行う。
- `server.rs:1670-1677`: command ごとに単一の `pending_flush` slot を上書きする。

server が遅延すると、複数 Unity frame 分の BeginFrame を連続して CEF へ送り、その後で CEF を 1 回だけ pump する。CEF 内部の pending BeginFrame guard による drop、flush slot の上書き、command latency の増大が連鎖する可能性がある。

CEF の `RenderWidgetHostViewOSR::SendExternalBeginFrame` は `begin_frame_pending_` が true の間、新しい request を処理せず return し、frame completion で pending を解除する。したがって one-in-flight 制約下へ最大 3 request/Unity frame を送る設計は、負荷が上がるほど空振りと wrapper 内処理を増やす。

- <https://github.com/chromiumembedded/cef/blob/master/libcef/browser/osr/render_widget_host_view_osr.cc>

CEF 公式 cefclient、cef-unity の standalone Viewer、Harness は、いずれも周期ごとに 1 BeginFrame を送り、ノンブロッキングに受信する成功経路である。

- <https://github.com/chromiumembedded/cef/blob/master/tests/cefclient/browser/osr_render_handler_win.cc>
- `cef-unity-csharp/CefUnity.Viewer/CefFrameSource.cs:33-60`
- `cef-unity-csharp/CefUnity.Harness/Program.cs:11-23`

### 5.4 最大 3 BeginFrame と全画面同期コピー

**確度: 高い増幅要因。単独寄与率は未計測。**

`server.rs:840-869,1666-1724` は、Unity の BeginFrame に加え、+3 ms と +6 ms に最大 2 回の内部 flush を発行する。

`on_accelerated_paint` は `server.rs:282-348` で dirty rect を受け取るが使用せず、paint ごとに `iosurface_pool_copy_and_get` を呼ぶ。`iosurface_pool.m:164-176` は viewport の `width × height` 全体を Metal blit し、`waitUntilCompleted` で同期する。

したがって、進捗バーの数ピクセルだけが変化しても全画面コピーとなる。flush が paint を増やせば、同じ Unity frame 内にこの同期コピーと Mach transfer が複数回起きる。

さらに `mach_iosurface.c:141-148` の send は最大 10 ms 待機できる。一方 client は `metal_texture.m:114-147` で溜まった message を drain し、最新以外を破棄する。中間 frame に費やした GPU copy と port transfer は画面へ使われない。

### 5.5 1000 Hz message pump

**確度: 高い増幅要因。ただし単独主因ではない。**

`event_loop/macos.rs:207-215` は 1 ms の repeating `CFRunLoopTimer` を作り、`event_loop/macos.rs:154-159` で毎 tick `CefDoMessageLoopWork()` を呼ぶ。

同時に `server.rs:956-960` は `external_message_pump=1` を設定し、CEF の `OnScheduleMessagePumpWork(delay)` も受け取る。つまり CEF 要求駆動のスケジュールと固定 1000 Hz fallback が併存する。

CEF 公式ドキュメントは、`CefDoMessageLoopWork()` について性能と過剰 CPU のバランスに注意し、`OnScheduleMessagePumpWork()` による scheduling を推奨している。

- <https://cef-builds.spotifycdn.com/docs/145.0/cef__app_8h.html>
- <https://cef-builds.spotifycdn.com/docs/112.3/classCefBrowserProcessHandler.html>
- <https://cef-builds.spotifycdn.com/docs/107.1/structcef__settings__t.html>

CEF 公式 sample は requested delay に合わせて既存予約を取り消し、one-shot timer を設定する。常時 1 ms の repeating timer ではない。

- <https://github.com/chromiumembedded/cef/blob/master/tests/shared/browser/main_message_loop_external_pump.cc>
- <https://github.com/chromiumembedded/cef/blob/master/tests/shared/browser/main_message_loop_external_pump_mac.mm>

ただし、Play 再起動直後は同じ 1000 Hz 実装でも server 約 3%だった。1000 Hz pump は病的状態で CEF work が増えた時に 1 core を占有し続ける増幅器であり、空の状態での単独主因ではない。

### 5.6 native resource と session lifetime

**確度: 中。今回の再起動回復と整合する。**

- `client/src/metal_texture.m:44-64`: connect ごとに新しい Mach receive port を allocate する。
- 同ファイルに対応する明確な disconnect/deallocate 経路がない。
- `client/src/metal_texture.m:162-218`: IOSurface/Metal texture cache が static に保持される。
- `client/src/lib.rs:412-439`: shutdown は connection と atomic state を戻すが、Mach port と native surface cache を破棄しない。
- `cef-unity/docs/REFACTORING_REPORT.md:16`: 5 時間以上稼働した Editor で CEF が 20〜30 fps へ劣化する既知の観測を記載。
- 同 `:219-223`: receive port leak と前 session の IOSurface cache を測定ノイズ源として記載。

今回も長時間稼働 session で高 CPU、一度 Play を再起動すると同じ URL・同じ設定で低 CPUへ戻った。再起動は CEF server process と page の両方を再生成するため、native leak、CEF queue、Web page lifecycle のどれが支配的かは追加計測が必要である。

## 6. CEF・Chromium・cef-unity の責任境界

| 層 | 判定 | 根拠 |
|---|---|---|
| Chromium/Blink | 主因の可能性は低い | rAF は host が与える External BeginFrame と CEF pump に従って動いている。Chrome/Chromium単体の同症状は未確認 |
| CEF | API仕様・制約は関係するが、現時点でCEFバグの証拠なし | External BeginFrame は one-in-flight。accelerated paint の共有handleはcallback中にopen/copyする必要がある |
| cef-unity Rust wrapper | 主因・増幅要因 | 追加flush、1000 Hz fallback、無制限drain、全画面同期copy、Mach送信、resource lifetime |
| cef-unity C# wrapper | 主因・増幅要因 | Unityメインスレッドbusy-wait、rAF-onlyアニメーションをidle扱いできない状態機械 |
| moorestech Web UI | 症状の増幅要因 | rAF内に進行ロジックがあり、delta 0.25秒capで低fps時に実時間より遅くなる |
| PC負荷 | 発火・悪化条件 | CPU idleがほぼない時に競合が顕在化。ただしUnity 60fps中にもCEFだけ低下 |

CEF 公式では `SendExternalBeginFrame()` は「Chromium に BeginFrame を要求する」APIとして定義されているだけで、1 Unity frame に 3 回送ることや、ホスト側で paint を待って busy-spin することは要求されていない。

- <https://cef-builds.spotifycdn.com/docs/146.0/classCefBrowserHost.html>
- <https://cef-builds.spotifycdn.com/docs/116.0/structcef__window__info__t.html>

また CEF の windowless frame rate は「最大値」であり、実際の fps は生成能力により下がり得る。

- <https://cef-builds.spotifycdn.com/docs/115.2/classCefBrowserHost.html>

CEF の accelerated paint handle が frame ごとに変わり、callback の外で保持・利用できないという制約はコピー境界を必要にする。ただし、毎回の全画面同期 copy、callback 内の Mach send、1 Unity frame に最大 3 BeginFrame という組み合わせは cef-unity 独自である。

- <https://cef-builds.spotifycdn.com/docs/145.0/classCefRenderHandler.html>

## 7. 棄却または優先度を下げた仮説

### Unity 自体が 1 fps

棄却。低下中も Unity は約 58〜60 fps で、C# は Unity frame ごとに BeginFrame を送っていた。

### 背景タブ throttling

棄却。`document.visibilityState=visible`、`document.hidden=false`、`document.hasFocus()=true` を確認した。

### 直近の SVG clip rect 変更が単独主因

優先度低。実際の SVG だけを動かした測定は約 17〜18 fps で、性能要因ではあるが一桁 fps や長時間劣化を単独では説明しない。

### Unity texture 受信だけの drop

主因として棄却。Unityへ適用された frame だけでなく、JavaScript rAF と CEF accelerated frame id 自体が低下していた。

### Renderer process が 2 個あること

優先度低。観測時に一方はほぼ 0% CPUであり、2 process が同じ page を二重描画している証拠はない。

### 孤児 cef-unity-server の大量蓄積

今回の観測では該当しない。高CPU server は対象 Unity Editor の子 process だった。

## 8. 次に必要な決定的計測

修正前に、低下開始前から低下後まで同一時間軸で次を記録する。

1. Web UI: 1 秒ごとの rAF 回数、最大 callback 間隔、craft delta。
2. C#: busy-wait 突入回数、spin loop 回数、block 合計時間、終了理由。
3. Rust client: `ipc-channel.send` 所要時間。
4. Rust server IPC: tick ごとの drain 件数、最古/最新 `unity_frame`、command age。
5. Rust BeginFrame: Unity BF、+3 ms flush、+6 ms flush の各発行数。
6. CEF paint: 1 Unity frame あたり paint 数、dirty rect 面積、全 surface 面積。
7. Metal/Mach: `waitUntilCompleted` 所要時間、Mach send 所要時間、timeout数、client drain件数。
8. message pump: callback回数、`CefDoMessageLoopWork` の1回・1秒累積時間、CEF requested delay。
9. lifecycle: Play session ID、Mach port数、IOSurface cache数、CEF process uptime。
10. process: Unity、server、Renderer、GPU process の CPU、memory、thread sample。

最低限の比較行列は次のとおり。

| 条件 | 比較値 |
|---|---|
| `_zeroFrameWaitMs=10` / `0` | rAF、paint、Unity CPU、spin時間 |
| server flush ON / OFF | BF数、paint数、Renderer/server CPU |
| 固定版 `64f9a5f` / `a08f585` 以降 | `1,1,1,0` 欠落、streak遷移 |
| resolution scale 1.0 / 0.5 | Metal copy時間、paint rate |
| clean start / 1h / 5h / Play反復 | CPU、port、cache、command age |
| Unity sample / standalone Viewer | busy-wait有無による差 |

server flush ON/OFF は server process 起動時にしか変えられない。再起動自体が病的状態を解消するため、単純な途中切り替えでは比較できない。両条件を同じ page、同じ経過時間、同じ deterministic animation で別 session として測定する必要がある。

## 9. 修正方向

この項目は実装前の候補であり、まだコード変更は行っていない。

1. Unity メインスレッドの paint busy-wait を廃止し、Viewer と同じノンブロッキング受信へ寄せる。
2. `requestAnimationFrame` のような入力を伴わない継続 damage を、スクロールと同等に「連続描画」と判定できる状態へ変更する。
3. BeginFrame IPC を最新 1 件へ coalesce し、古い frame を CEF へ burst 送信しない。
4. IPC drain に件数または時間 budget を設け、CEF pump と交互に処理する。
5. 1000 Hz fixed fallback をやめ、`OnScheduleMessagePumpWork(delay)` を基本とし、pending flush の期限だけを明示的に wake する。
6. 継続アニメーション中は 1 Unity frame あたり 1 BeginFrame を上限とし、+3/+6 ms flush を停止する。
7. dirty rect または compositor surface の所有権設計を見直し、全画面同期 `waitUntilCompleted` を paint ごとに行わない。
8. Mach receive port、IOSurface、Metal texture cache に明示的な session shutdown/reset を追加する。
9. moorestech の固定リビジョンを少なくとも `a08f585` 以降へ更新する。ただし、これは既知の4 frame発振の修正であり、本件全体の修正ではない。
10. Web UI のクラフト進行時間を rAF callback 回数から切り離し、低 fps でも wall-clock 経過を正しく消費する。

## 10. 現時点の優先順位

1. C# busy-wait の計測と廃止可否の検証
2. 長時間 session の IPC command age・pump時間・native resource数の記録
3. BeginFrame coalescing と server flush の同条件A/B
4. 全画面同期 Metal copy の時間・回数計測
5. `64f9a5f` と `a08f585` 以降の比較
6. Web UI craft timing の rAF 非依存化

最短の暫定回避は `_zeroFrameWaitMs=0` と Play 再起動である。ただし、前者の実測改善は約 7 → 11 fps に留まり、後者は蓄積状態をリセットするだけで再発を防がない。恒久対応には wrapper の待機・キュー・pump・resource lifetime を一体で直す必要がある。
