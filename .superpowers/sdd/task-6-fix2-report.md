# Task 6 fix2 報告
- 原因（実測）: L1 で bug-report-description の外箱=72 / 内側 TextField=72 だが、入力要素 .unity-base-text-field__input だけが 30px で止まっていた（TextField は row のため flex-grow が縦に効かない）。縮みでもトークン未解決でもない。
- 修正: BugReport.uss で入力要素に height:100% と flex-grow:1、外箱に flex-shrink:0。
- テスト: Client.Tests/Ui/Screens/BugReport/BugReportDescriptionHeightTest.cs（通常高と、枠を最大高160pxにしても欄が72px保ち本文がスクロールする場合）。Client\.Tests\.Ui\. 244件 pass。
