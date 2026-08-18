決定: `NotificationCategory`へ`ItemEarned`を追加し、`NotificationMessagePack`へ`[Key(4)] Count`を新設する。messageIdは定数`itemEarned.mined`固定。
棄却案: 既存`Achievement`カテゴリ＋`CreateAchievementWithItem`に相乗りし、個数を`MessageParams`の文字列で送る案。
理由: 相乗り案はスキーマ変更ゼロだが、「解放しました」系の達成通知と「+5」の獲得通知が同じカテゴリに同居し、Web UI側で集約対象を判別できなくなる。個数は文字列パラメータではなく数値として持つ方が加算の型が素直。
リンク: NotificationMessagePack.cs / NotificationTopic.cs（ToWebCategory） / docs/adr/0019-item-earned-notification.md
