using System;
using System.Collections.Generic;
using Game.Block;

namespace Game.World.EventHandler.InventoryEvent
{
    /// <summary>
    ///     
    ///     
    /// </summary>
    public static class VanillaBlockInventoryConnectionData
    {
        public static readonly Dictionary<string, IoConnectionData> IoConnectionData = new()
        {
            {
                VanillaBlockType.Machine,
                new IoConnectionData(
                    new ConnectDirection[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) },
                    new ConnectDirection[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) },
                    new[] { VanillaBlockType.BeltConveyor })
            },
            {
                VanillaBlockType.Chest,
                new IoConnectionData(
                    new ConnectDirection[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) },
                    new ConnectDirection[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) },
                    new[] { VanillaBlockType.BeltConveyor })
            },
            {
                VanillaBlockType.Generator,
                new IoConnectionData(
                    new ConnectDirection[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) },
                    new ConnectDirection[] { },
                    new[] { VanillaBlockType.BeltConveyor })
            },
            {
                VanillaBlockType.Miner,
                new IoConnectionData(
                    new ConnectDirection[] { },
                    new ConnectDirection[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) },
                    new[] { VanillaBlockType.BeltConveyor })
            },
            {
                VanillaBlockType.BeltConveyor, new IoConnectionData(
                    // 
                    new ConnectDirection[] { new(-1, 0), new(0, 1), new(0, -1) },
                    
                    new ConnectDirection[] { new(1, 0) },
                    new[] { VanillaBlockType.Machine, VanillaBlockType.Chest, VanillaBlockType.Generator, VanillaBlockType.Miner, VanillaBlockType.BeltConveyor })
            }
        };
    }


    /// <summary>
    ///     
    ///     
    /// </summary>
    public class IoConnectionData
    {
        public readonly string[] ConnectableBlockType;
        public readonly ConnectDirection[] InputConnector;
        public readonly ConnectDirection[] OutputConnector;

        public IoConnectionData(ConnectDirection[] inputConnector, ConnectDirection[] outputConnector, string[] connectableBlockType)
        {
            InputConnector = inputConnector;
            OutputConnector = outputConnector;
            ConnectableBlockType = connectableBlockType;
        }
    }

    public class ConnectDirection : IEquatable<ConnectDirection>
    {
        public readonly int East;

        public readonly int North;

        public ConnectDirection(int north, int east)
        {
            North = north;
            East = east;
        }

        public bool Equals(ConnectDirection other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return North == other.North && East == other.East;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ConnectDirection)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(North, East);
        }
    }
}