# 未解決事項・再開条件・調査の反省

## 残る因果の切れ目

残る候補を原因確率の順には並べられない。根拠のある機構と、元実行で観測できたことを分ける。

| 仮説 | 支持する根拠 | 原因同定に足りない証拠 |
|---|---|---|
| Fence未達復帰後の早期資源再利用 | 元の未達ログ、待機後に要求targetで回収へ進む静的経路 | 原event/list/allocatorの同一性、GPU未完了のReset、最初の記録違反 |
| CEFとUnityの同期・資源寿命 | 共有fence、allocator再利用、逆方向完了保証やcaller契約の懸念 | 元の対象resource、該当submit、Unity側失敗listとの対応 |
| 録画書込み失敗後の状態と撮影 | 原ENOMEM/BrokenPipe/Unavailable、CPU pool喪失の別試験 | 元throw site、子の退出時刻、pool/queue/readback、GPU命令への影響 |
| Screenshotと他描画の重なり | Pause直後のcapture要求、撮影からFlush等への静的経路 | 原撮影のnative状態、完了・失敗命令の直接対応 |
| GPU／driver／OS固有の動作 | 元と検証機の差、検証で同fatal未発生 | 同条件対照と最初のD3D12診断 |
| 先行commit逼迫の間接的影響 | ENOMEMと高いpeak | 失敗したallocation、時刻、所有者、その後の不正状態 |

これらは排他的とは限らない。OOMが別の欠陥を表面化させた場合も考えられるが、複数の疑いを線で結んだだけでは因果の証明にならない。

## 別件として扱える知見

- writer例外時のCPUバッファ返却漏れは、小試験で確認した独立不具合。GPUクラッシュ修正と同じ扱いにしない。
- CEFのclient側重複NT handleの寿命管理には別課題がある。元のClose失敗の証明ではない。
- 短間隔captureやworkspace世代の競合では、撮影保存失敗を別症状として扱う。
- UnityのGPU予約量ログのラベル逆転は、解析時の読み替えとして残す。9/27公開文書のGPU表にも逆転した値が転記されているため、本書の訂正を優先する。
- caller契約を満たす実装への整理や例外後のpool回復を行う場合、独立した入力・期待結果で検証する。元クラッシュ修正というPRタイトルを付けない。

## 再開するなら最初に決めること

新資料、新しい同署名の陽性条件、または未確認の保存先が得られた場合は、その入力を固定してから再開する。既存資料の再読、通常ESCの回数追加、別のUnity patchの説明文探索だけを進捗として扱わない。

一方で、実験前に元の因果関係を証明できることを要求してはならない。それでは再現探索が循環条件になる。新しい試験には、少なくとも「何を変えるか」「どの観測で仮説を分けるか」「条件不成立をどう検知するか」「陰性なら何が分かるか」を書く。未実施の組合せであることだけを採用理由にしない。

### 次の失敗で残したい記録

1. 実配布物のbuild ID、dirty差分、Unity／D3D12Core／native plugin／ffmpegのhashと依存版。
2. UTCと単調時計の対応、PIDと開始時刻、thread、描画frame、server tick。ログの集約時刻と実イベント時刻を区別する。
3. generation付きlist／allocator／resource ID、Reset／Closeの戻り値、submit順、対応するGPU完了値。
4. 最初のD3D12 validation errorと対象object。device removalがあればDRED・removal reason・breadcrumb等を回収するが、すべてのClose E_FAILにDREDが残るとは仮定しない。
5. 録画childのPID/start/exit、writer例外箇所、queueと借用枠、readback、captureの発火理由とnative撮影への対応。
6. system/process commitの同時系列と、実際に分かるJob所属。最大値の差からprocessを推定しない。
7. 収録のaccepted/written/dropped件数、flush/close完了、欠測理由。末尾がないtraceを正常終了としない。

計測はスケジューリングへ影響する。デバッグ層の有無、filter、記録頻度、drop数を試験条件として保存する。アドレスだけの一致では再利用後の別objectを混同するため、世代を含める。

## 追加資料の回収で分かったこと

元報告バンドルに最後の画像がないことは、元PCのcapture workspaceに画像がなかった証明ではない。crash回収経路と通常capture保存経路は別で、画像を拾わない経路がある。

capture workspaceは次のcaptureで掃除され得る。初期化時の古いworkspace掃除もあるため、元資料の回収を目的にゲームを再起動することは避ける。次の起動で前回ログが上書きされる可能性もある。送信済みmarkerがあることも、outbox全体が削除済みという意味ではない。

既存ファイルを回収するなら、原ログが示す保存先を優先し、一覧・metadata・hashを取り、移動ではなく別先へコピーする。収集対象・容量・読めなかった範囲を記録し、無関係なユーザーディレクトリ全体を収集しない。capture directory名やmtimeだけで最終captureと同定しない。

今回、報告者への依頼は送らないというユーザー制約がある。連絡経路が未確定であることも、新しい送信権限を意味しない。本書は収集依頼の送信指示ではない。

## 安全上・運用上の引き継ぎ

- 拒否された圧迫／Job／H2、native内部hook・回復・命令配列・doubleClose、拒否されたcompiler helperは再試行しない。別launcherや名前に置き換えない。
- 操作対象は所有が確認できた試験processに限定し、PIDと開始時刻を保存する。包括的なprocess名killを使わない。
- 試行に期限と停止条件を持たせ、通常負荷に限定する。今回の温度・電力閾値はこの検証の運用値で、他機器に一般化した安全保証ではない。
- 世界データ・DLL・pagefile・一時診断layer・監視をそれぞれ復元する。「ゲームを終了した」だけで後片付け完了としない。
- 既存の復元記録は過去の時点を示す。次回は新しい試行の前に現状を確認する。

## なぜ長期化したか

技術的な限界は、元ダンプに失敗objectの必要なheapや最初の診断履歴がなく、検証機で同じfatalを取れなかったことにある。実行環境やdirty buildの差も完全には揃えられなかった。

調査運用にも問題があった。知見の整理、静的な到達可能性の確認、既知の欠落の再確認を重ねても、原因候補を区別する観測は増えない段階に入っていた。複数agentの同意や資料数の増加を、独立した実行証拠の増加と混同しない管理が必要だった。

ユーザーの「同じところを回っている」という指摘に対し、もっと早く既存証拠の限界を明示し、最初の異常を捕まえる観測設計へ切り替えるべきだった。後半の計測でも陽性を取れなかった段階では、同じ再評価を続けず、今回のように詳細な引き継ぎを作って終了する判断が必要だった。

次回は「資料を読んだ」「可能性を発見した」「機構を別試験で確認した」「同署名を再現した」「原原因を同定した」を別の成果段階として管理する。陰性試験には成立した前提と未観測部分を必ず併記する。新しい証拠がない再レビューは原因調査の進捗に数えない。

追跡先: Beads `moorestech-rhvub.2`（元原因調査、未解決）と `moorestech-rhvub.6`（本書の作成・PR）。

## 非公開証拠索引

- `CURRENT-STATE.md`
- `investigation-20260929/causal-evidence-matrix.md`
- `investigation-20260929/fable-51-consultation/README.md`
- `investigation-20260929/original-evidence-recovery-paths.md`
- `investigation-20260929/cef-diagnostic-20260930/post-z-condition-audit/status.md`
- `investigation-20260929/cef-diagnostic-20260930/z-causal-followup/status.md`
- `investigation-20260929/cef-diagnostic-20260930/trial-z-result/README.md`
