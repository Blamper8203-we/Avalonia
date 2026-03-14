using System;
using System.Collections.ObjectModel;
using Xunit;
using DINBoard.Services;
using DINBoard.Models;

namespace Avalonia.Tests
{
    public class UndoRedoTests
    {
        #region Basic Command Tests

        [Fact]
        public void Service_ShouldPushCommandsToUndoStack()
        {
            var service = new UndoRedoService();
            var coll = new ObservableCollection<SymbolItem>();
            var item = new SymbolItem();

            service.Execute(new AddSymbolCommand(coll, item));

            Assert.True(service.CanUndo);
            Assert.False(service.CanRedo);
            Assert.Single(coll);
        }

        [Fact]
        public void Undo_ShouldRevertAddCommand()
        {
            var service = new UndoRedoService();
            var coll = new ObservableCollection<SymbolItem>();
            var item = new SymbolItem();

            service.Execute(new AddSymbolCommand(coll, item));
            service.Undo();

            Assert.Empty(coll);
            Assert.False(service.CanUndo);
            Assert.True(service.CanRedo);
        }

        [Fact]
        public void Redo_ShouldReApplyAddCommand()
        {
            var service = new UndoRedoService();
            var coll = new ObservableCollection<SymbolItem>();
            var item = new SymbolItem();

            service.Execute(new AddSymbolCommand(coll, item));
            service.Undo();
            service.Redo();

            Assert.Single(coll);
            Assert.Equal(item, coll[0]);
        }

        [Fact]
        public void Undo_ShouldRevertDeleteCommand()
        {
            var service = new UndoRedoService();
            var coll = new ObservableCollection<SymbolItem>();
            var item = new SymbolItem();
            coll.Add(item);

            service.Execute(new DeleteSymbolCommand(coll, item));

            Assert.Empty(coll); // Executed

            service.Undo();
            Assert.Single(coll); // Restored
        }

        #endregion

        #region Multiple Commands Tests

        [Fact]
        public void MultipleCommands_ShouldUndoInReverseOrder()
        {
            var service = new UndoRedoService();
            var coll = new ObservableCollection<SymbolItem>();
            var item1 = new SymbolItem { Id = "1" };
            var item2 = new SymbolItem { Id = "2" };
            var item3 = new SymbolItem { Id = "3" };

            service.Execute(new AddSymbolCommand(coll, item1));
            service.Execute(new AddSymbolCommand(coll, item2));
            service.Execute(new AddSymbolCommand(coll, item3));

            Assert.Equal(3, coll.Count);

            service.Undo();
            Assert.Equal(2, coll.Count);
            Assert.DoesNotContain(item3, coll);

            service.Undo();
            Assert.Single(coll);
            Assert.DoesNotContain(item2, coll);

            service.Undo();
            Assert.Empty(coll);
        }

        [Fact]
        public void MultipleUndoRedo_ShouldMaintainCorrectState()
        {
            var service = new UndoRedoService();
            var coll = new ObservableCollection<SymbolItem>();
            var item = new SymbolItem();

            service.Execute(new AddSymbolCommand(coll, item));
            service.Undo();
            service.Redo();
            service.Undo();
            service.Redo();

            Assert.Single(coll);
            Assert.Equal(item, coll[0]);
        }

        #endregion

        #region StateChanged Event Tests

        [Fact]
        public void Execute_ShouldRaiseStateChangedEvent()
        {
            var service = new UndoRedoService();
            var coll = new ObservableCollection<SymbolItem>();
            var item = new SymbolItem();
            var eventRaised = false;

            service.StateChanged += () => eventRaised = true;
            service.Execute(new AddSymbolCommand(coll, item));

            Assert.True(eventRaised);
        }

        [Fact]
        public void Undo_ShouldRaiseStateChangedEvent()
        {
            var service = new UndoRedoService();
            var coll = new ObservableCollection<SymbolItem>();
            var item = new SymbolItem();
            service.Execute(new AddSymbolCommand(coll, item));

            var eventRaised = false;
            service.StateChanged += () => eventRaised = true;
            service.Undo();

            Assert.True(eventRaised);
        }

        [Fact]
        public void Redo_ShouldRaiseStateChangedEvent()
        {
            var service = new UndoRedoService();
            var coll = new ObservableCollection<SymbolItem>();
            var item = new SymbolItem();
            service.Execute(new AddSymbolCommand(coll, item));
            service.Undo();

            var eventRaised = false;
            service.StateChanged += () => eventRaised = true;
            service.Redo();

            Assert.True(eventRaised);
        }

        #endregion

        #region History Depth Tests

        [Fact]
        public void Execute_ShouldTrimHistoryAfterMaxDepth()
        {
            var service = new UndoRedoService();
            var coll = new ObservableCollection<SymbolItem>();

            // Dodaj więcej komend niż MaxHistoryDepth (50)
            for (int i = 0; i < UndoRedoService.MaxHistoryDepth + 10; i++)
            {
                service.Execute(new AddSymbolCommand(coll, new SymbolItem { Id = $"item{i}" }));
            }

            Assert.Equal(UndoRedoService.MaxHistoryDepth, service.UndoCount);
            Assert.Equal(UndoRedoService.MaxHistoryDepth + 10, coll.Count);
        }

        [Fact]
        public void Execute_ShouldClearRedoStack()
        {
            var service = new UndoRedoService();
            var coll = new ObservableCollection<SymbolItem>();
            var item1 = new SymbolItem();
            var item2 = new SymbolItem();

            service.Execute(new AddSymbolCommand(coll, item1));
            service.Undo();

            Assert.True(service.CanRedo);

            // Nowa komenda powinna wyczyścić stos redo
            service.Execute(new AddSymbolCommand(coll, item2));

            Assert.False(service.CanRedo);
        }

        #endregion

        #region Clear Tests

        [Fact]
        public void Clear_ShouldEmptyUndoAndRedoStacks()
        {
            var service = new UndoRedoService();
            var coll = new ObservableCollection<SymbolItem>();
            var item = new SymbolItem();

            service.Execute(new AddSymbolCommand(coll, item));
            service.Undo();

            Assert.True(service.CanRedo);
            Assert.False(service.CanUndo);

            service.Clear();

            Assert.False(service.CanUndo);
            Assert.False(service.CanRedo);
            Assert.Equal(0, service.UndoCount);
            Assert.Equal(0, service.RedoCount);
        }

        [Fact]
        public void Clear_ShouldRaiseStateChangedEvent()
        {
            var service = new UndoRedoService();
            var coll = new ObservableCollection<SymbolItem>();
            service.Execute(new AddSymbolCommand(coll, new SymbolItem()));

            var eventRaised = false;
            service.StateChanged += () => eventRaised = true;
            service.Clear();

            Assert.True(eventRaised);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void Undo_WhenEmpty_ShouldDoNothing()
        {
            var service = new UndoRedoService();

            // Nie powinno rzucić wyjątku
            service.Undo();

            Assert.False(service.CanUndo);
        }

        [Fact]
        public void Redo_WhenEmpty_ShouldDoNothing()
        {
            var service = new UndoRedoService();

            // Nie powinno rzucić wyjątku
            service.Redo();

            Assert.False(service.CanRedo);
        }

        [Fact]
        public void Execute_WithNullCommand_ShouldThrowArgumentNullException()
        {
            var service = new UndoRedoService();

            Assert.Throws<ArgumentNullException>(() => service.Execute(null!));
        }

        #endregion
    }
}
