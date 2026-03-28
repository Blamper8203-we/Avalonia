using System.Collections.Generic;
using System.Linq;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class SchematicPaginationServiceTests
{
    private readonly SchematicPaginationService _service = new();

    [Fact]
    public void AssignPagesAndPosition_WithNoDevices_ShouldCreateSingleEmptyPage()
    {
        var layout = new SchematicLayout();

        _service.AssignPagesAndPosition(layout, new List<SchematicNode>(), new List<SchematicNode>(), null);

        Assert.Empty(layout.Devices);
        Assert.Single(layout.Pages);
        Assert.Equal(1, layout.TotalPages);
        Assert.Equal(0, layout.Pages[0].PageIndex);
        Assert.Equal(0, layout.Pages[0].YOffset);
        Assert.Equal(SchematicLayoutEngine.PageW, layout.TotalWidth, 3);
        Assert.Equal(SchematicLayoutEngine.PageH, layout.TotalHeight, 3);
    }

    [Fact]
    public void AssignPagesAndPosition_WithMainAndCircuitDevices_ShouldStartCircuitsOnNextPage()
    {
        var layout = new SchematicLayout();
        var main = CreateNode(SchematicNodeType.MainBreaker, "Q0");
        var circuit = CreateNode(SchematicNodeType.MCB, "F1");

        _service.AssignPagesAndPosition(layout, new List<SchematicNode> { main }, new List<SchematicNode> { circuit }, null);

        Assert.Equal(0, main.Page);
        Assert.Equal(1, circuit.Page);
        Assert.Equal(2, layout.TotalPages);
        Assert.Equal(2, layout.Pages.Count);
        Assert.Equal(SchematicLayoutEngine.DrawT + SchematicLayoutEngine.YFR, main.Y, 3);
    }

    [Fact]
    public void AssignPagesAndPosition_GroupedMainBreaker_ShouldReserveHeadSlotAndPositionChildrenOnMcbRow()
    {
        var layout = new SchematicLayout();
        var child1 = CreateNode(SchematicNodeType.MCB, "F1");
        var child2 = CreateNode(SchematicNodeType.MCB, "F2");
        var main = CreateNode(SchematicNodeType.MainBreaker, "Q0", null, null, child1, child2);

        _service.AssignPagesAndPosition(layout, new List<SchematicNode> { main }, new List<SchematicNode>(), null);

        Assert.Equal(0, main.Page);
        Assert.Equal(0, child1.Page);
        Assert.Equal(0, child2.Page);
        Assert.True(main.X < child1.X);
        Assert.True(child1.X < child2.X);
        Assert.True(main.CellWidth > child1.CellWidth + child2.CellWidth);
        Assert.Equal(SchematicLayoutEngine.DrawT + SchematicLayoutEngine.YFR, main.Y, 3);
        Assert.Equal(SchematicLayoutEngine.DrawT + SchematicLayoutEngine.YMCB - 20, child1.Y, 3);
        Assert.Equal(SchematicLayoutEngine.DrawT + SchematicLayoutEngine.YMCB - 20, child2.Y, 3);
    }

    [Fact]
    public void AssignPagesAndPosition_DesignationStartingWithF_ShouldApplyVerticalOffsetExceptFa()
    {
        var layout = new SchematicLayout();
        var qfLike = CreateNode(SchematicNodeType.MCB, "F1");
        var faLike = CreateNode(SchematicNodeType.MCB, "FA1");

        _service.AssignPagesAndPosition(layout, new List<SchematicNode>(), new List<SchematicNode> { qfLike, faLike }, null);

        Assert.Equal(SchematicLayoutEngine.DrawT + SchematicLayoutEngine.YMainDev - 50, qfLike.Y, 3);
        Assert.Equal(SchematicLayoutEngine.DrawT + SchematicLayoutEngine.YMainDev, faLike.Y, 3);
    }

    [Fact]
    public void AssignPagesAndPosition_WhenVisualSlotCountExceedsTen_ShouldSplitToNextPage()
    {
        var layout = new SchematicLayout();
        var circuits = Enumerable.Range(1, 11)
            .Select(i => CreateNode(SchematicNodeType.MCB, $"F{i}"))
            .ToList();

        _service.AssignPagesAndPosition(layout, new List<SchematicNode>(), circuits, null);

        Assert.Equal(10, circuits.Count(c => c.Page == 0));
        Assert.Equal(1, circuits.Count(c => c.Page == 1));
        Assert.Equal(2, layout.TotalPages);
    }

    [Fact]
    public void AssignPagesAndPosition_WhenWidthExceedsAvailable_ShouldSplitToNextPage()
    {
        var layout = new SchematicLayout();
        var longText = new string('X', 100);
        var circuits = Enumerable.Range(1, 6)
            .Select(i => CreateNode(SchematicNodeType.MCB, $"FA{i}", longText, longText))
            .ToList();

        _service.AssignPagesAndPosition(layout, new List<SchematicNode>(), circuits, null);

        Assert.Equal(5, circuits.Count(c => c.Page == 0));
        Assert.Equal(1, circuits.Count(c => c.Page == 1));
        Assert.Equal(2, layout.TotalPages);
    }

    private static SchematicNode CreateNode(
        SchematicNodeType type,
        string designation,
        string protection = null,
        string circuitName = null,
        params SchematicNode[] children)
    {
        return new SchematicNode
        {
            NodeType = type,
            Designation = designation,
            Protection = protection ?? "B16",
            CircuitName = circuitName ?? "Circuit",
            Children = children.ToList()
        };
    }
}
