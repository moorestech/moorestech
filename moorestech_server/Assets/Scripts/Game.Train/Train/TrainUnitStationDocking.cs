using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.RailGraph;

namespace Game.Train.Train
{
    // TrainUnit全体がドッキングしているかどうか
    public class TrainUnitStationDocking
    {
        //TrainUnitの_carsと_railPositionを受け取って列車の速度が0になったとき(手動自動とも)に列車の端がぴったり乗るnodeを検出
        //それが同じ駅の前と後ろにぴったり重なっている場合ドッキング成立とする
        //この検出はここで逐次計算で行う

        //状態について
        //・列車が駅にドッキングしてない状態
        //・列車が駅にドッキングしている状態
        //  ・この場合各carごとにドッキングしているかしてないかboolをもつ
        //  ・station側でないのは、セーブ・ロードなどなにかの操作で列車がロストした場合永久にstationがロックされるのを防ぐため
        //
        //station側ではcarがドッキングした瞬間にstationのドッキング状態を更新する
        //これでロード時最初の1フレームで必ずドッキングされた状態で始まる
        //なのでセーブ・ロードではcar側のboolのみを対象にしてstation側はセーブしない

        //他メモ
        //ドッキング中の列車は削除できる→TODO真ん中削除したらどうなるか？
        //列車が乗っているnodeは削除できない
        //
    }
}
