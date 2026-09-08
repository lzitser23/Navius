using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Navius.Primitives.Components.Field;
using Navius.Primitives.Components.Form;
using Navius.Primitives.Components.PasswordToggleField;
using Navius.Primitives.Components.ScrollArea;
using Navius.Primitives.Components.Select;
using Navius.Primitives.Components.Slider;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Tests;

/// <summary>
/// The engine module resolves in two modes: package mode serves navius-interop.js as a
/// static web asset under <c>_content/Navius.Primitives/</c>, while vendored mode (the
/// registry CLI) copies it into the consumer's own wwwroot at the app root, with no
/// package reference. Every importer must try the package path first and fall back to the
/// vendored path, or a vendored consumer silently loses all JS-driven behaviour. Exercised
/// through the <see cref="IJSRuntime"/> seam, whose package-mode import fails exactly the
/// way a missing static web asset does.
/// </summary>
public class ModuleFallbackTests
{
    [Fact]
    public async Task Vendored_module_serves_the_interop_when_the_package_import_fails()
    {
        var js = new PathAwareJsRuntime { FailingImports = { ModulePaths.Package } };
        await using var interop = new NaviusJsInterop(js);

        await interop.LockScrollAsync();

        Assert.Equal(new[] { ModulePaths.Package, ModulePaths.Vendored }, js.ImportedPaths);
        var module = Assert.Single(js.Modules);
        Assert.Equal(ModulePaths.Vendored, module.Path);
        // Live on the vendored module, not merely past the import.
        Assert.Contains("lockScroll", module.Calls);

        // The cached import keeps serving the same vendored module.
        await interop.UnlockScrollAsync();
        Assert.Equal(new[] { ModulePaths.Package, ModulePaths.Vendored }, js.ImportedPaths);
        Assert.Contains("unlockScroll", module.Calls);
    }

    [Fact]
    public async Task Package_module_is_used_alone_when_its_import_succeeds()
    {
        var js = new PathAwareJsRuntime();
        await using var interop = new NaviusJsInterop(js);

        await interop.LockScrollAsync();

        Assert.Equal(new[] { ModulePaths.Package }, js.ImportedPaths);
        Assert.Equal(ModulePaths.Package, Assert.Single(js.Modules).Path);
    }

    [Fact]
    public async Task Losing_both_imports_reports_both_paths_and_keeps_the_package_failure_as_the_cause()
    {
        var js = new PathAwareJsRuntime { FailingImports = { ModulePaths.Package, ModulePaths.Vendored } };
        var interop = new NaviusJsInterop(js);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(interop.LockScrollAsync);

        Assert.Equal(new[] { ModulePaths.Package, ModulePaths.Vendored }, js.ImportedPaths);
        Assert.Contains($"'{ModulePaths.Package}'", error.Message);
        Assert.Contains($"'{ModulePaths.Vendored}'", error.Message);
        var cause = Assert.IsType<JSException>(error.InnerException);
        Assert.Contains(ModulePaths.Package, cause.Message);
    }
}

/// <summary>
/// The six components that import the engine themselves (rather than through
/// <see cref="NaviusJsInterop"/>) carry their own copy of the package-then-vendored
/// fallback. Rendered against a runtime whose package-mode import fails — exactly a
/// vendored consumer's situation — each must reach for the vendored path and keep
/// working. bUnit's own module setup cannot fail an import (SetupModule exposes no
/// SetException), so the <see cref="IJSRuntime"/> registration is the seam instead.
/// </summary>
public class ComponentModuleFallbackTests
{
    private static TestContext NewCtx(out PathAwareJsRuntime js)
    {
        var runtime = new PathAwareJsRuntime { FailingImports = { ModulePaths.Package } };
        var ctx = new TestContext();
        ctx.Services.AddSingleton<IJSRuntime>(runtime);
        js = runtime;
        return ctx;
    }

    private static readonly string[] PackageThenVendored = [ModulePaths.Package, ModulePaths.Vendored];

    [Fact]
    public void Form_focus_first_invalid_falls_back_to_the_vendored_engine()
    {
        using var ctx = NewCtx(out var js);
        var submitted = false;

        var cut = ctx.RenderComponent<NaviusForm>(ps => ps
            .Add(p => p.OnSubmit, EventCallback.Factory.Create(this, () => submitted = true))
            .AddChildContent<NaviusField>(f => f
                .Add(x => x.Name, "email")
                .Add(x => x.Invalid, true)));

        Assert.Empty(js.ImportedPaths); // nothing is imported until a submit must focus

        cut.Find("form").Submit();

        Assert.Equal(PackageThenVendored, js.ImportedPaths);
        // Focus-first-invalid really ran, through the vendored module.
        Assert.Contains("focusElementById", Assert.Single(js.Modules).Calls);
        Assert.False(submitted); // and the invalid field still blocked submission
    }

    [Fact]
    public async Task Select_viewport_observer_falls_back_to_the_vendored_engine()
    {
        using var ctx = NewCtx(out var js);

        var cut = ctx.RenderComponent<NaviusSelectViewport>(ps => ps
            .AddChildContent<CascadedContextProbe<SelectViewportContext>>());

        Assert.Equal(PackageThenVendored, js.ImportedPaths);
        Assert.Contains("observeScrollArea", Assert.Single(js.Modules).Calls);

        // The observer the vendored module handed back still drives the scroll buttons.
        var viewport = cut.FindComponent<CascadedContextProbe<SelectViewportContext>>().Instance.Context!;
        await cut.InvokeAsync(() => cut.Instance.OnScrollMetrics(
            new NaviusSelectViewport.ScrollMetrics(0, 400, 100, 0, 0, 0)));

        Assert.True(viewport.CanScrollDown);
    }

