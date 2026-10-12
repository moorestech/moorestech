決定: GearChainPoleComponent から切り出す接続集合 GearChainConnectionSet は、読み取り用（I*Lookup）と変更用（I*Mutation）の interface に分け、外部へは読み取り面だけを渡す。
棄却案: コンポーネント内部の集合なので1クラスのまま読み書きを公開する案。
理由: ユーザー裁定 2026-10-05 選択「分ける」（writing-plans Phase 2.6 検査5-D）。
リンク: GearChainPoleComponent.cs / plan Task 1
