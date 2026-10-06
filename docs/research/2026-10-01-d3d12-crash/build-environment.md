# ビルド・環境の一致範囲

## 固定したソース

| 対象 | commit |
|---|---|
| 元報告の9/26版 | `9cec17ddcb88f2ae798352ddf876370876589ef0` |
| 主な検証の9/29版 | `24226d1ac30574f2c74e75cc179f6be4d6884d54` |

manifestのdirty状態と未収録差分を考慮する。commitが分かること、主要DLL/PDBが一致すること、全配布ビルドが一致することは別の主張である。調査時に現在のmasterを読んで原挙動としない。

## 描画資産とESCの差

追跡対象の描画・シリアライズ資産1241件では `MainMenu.unity` だけが変更された。MainGame、主要ゲーム／プレイヤー／UI prefab、URP設定、ProjectSettingsは同じGit blob/tree。主要32ファイルhashと14 tree IDも照合した。外部import結果や未収録dirty差分まで保証した比較ではない。

ESC時の挙動は完全一致しない。

- 9/29版は `UIStateControl.EnterState → ApplyMovementLock` を `Pause.OnEnter` の前に行う。
- `StarterAssetsInputs` がmove/look/jump/sprintを解除する。
- `ThirdPersonController.Move` の実行時に、ロック中の水平速度を0にする。setterを呼んだ瞬間に全速度が消えるという意味ではない。
- 9/26版にはこのPause移動ロックがない。
- 調べた `GameScreen.OnExit` のカメラ停止と撮影／RT／readbackの呼出し経路は一致する。

元の最終12件の位置通信（tick43761〜43784）はpayloadが同一で、最後のsnapshotも同じfloat32位置だった。直前の継続移動を支持しないが、キー状態、速度、保留ジャンプ、カメラ姿勢、最後の位置報告以後の描画frameは未記録なので完全除外はしない。公開文書にはプレイヤー位置の生値を転載しない。

新しい版で移動キーを保持してESCするだけでは、その版の移動ロックが先に働くため元挙動の対照にはならない。147件の変更runtime C#の追加・削除行を描画API名で走査した陰性も、間接的な描画影響が一切ない証明にはならない。

## ffmpeg起動差は説明にならなかった

固定commit間で `FfmpegProcess`、`GameFrameRecorder`、`FrameBufferPool` は全ファイル一致。WindowsPlayerの探索優先順位、同梱先、stdio、起動終了処理の変更は確認されなかった。

`ProcessSessionScope` の差は同じ `session_` 文字列の定数化で、Windows Job管理ではない。環境変数sanitizeの配置変更はnamespace以外同じで、旧・新ともPlayer対象assemblyの同じ初期化属性で実行される。検証版だけに新たな安定化処理が入ったと説明しない。

非公開assetの固定pin間の差も `.gitattributes` とmacOS arm64版ffmpeg関連の6パスだけで、Windows描画assetの変更は確認されなかった。両build logにはffmpeg同梱成功があるが、元PCで解決された実行ファイルのhash・起動引数・子PID/start/exitの実測は別途欠ける。

## 全ビルドの所在

元build runに残る7ファイルはpromotion、build log、verify結果、Steam設定等で、`build/` と `steam/output/` は残っていなかった。既知のSteam depot manifestも元配布版との対応が未証明のため、それを取得すれば元buildだと扱わなかった。

これは確認した保存先での欠落であり、世界中のすべての保存先に元buildが存在しないという主張ではない。元sourceの再buildも、当時のimport・依存物・dirty差分・設定を再現できなければ元配布物そのものにはならない。

## ハードウェアとOS

| 項目 | 元実行 | 検証機 |
|---|---|---|
| GPU | RTX 4080 Laptop / 12GB | RTX 2060 / 6GB |
| NVIDIA driver file version | 32.0.16.1692 | 32.0.16.1074 |
| Windows build | 28020系列（dump SystemInfo） | 26200系列（CIM記録） |

元OS DLLのfile versionは `10.0.28000.2623`。SystemInfoの28020とDLL版の28000は異なるフィールドであり、後者を検証機のOS版と取り違えない。検証機のsystem-only WARP試験ではOS DLL `10.0.26100.9278` をロードした。検証機側もCIMのOS版とDLLのfile versionを別フィールドとして扱う。ゲームのD3D12Coreを一致させた試験でも、OS側DXGI／D3D11on12／driver stack全体が一致するわけではない。

環境差は再現試験の適用範囲を制限するが、その差自体を原因としない。driver版やOS版を合わせれば直るという結論も出していない。

Steam／NVIDIA関連のoverlay DLLは元と以前の検証dumpの両方にあり、file version差を確認した。ただしloaded moduleの存在はoverlayの設定・実動作を表さない。Zは完全なnative module一覧を採っていないため、Zにも同じmodule構成があったと補完しない。

最新の非公開logs更新に別報告者・9/29版の長時間progressが1件あった。Pause7件と正常終了を記録するが、元と同じ故障状態ではなくGPU／子processの診断もない。元クラッシュの陰性試験7件と数えない。

## 非公開証拠索引

- `investigation-20260929/build-cef-findings.md`
- `investigation-20260929/managed-identity/README.md`
- `investigation-20260929/cef-native-identity/README.md`
- `investigation-20260929/verifier-overlay-coverage-review.md`
- `investigation-20260929/os-build-and-dump-coverage-review.md`
- `investigation-20260929/cef-diagnostic-20260930/original-fullbuild-audit/status.md`
- `investigation-20260929/cef-diagnostic-20260930/process-launch-delta-audit/status.md`
- `investigation-20260929/cef-diagnostic-20260930/evidence-refresh-oct01/status.md`
