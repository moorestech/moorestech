using System;
using System.Collections.Generic;
using System.Text;
using Client.Game.InGame.Train.Unit;
using Game.Train.RailGraph;
using Game.Train.SaveLoad;
using Game.Train.Unit;

namespace Client.Game.InGame.Train.DebugView
{
    public sealed class TrainUnitDebugStatusFormatter
    {
        private const int MaxDisplayedCarsPerTrain = 16;

        private readonly StringBuilder _builder = new(8192);
        private readonly List<ClientTrainUnit> _units = new();

        public string Format(TrainUnitClientCache trainCache, TrainUnitTickState tickState)
        {
            _builder.Clear();
            _units.Clear();

            // 列車キャッシュとtick状態を同じフレームの表示文字列へまとめる
            // Build one visible text snapshot from train cache and tick state.
            AppendHeader(trainCache, tickState);
            trainCache.CopyUnitsTo(_units);
            _units.Sort(CompareUnits);

            if (_units.Count == 0)
            {
                _builder.AppendLine("No train units.");
                return _builder.ToString();
            }

            // 表示順を固定して、動画やスクショで差分を追いやすくする
            // Keep display order stable so videos and screenshots are easy to compare.
            for (var i = 0; i < _units.Count; i++)
            {
                AppendUnit(i, _units[i]);
            }

            return _builder.ToString();
        }

        private void AppendHeader(TrainUnitClientCache trainCache, TrainUnitTickState tickState)
        {
            // 同期状況とハッシュを先頭に出し、ズレの有無を最初に見えるようにする
            // Show sync state and hash first so drift is visible immediately.
            _builder.AppendLine("[TrainUnit Debug Status]");
            _builder.Append("tick=").Append(tickState.GetTick());
            _builder.Append(" sequence=").Append(tickState.GetTickSequenceId());
            _builder.Append(" units=").Append(trainCache.Units.Count);
            _builder.Append(" hash=0x").Append(trainCache.ComputeCurrentHash().ToString("X8"));
            _builder.AppendLine();
        }

        private void AppendUnit(int index, ClientTrainUnit unit)
        {
            var cars = unit.Cars;
            var railPosition = unit.RailPosition;

            // TrainUnit単位で移動状態と編成状態をまとめる
            // Group movement and formation state by TrainUnit.
            _builder.Append('[').Append(index).Append("] train=").Append(ShortText(unit.TrainUnitInstanceId.ToString()));
            _builder.Append(" cars=").Append(cars.Count);
            _builder.Append(" speed=").Append(unit.CurrentSpeed.ToString("F3"));
            _builder.Append(" accumulated=").Append(unit.AccumulatedDistance.ToString("F3"));
            _builder.Append(" mascon=").Append(unit.MasconLevel);
            _builder.AppendLine();

            if (railPosition == null)
            {
                _builder.AppendLine("  rail: none");
            }
            else
            {
                // RailPositionの先頭側と直前ノードを出し、ワープ前後の位置を追跡する
                // Print rail head and just-passed node to track position around warps.
                _builder.Append("  rail: distanceToNext=").Append(railPosition.GetDistanceToNextNode());
                _builder.Append(" approaching={").Append(FormatNode(railPosition.GetNodeApproaching())).Append('}');
                _builder.Append(" passed={").Append(FormatNode(railPosition.GetNodeJustPassed())).Append('}');
                _builder.AppendLine();
            }

            AppendCars(cars);
        }

        private void AppendCars(IReadOnlyList<TrainCarSnapshot> cars)
        {
            var count = Math.Min(cars.Count, MaxDisplayedCarsPerTrain);

            // 車両ごとの向きと燃料有無を出し、牽引力の怪しさを追えるようにする
            // Show per-car direction and fuel flags to inspect traction-related behavior.
            for (var i = 0; i < count; i++)
            {
                var car = cars[i];
                _builder.Append("  car[").Append(i).Append("] id=").Append(ShortText(car.TrainCarInstanceId.ToString()));
                _builder.Append(" facing=").Append(car.IsFacingForward ? "forward" : "backward");
                _builder.Append(" fuel=").Append(car.HasFuel ? "yes" : "no");
                _builder.Append(" weight=").Append(car.Weight);
                _builder.AppendLine();
            }

            if (cars.Count > count)
            {
                _builder.Append("  ... ").Append(cars.Count - count).AppendLine(" more cars");
            }
        }

        private static int CompareUnits(ClientTrainUnit left, ClientTrainUnit right)
        {
            return string.CompareOrdinal(left.TrainUnitInstanceId.ToString(), right.TrainUnitInstanceId.ToString());
        }

        private static string FormatNode(IRailNode node)
        {
            if (node == null)
            {
                return "none";
            }

            var destination = node.ConnectionDestination;
            return $"id={node.NodeId} dest={FormatDestination(destination)}";
        }

        private static string FormatDestination(ConnectionDestination destination)
        {
            var position = destination.blockPosition;
            return $"({position.x},{position.y},{position.z}) component={destination.componentIndex} front={destination.IsFront}";
        }

        private static string ShortText(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= 8)
            {
                return text;
            }

            return text.Substring(0, 8);
        }
    }
}
