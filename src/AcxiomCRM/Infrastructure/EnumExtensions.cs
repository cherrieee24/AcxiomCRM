using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.Infrastructure;

public static class EnumExtensions
{
    public static string DisplayName(this Enum value) =>
        value.GetType().GetMember(value.ToString()).FirstOrDefault()?
            .GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();

    /// <summary>Select list whose option values are enum names (binds back to the enum and to API JSON).</summary>
    public static IEnumerable<SelectListItem> ToSelectList<TEnum>(TEnum? selected = null) where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().Select(v => new SelectListItem(v.DisplayName(), v.ToString(), selected.HasValue && selected.Value.Equals(v)));
}
