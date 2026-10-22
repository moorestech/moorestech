using System.Collections.Generic;

namespace Client.UitkPoc.Editing
{
    // 取り消し可能な1操作。Doは「やり直し」でも呼ばれる
    // One undoable operation; Do is also used for redo
    public interface IUndoableCommand
    {
        void Do();
        void Undo();
    }

    // 元に戻す／やり直すの履歴。ドラッグのように実行済みの操作はRecordで積む
    // Undo/redo history; operations already applied live (drags) are pushed with Record
    public class UndoStack
    {
        private readonly Stack<IUndoableCommand> _undo = new();
        private readonly Stack<IUndoableCommand> _redo = new();

        public int UndoCount => _undo.Count;
        public int RedoCount => _redo.Count;

        public void Execute(IUndoableCommand command)
        {
            command.Do();
            Record(command);
        }

        public void Record(IUndoableCommand appliedCommand)
        {
            _undo.Push(appliedCommand);
            _redo.Clear();
        }

        public void Undo()
        {
            if (_undo.Count == 0) return;
            var command = _undo.Pop();
            command.Undo();
            _redo.Push(command);
        }

        public void Redo()
        {
            if (_redo.Count == 0) return;
            var command = _redo.Pop();
            command.Do();
            _undo.Push(command);
        }
    }

    // 複数の操作を1回の取り消し単位にまとめる。戻すときは逆順
    // Groups several operations into one undo step; undone in reverse order
    public class CompositeCommand : IUndoableCommand
    {
        private readonly List<IUndoableCommand> _commands;

        public CompositeCommand(List<IUndoableCommand> commands)
        {
            _commands = commands;
        }

        public void Do()
        {
            foreach (var command in _commands) command.Do();
        }

        public void Undo()
        {
            for (var i = _commands.Count - 1; i >= 0; i--) _commands[i].Undo();
        }
    }
}
