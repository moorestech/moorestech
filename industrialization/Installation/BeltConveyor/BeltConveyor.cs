using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using industrialization.Item;
using industrialization.Util;
using NUnit.Framework;

namespace industrialization.Installation.BeltConveyor
{
    
    /*
     ベルトコンベアの仕様
     ベルトコンベアは3つのアイテムを載せられる
     それぞれ0%、25%、50%、75%、100%にアイテムが差し掛かった時点で更新処理をかけて、次のスロットに進めるかどうか判定する
     */
    
    public class BeltConveyor : InstallationBase, IInstallationInventory, IBeltConveyor
    {
        //TODO _beltConveyorSpeed変数は仮なので、recipeコンフィグが出来たら消す
        private double _beltConveyorSpeed = 300;
        private IInstallationInventory connect;
        private List<BeltConveyorItems> _beltConveyorItems;

        public List<IItemStack> TeestBeltConveyorItems
        {
            get
            {
                var a = new List<IItemStack>();
                foreach (var item in _beltConveyorItems)
                {
                    if (item == null)
                    {
                        a.Add(new NullItemStack());
                    }
                    else
                    {
                        a.Add(new ItemStack(item.Id,1));
                    }
                }

                return a;
            }
        }
            
        private const int _0percentIndex = 0;
        private const int _25percentIndex = 1;
        private const int _50percentIndex = 2;
        private const int _75percentIndex = 3;
        
        public BeltConveyor(int installationId, Guid guid,IInstallationInventory connect) : base(installationId, guid)
        {
            GUID = guid;
            InstallationID = installationId;
            this.connect = connect;
            _beltConveyorItems = new List<BeltConveyorItems>();
            for (int i = 0; i < 4; i++)
            {
                _beltConveyorItems.Add(null);
            }
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            //受け取ったitemStackから1個だけとって返す
            if (_beltConveyorItems[_0percentIndex] == null)
            {
                _beltConveyorItems[_0percentIndex] = new BeltConveyorItems(itemStack.Id,(int)_beltConveyorSpeed,_0percentIndex,UpdateItem);
                return itemStack.SubItem(1);
            }
            //もしアイテムに空きが無ければそのまま返す
            return itemStack;
        }

        //アイテムが25%、50%、75%、100%に到達したとき呼び出される
        private void UpdateItem(int index)
        {
            if (index == _75percentIndex)
            {
                //アイテムを隣接する設置物に入れたとき、アイテムの搬入に成功したら75%インデックスのアイテムを消して詰める
                if (connect.InsertItem(new ItemStack(_beltConveyorItems[index].Id, 1)).Id == NullItemStack.NullItemId)
                {
                    _beltConveyorItems[index] = null;
                    _beltConveyorItems = MoveItems(_beltConveyorItems, index);
                }
                return;
            }else
            {
                //普通に右詰めする
                _beltConveyorItems = MoveItems(_beltConveyorItems, index+1);
            }
        }

        private static List<BeltConveyorItems> MoveItems(List<BeltConveyorItems> beltConveyorItems,int startMoveIndex)
        {
            //リストを昇順にループし、アイテムを入れ替える
            for (int i = startMoveIndex; i > 0; i--)
            {
                if (beltConveyorItems[i] == null && beltConveyorItems[i-1] != null)
                {
                    beltConveyorItems[i]  = beltConveyorItems[i-1].NewBeltConveyorItems();
                    beltConveyorItems[i - 1] = null;
                }
            }
            return beltConveyorItems;
        }
        
        public BeltConveyorState GetState()
        {
            throw new NotImplementedException();
        }
        
        
    }

    class BeltConveyorItems
    {
        public int Id { get; }
        private long _inputTime;
        private long _outputTime;
        private int _index;
        private int _beltConveyorSpeed;
        
        public delegate void Arrival(int index);
        private event Arrival ArrivalEvent;

        public BeltConveyorItems(int itemId, int beltConveyorSpeed,int index,Arrival arrivalEvent)
        {
            Id = itemId;
            _inputTime = UnixTime.GetNowUnixTime();
            _outputTime = UnixTime.GetNowUnixTime()+beltConveyorSpeed;
            ArrivalEvent = arrivalEvent;
            _index = index;
            _beltConveyorSpeed = beltConveyorSpeed;

            Task.Run(ExecuteEvent);
        }

        private void ExecuteEvent()
        {
            
            Thread.Sleep(_beltConveyorSpeed);
            ArrivalEvent(_index);
        }

        public BeltConveyorItems NewBeltConveyorItems()
        {
            return new BeltConveyorItems(Id, _beltConveyorSpeed,_index+1 ,ArrivalEvent);
        }

    }
}