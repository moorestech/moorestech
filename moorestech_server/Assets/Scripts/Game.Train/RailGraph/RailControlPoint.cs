using UnityEngine;
/// <summary>
/// ベジェ曲線の制御点1つと自身の座標を1つ持つクラス
/// ベジェ曲線の計算には4つ点が必要だが、このクラスではそのうち2つを持つ
/// 
/// 前提：RailNodeが2つあると曲線が計算できる
/// 1つのRailNodeは進行方向と逆方向に伸びているので、進行方向用に1つ、逆方向用に1つのRailControlPointをもつ
/// これで"RailNode1の進行方向"と"RailNode2の逆方向"のRailControlPointから4つの点を取得できる
/// →曲線がもとまる
/// 
/// また別のメリットとして、表裏で重なっているRailNodeは同じRailControlPointを持つことが考えられる
/// 無駄に座標情報の実体を持たせる必要がなくなる
/// </summary>

namespace Game.Train.RailGraph
{
    public class RailControlPoint
    {
        //OriginalPositionはRailComponentが存在するOriginalPosと同じと考えて良い。floatだと16777216以降の座標でバグるので絶対int
        public Vector3 OriginalPosition { get; }
        //一方制御点はintだと表現できないのでfloat
        //制御点の座標はOriginalPositionが0,0,0のときの相対座標
        public Vector3 ControlPointPosition { get; }
        public RailControlPoint(Vector3 originalPosition, Vector3 controlPointPosition)
        {
            OriginalPosition = originalPosition;
            ControlPointPosition = controlPointPosition;
            if (ControlPointPosition.sqrMagnitude < 1e-6)
            {
                ControlPointPosition = new Vector3(0, 1f, 0);
            }
            else
            {
                ControlPointPosition.Normalize();                
            }
        }
    }
}