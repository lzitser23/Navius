using Bunit;
using Microsoft.AspNetCore.Components;
using Navius.Primitives.Components.Dialog;
using Navius.Primitives.Components.Tabs;
using Navius.Primitives.Components.Toggle;

namespace Navius.Primitives.Tests;

/// <summary>
/// Regression tests for the controlled/uncontrolled invariant (CONTEXT.md): controlled-ness
/// is determined by whether the value parameter was explicitly supplied (tracked in
/// SetParametersAsync), NOT by EventCallback.HasDelegate. Covers one representative per
/// family shape: overlay open-state (Dialog), value-selection (Tabs), pressed/checked (Toggle).
/// </summary>
public class ControlledStateTests
{
    private static TestContext NewCtx()
    {
        var ctx = new TestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return ctx;
    }

    // ---------------------------------------------------------------------
    // Family: overlay open-state (Dialog / DialogContext)
    // ---------------------------------------------------------------------

    [Fact]
    public void Overlay_Uncontrolled_DefaultOpenTrue_WithObserverCallback_StartsOpen()
    {
        // The reported bug: DefaultOpen=true + an observer-only OpenChanged froze the
        // dialog closed because HasDelegate made it "controlled" at Open=false.
        using var ctx = NewCtx();
        var observed = new List<bool>();

        var cut = ctx.RenderComponent<NaviusDialog>(ps => ps
            .Add(p => p.DefaultOpen, true)
            .Add(p => p.OpenChanged, EventCallback.Factory.Create<bool>(this, v => observed.Add(v)))
            .AddChildContent<CascadedContextProbe<DialogContext>>());

        var context = cut.FindComponent<CascadedContextProbe<DialogContext>>().Instance.Context!;
        Assert.True(context.Open); // must honour DefaultOpen, not freeze closed
    }

    [Fact]
    public async Task Overlay_Uncontrolled_UserToggle_UpdatesStateAndNotifiesObserver()
    {
        using var ctx = NewCtx();
        var observed = new List<bool>();

        var cut = ctx.RenderComponent<NaviusDialog>(ps => ps
            .Add(p => p.DefaultOpen, true)
            .Add(p => p.OpenChanged, EventCallback.Factory.Create<bool>(this, v => observed.Add(v)))
            .AddChildContent<CascadedContextProbe<DialogContext>>());

        var context = cut.FindComponent<CascadedContextProbe<DialogContext>>().Instance.Context!;

        await cut.InvokeAsync(() => context.RequestToggleAsync());

        Assert.False(context.Open);              // uncontrolled state actually changed
        Assert.Equal(new[] { false }, observed); // observer saw the change
    }

    [Fact]
    public async Task Overlay_Controlled_IgnoresInternalToggleUntilParentUpdates()
    {
        using var ctx = NewCtx();
        var observed = new List<bool>();

        var cut = ctx.RenderComponent<NaviusDialog>(ps => ps
            .Add(p => p.Open, false)
            .Add(p => p.OpenChanged, EventCallback.Factory.Create<bool>(this, v => observed.Add(v)))
            .AddChildContent<CascadedContextProbe<DialogContext>>());

        var context = cut.FindComponent<CascadedContextProbe<DialogContext>>().Instance.Context!;
        Assert.False(context.Open);

        await cut.InvokeAsync(() => context.RequestToggleAsync());

        // Controlled: the parent owns the value; without a parent update the state holds.
        Assert.False(context.Open);
        Assert.Equal(new[] { true }, observed); // but the callback still fired with the request
    }

    [Fact]
    public async Task Overlay_TwoWayBind_FlowsThroughParent()
    {
        using var ctx = NewCtx();
        var parentOpen = false;

        var cut = ctx.RenderComponent<NaviusDialog>(ps => ps
            .Add(p => p.Open, parentOpen)
            .Add(p => p.OpenChanged, EventCallback.Factory.Create<bool>(this, v => parentOpen = v))
            .AddChildContent<CascadedContextProbe<DialogContext>>());

        var context = cut.FindComponent<CascadedContextProbe<DialogContext>>().Instance.Context!;

        await cut.InvokeAsync(() => context.RequestToggleAsync());
        Assert.True(parentOpen); // callback set the bound field

        cut.SetParametersAndRender(ps => ps.Add(p => p.Open, parentOpen));
        Assert.True(context.Open); // new bound value flowed back in
    }

    // ---------------------------------------------------------------------
    // Family: value-selection (Tabs / TabsContext)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Selection_Uncontrolled_DefaultValue_WithObserverCallback_Selects()
    {
        using var ctx = NewCtx();
        var observed = new List<string?>();

        var cut = ctx.RenderComponent<NaviusTabs>(ps => ps
            .Add(p => p.DefaultValue, "a")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, v => observed.Add(v)))
            .AddChildContent<CascadedContextProbe<TabsContext>>());

        var context = cut.FindComponent<CascadedContextProbe<TabsContext>>().Instance.Context!;
        Assert.Equal("a", context.Selected); // DefaultValue honoured, not frozen null

        await cut.InvokeAsync(() => context.SelectAsync("b"));
        Assert.Equal("b", context.Selected);
        Assert.Equal(new[] { "b" }, observed);
    }

    [Fact]
    public async Task Selection_Controlled_HoldsUntilParentUpdates()
    {
        using var ctx = NewCtx();
        var observed = new List<string?>();

        var cut = ctx.RenderComponent<NaviusTabs>(ps => ps
            .Add(p => p.Value, "a")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, v => observed.Add(v)))
            .AddChildContent<CascadedContextProbe<TabsContext>>());

        var context = cut.FindComponent<CascadedContextProbe<TabsContext>>().Instance.Context!;
        Assert.Equal("a", context.Selected);

        await cut.InvokeAsync(() => context.SelectAsync("b"));
        Assert.Equal("a", context.Selected);   // controlled: value held
        Assert.Equal(new[] { "b" }, observed);  // callback fired with the request
    }

    // ---------------------------------------------------------------------
    // Family: pressed/checked (Toggle button)
    // ---------------------------------------------------------------------

    [Fact]
    public void Pressed_Uncontrolled_DefaultPressed_WithObserverCallback_Toggles()
    {
        using var ctx = NewCtx();
        var observed = new List<bool>();

        var cut = ctx.RenderComponent<NaviusToggle>(ps => ps
            .Add(p => p.DefaultPressed, true)
            .Add(p => p.PressedChanged, EventCallback.Factory.Create<bool>(this, v => observed.Add(v)))
            .AddChildContent("x"));

        var button = cut.Find("button");
        Assert.Equal("true", button.GetAttribute("aria-pressed")); // DefaultPressed honoured

        button.Click();

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed"));
        Assert.Equal(new[] { false }, observed);
    }

    [Fact]
    public void Pressed_Controlled_HoldsUntilParentUpdates()
    {
        using var ctx = NewCtx();
        var observed = new List<bool>();

        var cut = ctx.RenderComponent<NaviusToggle>(ps => ps
            .Add(p => p.Pressed, false)
            .Add(p => p.PressedChanged, EventCallback.Factory.Create<bool>(this, v => observed.Add(v)))
            .AddChildContent("x"));

        cut.Find("button").Click();

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed")); // controlled: held
        Assert.Equal(new[] { true }, observed);                                   // callback fired
    }
}