    [Fact]
    public async Task ScrollArea_viewport_observer_falls_back_to_the_vendored_engine()
    {
        using var ctx = NewCtx(out var js);

        var cut = ctx.RenderComponent<NaviusScrollArea>(ps => ps
            .AddChildContent<NaviusScrollAreaViewport>()
            .AddChildContent<CascadedContextProbe<ScrollAreaContext>>());

        Assert.Equal(PackageThenVendored, js.ImportedPaths);
        Assert.Contains("observeScrollArea", Assert.Single(js.Modules).Calls);

        // Metrics from that observer still reach the shared context and derive geometry.
        var area = cut.FindComponent<CascadedContextProbe<ScrollAreaContext>>().Instance.Context!;
        var viewport = cut.FindComponent<NaviusScrollAreaViewport>();
        await cut.InvokeAsync(() => viewport.Instance.OnScrollMetrics(
            new ScrollAreaContext.ScrollMetrics(0, 400, 100, 0, 0, 0)));

        Assert.True(area.HasVerticalOverflow);
        Assert.Equal(0.25, area.ThumbSizeRatio("vertical"));
    }

    [Fact]
    public void ScrollArea_thumb_drag_wiring_falls_back_to_the_vendored_engine()
    {
        using var ctx = NewCtx(out var js);
        var area = new ScrollAreaContext("always") { HasViewport = true };

        var cut = ctx.RenderComponent<NaviusScrollAreaThumb>(ps => ps
            .AddCascadingValue(area)
            .AddCascadingValue("ScrollAreaScrollbarOrientation", "horizontal"));

        Assert.Equal(PackageThenVendored, js.ImportedPaths);
        // The drag gesture is really wired, through the vendored module, for this axis.
        var module = Assert.Single(js.Modules);
        Assert.Contains("dragScrollThumb", module.Calls);
        Assert.Contains((object?)"horizontal", module.ArgsOf("dragScrollThumb"));
        Assert.Equal("horizontal", cut.Find("[data-navius-scrollarea-thumb]").GetAttribute("data-orientation"));
    }

    [Fact]
    public void Password_toggle_focus_return_falls_back_to_the_vendored_engine()
    {
        using var ctx = NewCtx(out var js);

        var cut = ctx.RenderComponent<NaviusPasswordToggleField>(ps => ps
            .AddChildContent<NaviusPasswordToggleFieldToggle>());

        Assert.Empty(js.ImportedPaths); // nothing is imported until a pointer click

        cut.Find("button").Click(new MouseEventArgs { Detail = 1 });

        Assert.Equal(PackageThenVendored, js.ImportedPaths);
        // Focus really returned to the input, through the vendored module.
        Assert.Contains("focusElementById", Assert.Single(js.Modules).Calls);
        // And the toggle still flipped the revealed state.
        Assert.Equal("Hide password", cut.Find("button").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task Slider_drag_tracker_falls_back_to_the_vendored_engine()
    {
        using var ctx = NewCtx(out var js);
        var committed = new List<IReadOnlyList<double>>();

        var cut = ctx.RenderComponent<NaviusSlider>(ps => ps
            .Add(p => p.DefaultValue, new double[] { 10 })
            .Add(p => p.ValueCommitted, EventCallback.Factory.Create<IReadOnlyList<double>>(
                this, v => committed.Add(v))));

        Assert.Equal(PackageThenVendored, js.ImportedPaths);
        Assert.Contains("createDragTracker", Assert.Single(js.Modules).Calls);

        // A gesture from that tracker still moves the thumb and commits the value.
        await cut.InvokeAsync(() => cut.Instance.OnCommit(0.5));

        Assert.Equal(50d, Assert.Single(committed).Single());
    }
}

/// <summary>The two paths every importer must try, in this order.</summary>
internal static class ModulePaths
{
    public const string Package = "./_content/Navius.Primitives/navius-interop.js";
    public const string Vendored = "./navius-interop.js";
}

/// <summary>Records every import path, fails the chosen ones, and serves a recording module.</summary>
internal sealed class PathAwareJsRuntime : IJSRuntime
{
    public List<string> ImportedPaths { get; } = [];
    public HashSet<string> FailingImports { get; } = [];
    public List<RecordingModule> Modules { get; } = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (identifier != "import")
        {
            return ValueTask.FromResult(default(TValue)!); // localStorage.getItem etc.
        }

        var path = Assert.IsType<string>(Assert.Single(args ?? []));
        ImportedPaths.Add(path);

        if (FailingImports.Contains(path))
        {
            throw new JSException($"Failed to fetch dynamically imported module: {path}");
        }

        var module = new RecordingModule(path);
        Modules.Add(module);
        return ValueTask.FromResult((TValue)(object)module);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);
}

/// <summary>
/// The navius-interop.js surface, remembering which path it was imported from and every
/// export the caller reached for. Exports that hand back a live handle (observers, drag
/// trackers) return another recording module, so a handle's own calls are observable too.
/// </summary>
internal sealed class RecordingModule(string path) : IJSObjectReference
{
    public string Path { get; } = path;
    public List<string> Calls { get; } = [];
    private readonly List<object?[]> _args = [];

    /// <summary>The arguments the first call to <paramref name="identifier"/> was made with.</summary>
    public object?[] ArgsOf(string identifier) => _args[Calls.IndexOf(identifier)];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        Calls.Add(identifier);
        _args.Add(args ?? []);

        return typeof(TValue) == typeof(IJSObjectReference)
            ? ValueTask.FromResult((TValue)(object)new RecordingModule(Path))
            : ValueTask.FromResult(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
