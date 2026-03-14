using System;
using System.Collections.Generic;
using System.Linq;
using DINBoard.Models;

namespace DINBoard.Services;

internal sealed class SchematicPaginationService
{
    public void AssignPagesAndPosition(
        SchematicLayout layout,
        List<SchematicNode> mainDevices,
        List<SchematicNode> circuitDevices,
        Project? project)
    {
        double drawWidthAvailable = SchematicLayoutEngine.DrawW - SchematicLayoutEngine.ColMarginL - SchematicLayoutEngine.ColMarginR;

        int mainPage = 0;
        double currentMainWidth = 0;
        int currentMainCount = 0;
        foreach (var device in mainDevices)
        {
            ApplyCellWidth(device);
            int itemCount = GetVisualSlotCount(device);

            if (currentMainWidth > 0 && (currentMainWidth + device.CellWidth > drawWidthAvailable || currentMainCount + itemCount > 10))
            {
                mainPage++;
                currentMainWidth = 0;
                currentMainCount = 0;
            }

            device.Page = mainPage;
            currentMainWidth += device.CellWidth;
            currentMainCount += itemCount;
            if (device.Children.Count > 0)
            {
                foreach (var child in device.Children) child.Page = mainPage;
            }
        }

        int circuitPage = mainPage + (currentMainWidth > 0 ? 1 : 0);
        if (mainDevices.Count == 0) circuitPage = 0;

        double currentCircuitWidth = 0;
        int currentCircuitCount = 0;
        foreach (var device in circuitDevices)
        {
            ApplyCellWidth(device);
            int itemCount = GetVisualSlotCount(device);

            if (currentCircuitWidth > 0 && (currentCircuitWidth + device.CellWidth > drawWidthAvailable || currentCircuitCount + itemCount > 10))
            {
                circuitPage++;
                currentCircuitWidth = 0;
                currentCircuitCount = 0;
            }

            device.Page = circuitPage;
            currentCircuitWidth += device.CellWidth;
            currentCircuitCount += itemCount;
            if (device.Children.Count > 0)
            {
                foreach (var child in device.Children) child.Page = circuitPage;
            }
        }

        layout.Devices.AddRange(mainDevices);
        layout.Devices.AddRange(circuitDevices);

        int maxPage = layout.Devices.Count > 0 ? layout.Devices.Max(d => d.Page) : 0;
        layout.TotalPages = maxPage + 1;
        layout.TotalWidth = SchematicLayoutEngine.PageW;
        layout.TotalHeight = layout.TotalPages * SchematicLayoutEngine.PageH + (layout.TotalPages - 1) * SchematicLayoutEngine.PageGap;
        layout.FRPosition = (0, 0);

        for (int page = 0; page < layout.TotalPages; page++)
        {
            var pageDevices = layout.Devices.Where(d => d.Page == page).ToList();
            if (pageDevices.Count == 0)
            {
                layout.Pages.Add(new PageInfo
                {
                    PageIndex = page,
                    YOffset = page * (SchematicLayoutEngine.PageH + SchematicLayoutEngine.PageGap)
                });
                continue;
            }

            double schematicWidth = pageDevices.Sum(d => d.CellWidth);
            double available = SchematicLayoutEngine.DrawW - SchematicLayoutEngine.ColMarginL - SchematicLayoutEngine.ColMarginR;
            double offsetX = SchematicLayoutEngine.DrawL + SchematicLayoutEngine.ColMarginL + Math.Max(0, (available - schematicWidth) / 2.0);
            if (offsetX + schematicWidth > SchematicLayoutEngine.DrawR) offsetX = SchematicLayoutEngine.DrawR - schematicWidth;
            if (offsetX < SchematicLayoutEngine.DrawL + SchematicLayoutEngine.ColMarginL) offsetX = SchematicLayoutEngine.DrawL + SchematicLayoutEngine.ColMarginL;

            double offsetY = page * (SchematicLayoutEngine.PageH + SchematicLayoutEngine.PageGap);
            var pageInfo = new PageInfo
            {
                PageIndex = page,
                OffsetX = offsetX,
                YOffset = offsetY,
                MinCol = 0,
                BusX1 = Math.Max(SchematicLayoutEngine.DrawL, offsetX - 16),
                BusX2 = Math.Min(SchematicLayoutEngine.DrawR, offsetX + schematicWidth + 16)
            };
            layout.Pages.Add(pageInfo);

            double currentX = offsetX;
            foreach (var device in pageDevices)
            {
                PositionDeviceOnPage(device, pageInfo, currentX, project);
                currentX += device.CellWidth;
            }
        }
    }

