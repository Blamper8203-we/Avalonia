using System.Collections.Generic;
using System.Linq;
using DINBoard.Models;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    private static void AddLeafNodes(List<SchematicNode> nodes, SchematicNode device)
    {
        if (ShouldReserveHeadSlot(device))
        {
            nodes.Add(CloneDisplayNode(device, GetHeadCellWidth(device)));
            foreach (var child in device.Children)
            {
                nodes.Add(child);
            }

            return;
        }

        if (device.Children.Count > 0)
        {
            foreach (var child in device.Children)
            {
                nodes.Add(child);
            }

            return;
        }

        nodes.Add(device);
    }

    private static int GetVisualSlotCount(SchematicNode node)
    {
        if (ShouldReserveHeadSlot(node))
        {
            return node.Children.Count + 1;
        }

        return node.Children.Count > 0 ? node.Children.Count : 1;
    }

    private static bool ShouldReserveHeadSlot(SchematicNode node)
        => node.NodeType == SchematicNodeType.MainBreaker && node.Children.Count > 0;

    private static double GetHeadCellWidth(SchematicNode node)
    {
        double childWidth = node.Children.Sum(child => child.CellWidth);
        double headWidth = node.CellWidth - childWidth;
        return headWidth > 0 ? headWidth : node.CellWidth;
    }

    private static SchematicNode CloneDisplayNode(SchematicNode node, double cellWidth)
    {
        return new SchematicNode
        {
            NodeType = node.NodeType,
            Symbol = node.Symbol,
            DistributionBlockSymbol = node.DistributionBlockSymbol,
            Designation = node.Designation,
            Protection = node.Protection,
            CircuitName = node.CircuitName,
            CableDesig = node.CableDesig,
            CableType = node.CableType,
            CableSpec = node.CableSpec,
            CableLength = node.CableLength,
            PowerInfo = node.PowerInfo,
            Phase = node.Phase,
            PhaseCount = node.PhaseCount,
            Location = node.Location,
            X = node.X,
            Y = node.Y,
            Width = node.Width,
            Height = node.Height,
            Column = node.Column,
            Page = node.Page,
            CellWidth = cellWidth,
        };
    }
}
