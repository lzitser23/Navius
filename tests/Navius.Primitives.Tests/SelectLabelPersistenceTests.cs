using Bunit;
using Navius.Primitives.Components.Select;

namespace Navius.Primitives.Tests;

/// <summary>
/// Regression for the select label-cache defect: once an item is selected and the popup
/// closes (its options dispose), the closed trigger must still render the item's LABEL,
/// not the raw value key. Labels are persisted in a value-to-label cache that survives
/// item unmount.
/// </summary>
public class SelectLabelPersistenceTests
{
    [Fact]
    public async Task ClosedTrigger_StillShowsLabel_AfterOptionsDispose()
    {
        using var ctx = new TestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.RenderComponent<SelectLabelHost>(ps => ps.Add(p => p.ShowItems, true));
        var context = cut.FindComponent<CascadedContextProbe<SelectContext>>().Instance.Context!;

        // Select the first option (value "customer_42", label "Ada Lovelace").
        cut.FindAll("[data-navius-select-item]")[0].Click();

        // Close the popup: its options unmount and dispose (UnregisterText fires).
        cut.SetParametersAndRender(ps => ps.Add(p => p.ShowItems, false));

        // Force the value display to re-read the label AFTER the options are gone, the way
        // the real close path re-renders the trigger once its listbox has torn down.
        await cut.InvokeAsync(() => context.RequestToggleAsync());

        var label = cut.Find("[data-navius-select-value]").TextContent.Trim();
        Assert.Equal("Ada Lovelace", label); // must be the label, not "customer_42"
    }
}
