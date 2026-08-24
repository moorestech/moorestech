namespace Client.Game.InGame.Map.MapObject.Pending
{
    /// <summary>
    ///     生成前に届いた破壊/HPを、スナップショット適用済みの個体へ上書き適用する
    ///     Applies destroy/HP that arrived before instantiation onto an object that already took its snapshot
    /// </summary>
    public static class MapObjectPendingStateApplier
    {
        public static void Apply(MapObjectGameObject mapObject, MapObjectPendingState pendingState)
        {
            // HPを先に当て、破壊はその後に当てる。破壊済みへの再破壊はイベント再発火になるので抑止する
            // HP lands first and destruction after it; re-destroying an already destroyed object would re-fire the event
            if (pendingState.HasHp) mapObject.UpdateHp(pendingState.Hp);
            if (pendingState.IsDestroyed && !mapObject.IsDestroyed) mapObject.DestroyMapObject();
        }
    }
}