    private static void ApplyCellWidth(SchematicNode device)
    {
        if (device.Children.Count > 0)
        {
            foreach (var child in device.Children) child.CellWidth = EstimateWidth(child);
            double childWidth = device.Children.Sum(c => c.CellWidth);
            device.CellWidth = ShouldReserveHeadSlot(device)
                ? EstimateWidth(device) + childWidth
                : childWidth;
            return;
        }

        device.CellWidth = EstimateWidth(device);
    }

    private static void PositionDeviceOnPage(SchematicNode node, PageInfo pageInfo, double startX, Project? project)
    {
        double yBase = pageInfo.YOffset + SchematicLayoutEngine.DrawT;
        double offsetQf = node.Designation != null && node.Designation.StartsWith("F") && !node.Designation.StartsWith("FA") ? 50 : 0;
        bool isGroupedMainBreaker = ShouldReserveHeadSlot(node);

        if (node.Children.Count > 0)
        {
            double headWidth = isGroupedMainBreaker ? GetHeadCellWidth(node) : 0;
            node.X = isGroupedMainBreaker
                ? startX + headWidth / 2.0 - SchematicLayoutEngine.NW / 2
                : startX + node.CellWidth / 2.0 - SchematicLayoutEngine.NW / 2;
            double childX = isGroupedMainBreaker ? startX + headWidth : startX;
            foreach (var child in node.Children)
            {
                double childOffsetQf = child.Designation != null && child.Designation.StartsWith("F") && !child.Designation.StartsWith("FA") ? 20 : 0;
                child.X = childX + child.CellWidth / 2.0 - SchematicLayoutEngine.NW / 2;
                child.Y = yBase + SchematicLayoutEngine.YMCB - childOffsetQf;
                childX += child.CellWidth;
            }

            node.Y = yBase + (isGroupedMainBreaker ? SchematicLayoutEngine.YFR : SchematicLayoutEngine.YMainDev) - offsetQf;
            return;
        }

        node.X = startX + node.CellWidth / 2.0 - SchematicLayoutEngine.NW / 2;
        node.Y = yBase + (node.NodeType == SchematicNodeType.MainBreaker ? SchematicLayoutEngine.YFR : SchematicLayoutEngine.YMainDev) - offsetQf;
    }

    private static double EstimateWidth(SchematicNode node)
    {
        double maxWidth = 0;
        maxWidth = Math.Max(maxWidth, (node.Designation?.Length ?? 0) * 6.5);
        maxWidth = Math.Max(maxWidth, (node.Protection?.Length ?? 0) * 6.0);
        maxWidth = Math.Max(maxWidth, (node.CircuitName?.Length ?? 0) * 5.5);
        maxWidth = Math.Max(maxWidth, (node.Location?.Length ?? 0) * 5.5);
        maxWidth = Math.Max(maxWidth, (node.CableDesig?.Length ?? 0) * 6.5);
        maxWidth = Math.Max(maxWidth, (node.CableType?.Length ?? 0) * 5.5);
        maxWidth = Math.Max(maxWidth, (node.CableSpec?.Length ?? 0) * 6.0);
        maxWidth = Math.Max(maxWidth, (node.PowerInfo?.Length ?? 0) * 5.5);
        return Math.Min(180, Math.Max(80, maxWidth + 16));
    }

    private static int GetVisualSlotCount(SchematicNode node)
    {
        if (ShouldReserveHeadSlot(node))
        {
            return node.Children.Count + 1;
        }

        return node.Children.Count > 0 ? Math.Max(1, node.Children.Count) : 1;
    }

    private static bool ShouldReserveHeadSlot(SchematicNode node)
        => node.NodeType == SchematicNodeType.MainBreaker && node.Children.Count > 0;

    private static double GetHeadCellWidth(SchematicNode node)
    {
        double childWidth = node.Children.Sum(child => child.CellWidth);
        double headWidth = node.CellWidth - childWidth;
        return headWidth > 0 ? headWidth : EstimateWidth(node);
    }
}
