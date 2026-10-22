# windows-desktop で 5〜6fps しか出ない件の調査ログ（2026-10-22）

## 結論
- メインスレッドの律速は**サーバーから届くイベントパケットの受信処理**（`PacketExchangeManager.DispatchReceivedPacketsLoop`）。PlayerLoop 141.7ms/フレームのうち 110.3ms（78%）。
- 受信処理の中身の 61%（67.5ms）は **MessagePack のデシリアライズ**。パイプのイベント1件で同じデータを4段階に分けて読んでいる。
- イベントの 92% は**液体パイプの `va:event:changeBlockState`**。`FluidPipeStateDetail.Amount`（float）が毎 tick わずかに変わるため、サーバー側の「同じペイロードなら送らない」差分スキップが効かず、全パイプが毎 tick 配信されている。
- 2つ目の原因：`WorldDataHandler.UpdateWorldData` が 100ms ごと（`NetworkConst.SecondUpdateRate = 10`）に**ワールドの全ブロックを `RequestWorldDataProtocol` で取り直している**。応答1回の処理が約30ms（最大115ms）で、約2.5フレームに1回届く。
- GPU は待ちが中心で、律速ではない。

グラフ：[frame-breakdown.html](frame-breakdown.html)（ブラウザで開く）

## 計測環境
| 項目 | 値 |
|---|---|
| マシン | windows-desktop（DESKTOP-PBBLSLS）／i7-12700／RTX 3060 Ti／4K出力 |
| 実行形態 | Unity Editor 6000.3.8f1 の PlayMode、「ローカルでプレイ」 |
| コード最適化 | **Debug**（`CompilationPipeline.codeOptimization`）。C# が最適化なしで実行されるので、ms の絶対値はビルド版より大きく出る |
| コミット | master 6dba07b60 ＋ ProfilerMarker 追加（ブランチ `perf/event-dispatch-profiler-markers`、09b546dc98） |
| ワールド規模 | Renderer 約21.7万／GameObject 約31.5万／状態イベントを出すブロック 1,745個／イベント購読タグ 15,114個 |
| 品質設定 | URP High／vSync 1／targetFrameRate 60／Game ビュー 1920x1080 |

## 計測の流れ
1. 前回の Editor.log を確認した。15:11 の Editor 終了は、Profiler の記録データがメモリ不足になったクラッシュだった（`profiling::ThreadDeserializer` で OutOfMemory）。ワールドが大きいので、Profiler を長く回すと再発する。
2. Profiler を数秒記録してマーカー別に集計した（`snippets/profiler/start.cs` → `analyze.cs`）。`UniTaskLoopRunnerYieldUpdate` が 75〜85ms/フレームで、中身は名前の付かない `UpdateFunction.Invoke` の self 時間だった。
3. フレームの合間に UniTask の Update キューを覗いた（`snippets/event-hooks/queue.cs`）。毎フレーム居たのは `PacketExchangeManager.DispatchReceivedPacketsLoop` だけだった。
4. イベント購読の Subject をラップし、タグ別の件数・時間・バイト数を集計した（`hook.cs` → `report.cs`）。`changeBlockState` が 1,745 ブロックから毎秒約17,000件、約500KB/s 届いていた。
5. 状態キーごとに、値が実際に変わった回数を数えた（`keys.cs` → `keysreport.cs`）。約5秒間の結果は下表のとおり。

   | 状態キー | イベント数 | 値が変わった回数 |
   |---|---:|---:|
   | FluidPipeStateData | 84,285 | 84,285 |
   | CommonMachine | 11,920 | 11,132 |
   | MachineBlock | 10,860 | 10,444 |
   | PowerGenerator | 2,865 | 2,862 |
   | GearStateData | 7,434 | 1,589 |

6. ProfilerMarker を入れて取り直した（`markers.cs`）。マーカーの位置は下の「入れたマーカー」を参照。
7. `RouteResponse` が数フレームおきに約30ms かかっていたので、応答待ちの一覧から型を特定した（`waiters.cs`、`resp.cs`）。`RequestWorldDataProtocol.ResponseWorldDataMessagePack` だった。
8. マーカー自体の負荷を計測した（`bench.cs`）。

## 結果（ProfilerMarker の実測、定常状態24フレームの平均）
### メインスレッド全体（PlayerLoop 141.7ms）
| 区分 | ms/フレーム | 比率 |
|---|---:|---:|
| 受信パケット処理 | 110.3 | 78% |
| 描画（カメラ） | 10.8 | 8% |
| `GearStateChangeProcessor.Update`（1,070個） | 2.7 | 2% |
| その他（物理・SkinnedMesh・Renderer 更新など） | 17.9 | 13% |
| （参考）EditorLoop：PlayerLoop の外、Editor だけの負荷 | 13.8 | — |

### 受信パケット処理 110.3ms の内訳
| マーカー | 内容 | ms/フレーム | 回数/フレーム | 1件あたり |
|---|---|---:|---:|---:|
| `BlockState.Deserialize` | `BlockStateMessagePack`（`Dictionary<string, byte[]>` と座標） | 27.3 | 3,292 | 8.3µs |
| `PacketExchange.Deserialize.EventStream` | パケット全体の2回目の読み込み（タグ文字列と payload のコピー） | 18.0 | 3,296 | 5.5µs |
| `PacketExchange.Deserialize.ProtocolBase` | 種類判定のための1回目の読み込み | 15.8 | 3,296 | 4.8µs |
| `PacketExchange.RouteResponse` | ワールドデータ定期取得の応答処理（デシリアライズ・`BlockInfo` 生成・全ブロックの `PlaceBlock`） | 15.5 | 約0.4 | 約30ms/回 |
| `PacketExchange.RouteEvent` の self | Subject の連鎖・タグ辞書の引き・例外の隔離 | 10.5 | 3,296 | 3.2µs |
| `BlockState.FluidPipe.ApplyWaterLevel` | 水位の Transform 更新と `Material.SetFloat` | 7.5 | 2,751 | 2.7µs |
| `BlockState.FluidPipe.GetStateDetail` | `FluidPipeStateDetail` の再デシリアライズ | 6.4 | 3,016 | 2.1µs |
| `BlockState.Processor.ElectricWireStateChangeProcessor` | 電線 | 3.1 | 326 | 9.6µs |
| その他のプロセッサ | 機械・歯車・ベルト・蒸気エンジン | 3.5 | 約1,360 | — |
| `PacketExchange.DispatchReceivedPackets` の self | 受信ループ自体 | 1.7 | 1 | — |
| パイプのプロセッサの self | パイプのその他 | 1.0 | 3,016 | — |

