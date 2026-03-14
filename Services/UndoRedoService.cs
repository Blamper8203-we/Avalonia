using System;
using System.Collections.Generic;

namespace DINBoard.Services;

public interface IUndoableCommand
{
    void Execute();
    void Undo();
}

public class UndoRedoService
{
    /// <summary>
    /// Maksymalna głębokość historii (dla oszczędności pamięci).
    /// </summary>
    public const int MaxHistoryDepth = 50;

    private readonly LinkedList<IUndoableCommand> _undoStack = new();
    private readonly LinkedList<IUndoableCommand> _redoStack = new();

    public event Action? StateChanged;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;

    public void Execute(IUndoableCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute();
        _undoStack.AddLast(command);
        _redoStack.Clear();

        // Limit głębokości historii
        TrimHistory();

        StateChanged?.Invoke();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;

        var command = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        command.Undo();
        _redoStack.AddLast(command);
        StateChanged?.Invoke();
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;

        var command = _redoStack.Last!.Value;
        _redoStack.RemoveLast();
        command.Execute();
        _undoStack.AddLast(command);

        // Limit też dla redo->undo
        TrimHistory();

        StateChanged?.Invoke();
    }

    /// <summary>
    /// Czyści całą historię.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke();
    }

    private void TrimHistory()
    {
        while (_undoStack.Count > MaxHistoryDepth)
        {
            _undoStack.RemoveFirst(); // Usuń najstarszą operację
        }
    }
}
