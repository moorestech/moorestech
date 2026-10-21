namespace Core.Update
{
    // GameUpdaterが回すサーバー1台分のtick本体。1tickの処理順はServer.BootのMasterTickUpdaterが持つ
    // The tick body of one server that GameUpdater drives; the order inside one tick lives in Server.Boot's MasterTickUpdater
    public interface IMasterTick
    {
        void Tick();

        // tick外で起きた設置・撤去を網へ反映する。本番のtickは末尾で自ら呼ぶので、tick外の変更を受けるテストの進行とロードだけが使う
        // Applies placements and removals made outside a tick to the networks; a production tick calls it itself at its end, so only test stepping and load use it from outside
        void RebuildTopologiesIfDirty();
    }
}
