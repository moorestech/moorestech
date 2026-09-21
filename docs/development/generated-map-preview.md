# 生成マップを Scene View で確認する

Unity を **EditMode** にした状態で、メニューの **moorestech → Generated Map Preview** を開きます。ウィンドウを開くだけでは生成は始まりません。

1. **生成 / 再生成** を押します。現在のマスタを読み込み、通常ゲームと同じ既定 seed の生成結果を専用の一時 Stage に表示します。
2. 状態が **Ready** になったら Scene View で確認します。Terrain、草、木・岩などの配置物、鉱脈の露頭とスポーン地点を表示します。
3. **スポーン地点へ移動** で開始位置の近景へ、**全地形を表示** で全タイルの俯瞰へ移動できます。その後は Scene View の通常の操作で自由に視点を動かせます。
4. マスタを編集した後は **生成 / 再生成** を押します。前回の一時表示を破棄し、現在のマスタで作り直します。
5. **プレビューを閉じる**、または Scene View 上部の **Scenes** から元の Stage に戻ります。

Prefab 編集中は生成できません。Prefab Stage を閉じてから操作してください。

## 表示と保存

これは生成結果を見るための一時表示です。プレビュー内の配置物や Terrain を保存・書き出しする機能ではありません。ユーザーのセーブや開いていたシーンの内容を更新しません。元のシーンを開いたまま、別の一時 Stage を表示します。

ウィンドウだけを閉じても Stage は残ります。メニューからウィンドウを開き直して **プレビューを閉じる** を押すか、Scene View の **Scenes** から戻ってください。PlayMode への移行、スクリプトの再コンパイル、Editor の終了でも Stage は閉じます。

プレビューを閉じると、その回の一時ワールドと生成した GameObject / TerrainData を破棄します。通常ゲームと共有する生成キャッシュは残ることがあります。

## 生成中・失敗時

**Generating** の間は再生成やフレーミングを操作できません。**プレビューを閉じる** は操作できます。ただしワールドの同期生成中は Editor が応答を返すまで操作が処理されません。戻った後の安全な中断点で終了します。

**Failed** になった場合は、ウィンドウの状態表示・期待数／作成数／欠損数と Unity の **Console** を確認してください。Console には対象の GUID、アセットのアドレス、読み込みや配置に失敗した理由が出ます。不完全な表示は破棄されます。マスタやアセット登録を直して **生成 / 再生成** を押すと再試行できます。

## 開発時の検証

以下を対象 worktree のクライアント Editor で実行します。

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapPreview|TerrainDataAssemblerGateTest|TerrainAlphamap" --unsaved-changes fail --timeout-seconds 1500
```

実マスタの統合テストは、EditMode の公開生成入口から完了を最大 600 秒待ちます。生成した一時ワールドの map.json と全配置を比較し、全タイルの隣接参照、レイヤー順、detail 密度の全セル、材質と高さサンプルを確認します。2 回の再生成、Close 後の資源解放、専用 fixture の 0 件／1 件／欠損／復帰も検査します。画像の見た目だけを一致の根拠にはしません。
