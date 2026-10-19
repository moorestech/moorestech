using UnityEngine;

namespace Client.Game.InGame.BeltTransport.Cover
{
    // 合流マスの位置に覆いを作る。ベルコンの BlockGameObject がまだ無い、または分岐器が引けないときは false
    // Creates a cover at a merge cell; false while the belt's BlockGameObject does not exist yet or no splitter resolves
    public interface IBeltMergeCellCoverFactory
    {
        bool TryCreate(Vector3Int position, out IBeltMergeCellCover cover);
    }
}
