using System.Collections.Generic;
using System.Linq;
using Game.Block.BlockInventory;
using Game.Block.Interface;
using Game.World.Interface;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.Block.Component.IOConnector
{
    public class InputConnectorComponent : IBlockComponent
    {
        public bool IsDestroy { get; private set; }

        private readonly List<IBlockInventory> _connectInventory = new();
        private IWorldBlockDatastore _worldBlockDatastore;
        
        private IOConnectionSetting _ioConnectionSetting;
        
        public InputConnectorComponent(IWorldBlockDatastore worldBlockDatastore,IWorldBlockUpdateEvent worldBlockUpdateEvent)
        {
            _worldBlockDatastore = worldBlockDatastore;
        }

        public void SetIOSetting(IOConnectionSetting ioConnectionSetting, Vector3Int blockPos, BlockDirection blockDirection)
        {
            var inputPoss = ConvertConnectDirection(ioConnectionSetting.InputConnector);
            
            
            
            var outputPoss = ConvertConnectDirection(ioConnectionSetting.OutputConnector);
            
            
            
            

            #region Internal

            // 接続先のブロックの接続可能な位置を取得する
            List<Vector3Int> ConvertConnectDirection(ConnectDirection[] connectDirection)
            {
                var blockPosConvertAction = blockDirection.GetCoordinateConvertAction();

                var convertedPositions =
                    connectDirection.Select(c => blockPosConvertAction(c.ToVector3Int()) + blockPos);
                return convertedPositions.ToList();
            }

            #endregion
        }
        
        
        
        public void Destroy()
        {
            _connectInventory.Clear();
            IsDestroy = true;
        }
    }
}