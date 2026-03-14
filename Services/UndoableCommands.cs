using System.Collections.ObjectModel;
using DINBoard.Services;
using DINBoard.Models;

namespace DINBoard.Services;

public class AddSymbolCommand : IUndoableCommand
{
    private readonly ObservableCollection<SymbolItem> _collection;
    private readonly SymbolItem _symbol;

    public AddSymbolCommand(ObservableCollection<SymbolItem> collection, SymbolItem symbol)
    {
        _collection = collection;
        _symbol = symbol;
    }

    public void Execute()
    {
        if (!_collection.Contains(_symbol))
        {
            _collection.Add(_symbol);
        }
    }

    public void Undo()
    {
        if (_collection.Contains(_symbol))
        {
            _collection.Remove(_symbol);
        }
    }
}

public class DeleteSymbolCommand : IUndoableCommand
{
    private readonly ObservableCollection<SymbolItem> _collection;
    private readonly SymbolItem _symbol;

    public DeleteSymbolCommand(ObservableCollection<SymbolItem> collection, SymbolItem symbol)
    {
        _collection = collection;
        _symbol = symbol;
    }

    public void Execute()
    {
        if (_collection.Contains(_symbol))
        {
            _collection.Remove(_symbol);
        }
    }

    public void Undo()
    {
        if (!_collection.Contains(_symbol))
        {
            _collection.Add(_symbol);
        }
    }
}
