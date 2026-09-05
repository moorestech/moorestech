# uGUIビューの論理状態はUnity側の純ロジッククラスへ抽出する

- 日付: 2026-09-05
- 決定: UI状態主権はUnity（UIStateControl）のまま。uGUIビューが抱える論理状態（サブインベントリ選択・ビルドメニュー選択・採掘進捗・ブループリント名入力等）は Client.Game 内の uGUI 非依存モデルクラスへ抽出し、State と Web ブリッジ（Topic/Action）の両方がそれを読み書きする。恒久非表示ビューへの SetActive 呼び出しは削除。ProgressBarView.Instance 型の静的所有は DI 登録へ置換。Web 側の契約（topic/action）は不変。テスト54ファイルは新モデル型へ移植。
- 棄却案（提示したもの）: 状態主権をWeb側へ移譲しUnityはUIStateEnumのミラーだけ持つ
- 理由: ユーザー裁定 2026-09-05「Unity側の純ロジッククラスへ抽出（状態主権はUnityのまま）」。既存裁定（Web UIブリッジはUnity状態機械を購読）と整合
- 関連: [[2026-09-05-デバッグUIはuGUI現状維持]]
