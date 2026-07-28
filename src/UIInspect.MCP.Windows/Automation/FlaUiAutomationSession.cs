// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Windows.Automation;

/// <summary>Serialized, locator-based UIA3 session that never exposes live COM elements.</summary>
public sealed class FlaUiAutomationSession : IUiAutomationSession
{
    private static readonly IReadOnlyDictionary<string, VirtualKeyShort> AllowedKeys =
        new Dictionary<string, VirtualKeyShort>(StringComparer.OrdinalIgnoreCase)
        {
            ["BACKSPACE"] = VirtualKeyShort.BACK,
            ["DELETE"] = VirtualKeyShort.DELETE,
            ["DOWN"] = VirtualKeyShort.DOWN,
            ["END"] = VirtualKeyShort.END,
            ["ENTER"] = VirtualKeyShort.RETURN,
            ["ESCAPE"] = VirtualKeyShort.ESCAPE,
            ["HOME"] = VirtualKeyShort.HOME,
            ["LEFT"] = VirtualKeyShort.LEFT,
            ["PAGEDOWN"] = VirtualKeyShort.NEXT,
            ["PAGEUP"] = VirtualKeyShort.PRIOR,
            ["RIGHT"] = VirtualKeyShort.RIGHT,
            ["SPACE"] = VirtualKeyShort.SPACE,
            ["TAB"] = VirtualKeyShort.TAB,
            ["UP"] = VirtualKeyShort.UP,
        };

    private readonly UIA3Automation _automation;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, ElementLocator> _locators = new(StringComparer.Ordinal);
    private bool _disposed;
    private long _generation;

    /// <summary>Initializes a new instance of the <see cref="FlaUiAutomationSession"/> class.</summary>
    /// <param name="automation">Owned UIA3 automation instance.</param>
    /// <param name="target">Exact process instance.</param>
    /// <param name="windowHandle">Attached native window handle.</param>
    public FlaUiAutomationSession(
        UIA3Automation automation,
        ProcessIdentity target,
        long windowHandle)
    {
        _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        ArgumentOutOfRangeException.ThrowIfEqual(windowHandle, 0);
        WindowHandle = windowHandle;
    }

    /// <inheritdoc/>
    public ProcessIdentity Target { get; }

    /// <inheritdoc/>
    public long WindowHandle { get; }

    /// <inheritdoc/>
    public async ValueTask<UiTreeSnapshot> InspectAsync(
        string sessionId,
        int maxDepth,
        int maxNodes,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDepth, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxNodes, 1);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var root = GetRoot();
            _locators.Clear();
            var generation = ++_generation;
            var nodes = new List<UiElementNode>(Math.Min(maxNodes, 256));
            var pending = new Queue<PendingElement>();
            pending.Enqueue(new PendingElement(root, null, [], 0));
            var truncated = false;

            while (pending.TryDequeue(out var current))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (nodes.Count >= maxNodes)
                {
                    truncated = true;
                    break;
                }

                var reference = $"e_{generation}_{nodes.Count}";
                var locator = new ElementLocator(current.Segments);
                _locators.Add(reference, locator);
                nodes.Add(CreateNode(current.Element, reference, current.ParentReference, current.Segments, current.Depth));

                var children = current.Element.FindAllChildren();
                if (children.Length == 0)
                {
                    continue;
                }

                if (current.Depth >= maxDepth)
                {
                    truncated = true;
                    continue;
                }

