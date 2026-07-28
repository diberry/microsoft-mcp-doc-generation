namespace Shared;

/// <summary>
/// Centralized parameter sorting logic shared across generation steps.
/// </summary>
public static class ParameterSorting
{
    /// <summary>
    /// Sorts items so required entries appear before optional entries while preserving source order within each group.
    /// </summary>
    public static IOrderedEnumerable<T> SortByRequiredThenName<T>(
        IEnumerable<T> items,
        Func<T, bool> isRequired)
    {
        return items.OrderByDescending(isRequired);
    }
}