### 観点別の合計
| 観点 | ms/フレーム | 受信処理比 |
|---|---:|---:|
| デシリアライズ（4か所の合計） | 67.5 | 61% |
| ワールドデータ定期取得 | 15.5 | 14% |
| プロセッサ本体 | 15.1 | 14% |
| イベント配送 | 10.5 | 10% |
| 受信ループ自体 | 1.7 | 2% |

### 推測を含む値
- **液体パイプの寄与は受信処理の約73%（約80ms/フレーム）**。件数に比例する費用（外側デシリアライズ・配送・BlockState デシリアライズの計 71.6ms）にパイプの件数比 92% を掛け、パイプのプロセッサ費用 14.9ms を足した値。PlayerLoop 全体では約57%。
- デシリアライズの中身（文字列・辞書・byte[] の確保が主因）はコードを読んだうえでの推測で、計測はしていない。
- Debug 最適化の Editor なので、ビルド版（Release・IL2CPP）では ms の絶対値が数倍小さい可能性がある（推測）。比率の構造は変わらないと見ている。

## 入れたマーカー（ブランチ `perf/event-dispatch-profiler-markers`）
| マーカー名 | 場所 |
|---|---|
| `PacketExchange.DispatchReceivedPackets` | `PacketExchangeManager` の受信ループ1回分 |
| `PacketExchange.Deserialize.ProtocolBase` / `.EventStream` | `PacketExchangeManager.DispatchPacket` の2回のデシリアライズ |
| `PacketExchange.RouteEvent` / `.RouteResponse` | イベント配送／応答の配送（await の継続は同期で走るので、応答の後処理も含む） |
| `BlockState.Deserialize` | `BlockGameObject` の状態購読ラムダ |
| `BlockState.Processor.<型名>` | 各 `IBlockStateChangeProcessor.OnChangeState` |
| `BlockState.FluidPipe.GetStateDetail` / `.ApplyWaterLevel` | `FluidPipeStateChangeProcessor` |

マーカー自体の負荷も測った。1組（Begin/End）あたり約50ns（Profiler の記録オン・オフとも同程度）。今回の件数だと約1.3ms/フレーム（PlayerLoop の約0.9%）で、前後の計測の誤差に埋もれる。リリースビルドでは中身が空になる。ただし `ForProcessor` の辞書引き（約0.14ms/フレーム、推測）は残る。

## 改善の候補（未着手・要 grill）
1. **パイプの状態配送を間引く**：サーバー側での量子化、配送周期の引き下げ、描画に効く変化だけを送る、など。件数が減れば受信処理の大半が比例して減る。
2. **デシリアライズの回数を減らす**：外側の2回を1回にまとめる（ProtocolBase の読み込みは丸ごと重複）。状態をタグ別の型で直接送り、`Dictionary<string, byte[]>` 経由の二重デシリアライズをやめる。
3. **ワールドデータの定期全量取得をやめる**：設置・撤去イベントで同期できているなら不要な可能性がある。コードには `//TODO 本当に0.5秒に1回でいいのか？` が残っているが、実際は 0.1 秒間隔。
4. **座標入りのタグ文字列での購読振り分けをやめる**：配送の self 10.5ms には、約40文字のタグのハッシュ計算が毎件含まれる。

## まだ分解できていない部分
- ワールドデータ定期取得 1回約30ms の内訳（デシリアライズ／`ParseWorldResponse`／`ApplyWorldData`）。
- サーバースレッド側の負荷（`ChangeBlockStateEventPacket.ChangeState` のシリアライズと `SequenceEqual`、配送用のタグ生成、送信時のシリアライズ）。今回はメインスレッドしか集計していない。
- GC.Alloc（1フレーム約7.8万回）の発生元。Profiler の「Call Stacks → GC.Alloc」で見るのが確実。

## 再計測の手順
1. Windows 側で `G:\GitHub\moorestech` を対象ブランチにし、`uloop compile`。
2. Editor は SSH の `Start-Process` だとセッション終了とともに消えるので、スケジュールタスク（`New-ScheduledTaskAction` ＋ `LogonType Interactive`）で起動する。`uloop launch` は Editor が `G:\UnityEditor` にあるため見つけられない。
3. `uloop control-play-mode --action Play` → `snippets/setup/skip.cs`（前回クラッシュ報告の「送らない」）→ `startlocal.cs`。
4. ワールドの読み込み完了を待つ。`env.cs` の Renderer 数が約21.7万で止まれば定常。読み込み中は初期同期のバースト（1フレーム約1万件）が混ざる。
5. `snippets/profiler/start.cs` → 数秒待つ → `markers.cs`。Profiler の記録は数秒に留める（長いとメモリ不足でクラッシュする）。
6. スニペットは `uloop execute-dynamic-code --code-file <path>` で実行する。`event-hooks/` のフックは、対になる report 系のスクリプトで必ず元に戻す。
