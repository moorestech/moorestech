> ⚠️ benchmark/results/2026-11-13T2353_before-aim-traversal_110ccc5674_r1.json: 表示待ち（GfxDeviceD3D12.WaitForLastPresentation.WaitForGPU）が記録されていない。画面オフや非表示で描画の表示を省いていた疑いがある
> ⚠️ benchmark/results/2026-11-13T2353_before-aim-traversal_110ccc5674_r2.json: 表示待ち（GfxDeviceD3D12.WaitForLastPresentation.WaitForGPU）が記録されていない。画面オフや非表示で描画の表示を省いていた疑いがある
> ⚠️ benchmark/results/2026-11-13T2353_before-aim-traversal_110ccc5674_r3.json: 表示待ち（GfxDeviceD3D12.WaitForLastPresentation.WaitForGPU）が記録されていない。画面オフや非表示で描画の表示を省いていた疑いがある
> ⚠️ benchmark/results/2026-11-14T0011_after-aim-traversal_f1f4c3fbc3_r1.json: 表示待ち（GfxDeviceD3D12.WaitForLastPresentation.WaitForGPU）が記録されていない。画面オフや非表示で描画の表示を省いていた疑いがある
> ⚠️ benchmark/results/2026-11-14T0011_after-aim-traversal_f1f4c3fbc3_r2.json: 表示待ち（GfxDeviceD3D12.WaitForLastPresentation.WaitForGPU）が記録されていない。画面オフや非表示で描画の表示を省いていた疑いがある
| 指標 | before-aim-traversal（3回）<br>110ccc5674 | after-aim-traversal（3回）<br>f1f4c3fbc3 |
|---|---:|---:|
| fps（Profiler なし） | 26.75<br><sub>26.47〜27.18</sub> | 27.24<br><sub>26.97〜27.51</sub> (+2%) |
| フレーム ms 平均（Profiler なし） | 37.4<br><sub>36.8〜37.8</sub> | 36.7<br><sub>36.4〜37.1</sub> (-2%) |
| フレーム ms p95（Profiler） | 54.4<br><sub>48.8〜58.8</sub> | 50.3<br><sub>49.4〜51.7</sub> (-8%) |
| 受信イベント 件/秒 | 59<br><sub>56〜62</sub> | 56<br><sub>55〜57</sub> (-5%) |
| 受信処理 µs/件（応答を除く） | 1,583.5<br><sub>1,519.5〜1,663.8</sub> | 1,595.9<br><sub>1,452.0〜1,881.0</sub> (+1%) |
| 受信処理 ms/フレーム（応答を除く） | 3.3<br><sub>3.0〜3.5</sub> | 3.2<br><sub>2.9〜3.8</sub> (-1%) |
| ブロック状態 件/秒 | 4,208<br><sub>4,007〜4,398</sub> | 4,030<br><sub>3,905〜4,169</sub> (-4%) |
| 受信 payload KB/秒 | 786<br><sub>750〜820</sub> | 754<br><sub>730〜779</sub> (-4%) |
| `PlayerLoop` ms | 30.7<br><sub>30.0〜31.4</sub> | 30.5<br><sub>30.0〜30.9</sub> (-1%) |
| `UniTaskLoopRunnerYieldUpdate` ms | 3.3<br><sub>3.0〜3.5</sub> | 3.2<br><sub>2.9〜3.8</sub> (-1%) |
| `PacketExchange.DispatchReceivedPackets` ms | 3.3<br><sub>3.0〜3.5</sub> | 3.2<br><sub>2.9〜3.8</sub> (-1%) |
| `PacketExchange.Deserialize.ProtocolBase` ms | — | — |
| `PacketExchange.ReadPacketHeader` ms | 0.0<br><sub>0.0〜0.0</sub> | 0.0<br><sub>0.0〜0.0</sub> (+5%) |
| `PacketExchange.Deserialize.EventStream` ms | 0.1<br><sub>0.1〜0.1</sub> | 0.1<br><sub>0.0〜0.1</sub> (-11%) |
| `PacketExchange.RouteEvent` ms | 3.2<br><sub>3.0〜3.4</sub> | 3.2<br><sub>2.8〜3.7</sub> (-1%) |
| `PacketExchange.RouteResponse` ms | — | — |
| `BlockState.Deserialize` ms | 1.2<br><sub>1.1〜1.3</sub> | 1.1<br><sub>1.1〜1.2</sub> (-9%) |
| `BlockState.FluidPipe.GetStateDetail` ms | — | — |
| `BlockState.FluidPipe.ApplyWaterLevel` ms | — | — |
| `RenderPlayModeViewCameras` ms | 10.2<br><sub>10.0〜10.5</sub> | 10.1<br><sub>9.9〜10.2</sub> (-1%) |
| `EditorLoop` ms | 9.2<br><sub>8.4〜9.6</sub> | 8.6<br><sub>8.3〜8.9</sub> (-6%) |
| `GC.Collect` ms | 0.0 | 0.4 (+1614%⚠️) |
| `GC.Alloc` 回/フレーム | 4,805<br><sub>4,756〜4,853</sub> | 4,627<br><sub>4,565〜4,732</sub> (-4%✅) |
