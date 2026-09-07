# CutSceneはuGUI Canvasだけ消しTimelinePlayerは残す

- 日付: 2026-09-05
- 決定: CutSceneManager.prefab の CutSceneCanvas（BlackOut画像・InitialText・CanvasScaler・GraphicRaycaster）を除去する。TimelinePlayer・PlayableDirector・CutSceneCamera・playable 2本・GameStateController の OnPlayingChanged 購読は残す。将来カットシーンの暗転・字幕は web 側で描く前提。
- 併せて確定: Skit の SelectionButton.prefab（参照ゼロの孤児）と Client.Skit/UI/BackgroundSkitUI.cs（Webモードで描画停止中）は削除対象。既存計画の Excluded/Pending 分類は agent が引き写しただけで根拠が無かった。
- 棄却案（提示したもの）: Client.CutScene ごと全部消す／現状維持
- 理由: ユーザー裁定 2026-09-05「uGUI Canvas だけ消し、TimelinePlayer は残す」（ユーザーの問い「CutScene Skit SelectionButtonが残す枠になってるのはなぜ？」への調査回答を受けて）
- 関連: [[2026-09-05-mapObjectのHPバーはuGUI現状維持]]