                for (var index = 0; index < children.Length; index++)
                {
                    var selector = CreateSelector(children, index);
                    var childSegments = current.Segments.Append(selector).ToArray();
                    pending.Enqueue(new PendingElement(children[index], reference, childSegments, current.Depth + 1));
                }
            }

            return new UiTreeSnapshot(sessionId, generation, nodes, truncated);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <inheritdoc/>
    public ValueTask<PlatformActionResult> InvokeAsync(
        string elementReference,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            elementReference,
            static element =>
            {
                var pattern = element.Patterns.Invoke.PatternOrDefault;
                if (pattern is null)
                {
                    return PlatformActionResult.Fail("pattern_not_supported", "The element does not support InvokePattern.");
                }

                pattern.Invoke();
                return PlatformActionResult.Ok("The element was invoked.");
            },
            cancellationToken);

    /// <inheritdoc/>
    public ValueTask<PlatformActionResult> ClickAsync(
        string elementReference,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            elementReference,
            static element =>
            {
                element.Click(false);
                return PlatformActionResult.Ok("The resolved element was clicked.");
            },
            cancellationToken);

    /// <inheritdoc/>
    public ValueTask<PlatformActionResult> SetValueAsync(
        string elementReference,
        string value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ExecuteAsync(
            elementReference,
            element =>
            {
                var pattern = element.Patterns.Value.PatternOrDefault;
                if (pattern is null)
                {
                    return PlatformActionResult.Fail("pattern_not_supported", "The element does not support ValuePattern.");
                }

                if (pattern.IsReadOnly.Value)
                {
                    return PlatformActionResult.Fail("read_only", "The element value is read-only.");
                }

                pattern.SetValue(value);
                return PlatformActionResult.Ok("The element value was set.");
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<PlatformActionResult> SelectAsync(
        string elementReference,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            elementReference,
            static element =>
            {
                var pattern = element.Patterns.SelectionItem.PatternOrDefault;
                if (pattern is null)
                {
                    return PlatformActionResult.Fail("pattern_not_supported", "The element does not support SelectionItemPattern.");
                }

                pattern.Select();
                return PlatformActionResult.Ok("The item was selected.");
            },
            cancellationToken);

    /// <inheritdoc/>
    public ValueTask<PlatformActionResult> SetExpandedAsync(
        string elementReference,
        bool expand,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            elementReference,
            element =>
            {
                var pattern = element.Patterns.ExpandCollapse.PatternOrDefault;
                if (pattern is null)
                {
                    return PlatformActionResult.Fail("pattern_not_supported", "The element does not support ExpandCollapsePattern.");
                }

                if (expand)
                {
                    pattern.Expand();
                    return PlatformActionResult.Ok("The element was expanded.");
                }

                pattern.Collapse();
                return PlatformActionResult.Ok("The element was collapsed.");
            },
            cancellationToken);

    /// <inheritdoc/>
    public ValueTask<PlatformActionResult> SendKeyAsync(
        string elementReference,
        string key,
        CancellationToken cancellationToken)
    {
        if (!AllowedKeys.TryGetValue(key, out var virtualKey))
        {
            return ValueTask.FromResult(
                PlatformActionResult.Fail(
                    "key_not_allowed",
                    $"Unsupported logical key. Allowed keys: {string.Join(", ", AllowedKeys.Keys)}."));
        }

        return ExecuteAsync(
            elementReference,
            element =>
            {
                element.Focus();
                Keyboard.Press(virtualKey);
                return PlatformActionResult.Ok($"Logical key {key.ToUpperInvariant()} was sent.");
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _locators.Clear();
        _automation.Dispose();
        _gate.Dispose();
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private static UiElementNode CreateNode(
        AutomationElement element,
        string reference,
        string? parentReference,
        IReadOnlyList<ElementSelector> segments,
        int depth)
    {
        var isPassword = UiaOperationGuard.Read(() => element.Properties.IsPassword.ValueOrDefault, false);
        var rectangle = UiaOperationGuard.Read(() => element.BoundingRectangle, System.Drawing.Rectangle.Empty);
        return new UiElementNode(
            reference,
            parentReference,
            segments.Count == 0 ? "$window" : "$window/" + string.Join("/", segments.Select(static segment => segment.Display)),
            depth,
            UiaOperationGuard.Read(() => element.ControlType.ToString(), "Unknown"),
            isPassword ? "[redacted]" : UiaOperationGuard.ReadString(() => element.Name),
            UiaOperationGuard.ReadString(() => element.AutomationId),
            UiaOperationGuard.ReadString(() => element.ClassName),
            UiaOperationGuard.ReadString(() => element.Properties.FrameworkId.ValueOrDefault),
            UiaOperationGuard.Read(() => element.IsEnabled, false),
            UiaOperationGuard.Read(() => element.IsOffscreen, true),
            isPassword,
            new UiRectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height),
            GetSupportedPatterns(element));
    }

    private static List<string> GetSupportedPatterns(AutomationElement element)
    {
        var patterns = new List<string>(6);
        AddIfSupported(patterns, "ExpandCollapse", () => element.Patterns.ExpandCollapse.IsSupported);
        AddIfSupported(patterns, "Invoke", () => element.Patterns.Invoke.IsSupported);
        AddIfSupported(patterns, "SelectionItem", () => element.Patterns.SelectionItem.IsSupported);
        AddIfSupported(patterns, "Toggle", () => element.Patterns.Toggle.IsSupported);
        AddIfSupported(patterns, "Value", () => element.Patterns.Value.IsSupported);
        AddIfSupported(patterns, "Window", () => element.Patterns.Window.IsSupported);
        return patterns;
    }

    private static void AddIfSupported(List<string> patterns, string name, Func<bool> isSupported)
    {
        if (UiaOperationGuard.Read(isSupported, false))
        {
            patterns.Add(name);
        }
    }

    private static ElementSelector CreateSelector(AutomationElement[] siblings, int index)
    {
        var identities = siblings.Select(ReadIdentity).ToArray();
        var identity = identities[index];
        var ordinal = ElementMatching.CountPriorMatches(identities, index, identity);
        return new ElementSelector(
            identity.ControlType,
            identity.AutomationId,
            identity.Name,
            identity.ClassName,
            ordinal);
    }

    private static ElementIdentity ReadIdentity(AutomationElement element) =>
        new(
            UiaOperationGuard.Read(() => element.ControlType.ToString(), "Unknown"),
            UiaOperationGuard.ReadString(() => element.AutomationId),
            UiaOperationGuard.ReadString(() => element.Name),
            UiaOperationGuard.ReadString(() => element.ClassName));

    private static bool Matches(AutomationElement element, ElementSelector selector) =>
        ElementMatching.Matches(
            ReadIdentity(element),
            new ElementIdentity(
                selector.ControlType,
                selector.AutomationId,
                selector.Name,
                selector.ClassName));

    private async ValueTask<PlatformActionResult> ExecuteAsync(
        string elementReference,
        Func<AutomationElement, PlatformActionResult> execute,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(elementReference);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_locators.TryGetValue(elementReference, out var locator))
            {
                return PlatformActionResult.Fail("stale_element", "The element reference is absent or stale; inspect the tree again.");
            }

            var element = Resolve(locator);
            if (element is null)
            {
                return PlatformActionResult.Fail("stale_element", "The element could not be resolved uniquely; inspect the tree again.");
            }

            if (!UiaOperationGuard.Read(() => element.IsEnabled, false))
            {
                return PlatformActionResult.Fail("element_disabled", "The element is disabled.");
            }

            var result = UiaOperationGuard.Execute(() => execute(element));
            if (result.Succeeded)
            {
                _locators.Clear();
                _generation++;
            }

            return result;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private AutomationElement? Resolve(ElementLocator locator)
    {
        var current = GetRoot();
        foreach (var selector in locator.Segments)
        {
            var matches = current.FindAllChildren()
                .Where(element => Matches(element, selector))
                .ToArray();
            if (selector.Ordinal >= matches.Length)
            {
                return null;
            }

            current = matches[selector.Ordinal];
        }

        return current;
    }

    private AutomationElement GetRoot()
    {
        var root = _automation.FromHandle(new IntPtr(WindowHandle));
        if (root.Properties.ProcessId.Value != Target.ProcessId)
        {
            throw new InvalidOperationException("The attached window handle no longer belongs to the consented process.");
        }

        return root;
    }

    private sealed record PendingElement(
        AutomationElement Element,
        string? ParentReference,
        IReadOnlyList<ElementSelector> Segments,
        int Depth);
}
