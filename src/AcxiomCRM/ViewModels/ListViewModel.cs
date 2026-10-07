using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels;

public class ListViewModel<TItem, TFilter>
{
    public required PagedList<TItem> Items { get; init; }
    public required TFilter Filter { get; init; }

    /// <summary>Users the current user may filter/assign by (empty for Sales Executives).</summary>
    public List<SelectListItem> Users { get; init; } = new();
}

public class DetailsViewModel<T>
{
    public required T Item { get; init; }
    public List<HistoryEntry> History { get; init; } = new();
}
