# ChallengeListUITest は移植せず PR1 で削除する

- **日付**: 2026-09-06
- **文脈**: PR #1325 で `ChallengeManager.SetUI/UpdateUI`（uGUI への投入口）を削除した結果、`ChallengeListUITest`（`CiShardClientPlay3` shard）が数えていた `categoryListParent` の子が生えなくなり、`Expected: 1 / But was: 0` で確実に落ちる。plan の要件 R6 は「テストは破棄でなく移植する」と定めている。

## 決定

本 PR で `ChallengeListUITest` を削除し、plan の非目標へ「`ChallengeListUITest` は uGUI 観測点のため PR1 で削除」と明記する。

## 棄却した案

- **Web 側の権威へ移植する（R6 準拠案）**: `Client.WebUiHost/Game/Topics/Challenge/ChallengeTopicState.cs` の `ChallengeTreeTopic`/`ChallengeCurrentTopic` スナップショット JSON でカテゴリ数を検証する形へ書き換える。検証内容が生き残るが、移植先の前例調査に手間がかかる。

## 理由

このテストが見ていたのは uGUI の子オブジェクト数そのもの＝描画観測点であり、R6 が守ろうとしている「サーバー往復・ロジックの検証」ではない。描画主体は Web に移っており、そちら側は vitest / e2e が担う。

## リンク

- [[2026-09-05-uGUI撤去は抽出PRと削除PRの2本に分ける]]
