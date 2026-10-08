// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
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
    /// <summary>Error code returned when an element does not expose the required UIA pattern.</summary>
    private const string UnknownControlType = "Unknown";

    /// <summary>Error code for an unsupported pattern.</summary>
    private const string PatternNotSupportedCode = "pattern_not_supported";

    /// <summary>Initial capacity for a bounded tree snapshot.</summary>
    private const int SnapshotInitialCapacity = 256;

    /// <summary>Expected number of common UIA patterns on a single element.</summary>
    private const int SupportedPatternCapacity = 6;

    /// <summary>Logical keyboard keys that are safe and deterministic to send through UIA.</summary>
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

    /// <summary>Owned UIA3 client used for the lifetime of this session.</summary>
    private readonly UIA3Automation _automation;

    /// <summary>Serializes UIA calls because UIA providers are not reliably concurrent.</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Session-scoped opaque element references and their semantic locators.</summary>
    private readonly Dictionary<string, ElementLocator> _locators = [];

    /// <summary>Serializes operation registration and disposal.</summary>
    private readonly Lock _lifetimeLock = new();

    /// <summary>Shared completion of disposal.</summary>
    private Task? _disposalTask;

    /// <summary>Whether the owned automation client has been disposed.</summary>
    private volatile bool _disposed;

    /// <summary>Monotonically increasing generation used to invalidate stale references.</summary>
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

        await EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var root = GetRoot();
            _locators.Clear();
            var generation = ++_generation;
            var nodes = new List<UiElementNode>(Math.Min(maxNodes, SnapshotInitialCapacity));
            var pending = new Queue<PendingElement>();
            pending.Enqueue(new(root, null, [], 0));
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

                truncated |= EnqueueChildren(children, current, reference, maxNodes - nodes.Count - pending.Count, pending, cancellationToken);
            }

            return new(sessionId, generation, nodes, truncated);
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
                    return PlatformActionResult.Fail(PatternNotSupportedCode, "The element does not support InvokePattern.");
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
                var invoke = element.Patterns.Invoke.PatternOrDefault;
                if (invoke is not null)
                {
                    invoke.Invoke();
                    return PlatformActionResult.Ok("The resolved element was invoked without moving the pointer.");
                }

                var selection = element.Patterns.SelectionItem.PatternOrDefault;
                if (selection is not null)
                {
                    selection.Select();
                    return PlatformActionResult.Ok("The resolved element was selected without moving the pointer.");
                }

                var toggle = element.Patterns.Toggle.PatternOrDefault;
                if (toggle is not null)
                {
                    toggle.Toggle();
                    return PlatformActionResult.Ok("The resolved element was toggled without moving the pointer.");
                }

                element.Click();
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
                    return PlatformActionResult.Fail(PatternNotSupportedCode, "The element does not support ValuePattern.");
                }

                if (UiaOperationGuard.Read(() => pattern.IsReadOnly.Value, true))
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
                    return PlatformActionResult.Fail(PatternNotSupportedCode, "The element does not support SelectionItemPattern.");
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
                    return PlatformActionResult.Fail(PatternNotSupportedCode, "The element does not support ExpandCollapsePattern.");
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
        CancellationToken cancellationToken) =>
        !AllowedKeys.TryGetValue(key, out var virtualKey) ? ValueTask.FromResult(
                PlatformActionResult.Fail(
                    "key_not_allowed",
                    $"Unsupported logical key. Allowed keys: {string.Join(", ", AllowedKeys.Keys)}.")) : ExecuteAsync(
            elementReference,
            element =>
            {
                element.Focus();
                if (!UiaOperationGuard.Read(() => element.Properties.HasKeyboardFocus.ValueOrDefault, false)
                    || UiaOperationGuard.Read(() => _automation.FocusedElement().Properties.ProcessId.Value, 0) != Target.ProcessId)
                {
                    return PlatformActionResult.Fail("focus_failed", "Keyboard focus could not be verified on the target element; no key was sent.");
                }

                Keyboard.Press(virtualKey);
                return PlatformActionResult.Ok($"Logical key {key.ToUpperInvariant()} was sent.");
            },
            cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Task completion;
        lock (_lifetimeLock)
        {
            if (_disposalTask is null)
            {
                _disposed = true;
                _disposalTask = DisposeCoreAsync();
            }

            completion = _disposalTask;
        }

        return new(completion);
    }

    /// <summary>Enqueues only children that fit within the remaining snapshot budget.</summary>
    /// <param name="children">Provider children.</param>
    /// <param name="current">Parent traversal data.</param>
    /// <param name="reference">Parent reference.</param>
    /// <param name="remaining">Remaining node budget.</param>
    /// <param name="pending">Traversal queue.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Whether children exceeded the budget.</returns>
    private static bool EnqueueChildren(
        AutomationElement[] children,
        PendingElement current,
        string reference,
        int remaining,
        Queue<PendingElement> pending,
        CancellationToken cancellationToken)
    {
        var childCount = Math.Min(children.Length, remaining);
        var identities = new ElementIdentity[childCount];
        for (var index = 0; index < childCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            identities[index] = ReadIdentity(children[index]);
        }

        var selectors = ElementMatching.CreateSelectors(identities);
        for (var index = 0; index < childCount; index++)
        {
            var childSegments = AppendSegment(current.Segments, selectors[index]);
            pending.Enqueue(new(children[index], reference, childSegments, current.Depth + 1));
        }

        return childCount < children.Length;
    }

    /// <summary>Creates a safe, serializable snapshot node for a UIA element.</summary>
    /// <param name="element">UIA element to inspect.</param>
    /// <param name="reference">Opaque session reference.</param>
    /// <param name="parentReference">Opaque parent reference.</param>
    /// <param name="segments">Semantic path segments.</param>
    /// <param name="depth">Depth from the attached window root.</param>
    /// <returns>The serializable node.</returns>
    private static UiElementNode CreateNode(
        AutomationElement element,
        string reference,
        string? parentReference,
        IReadOnlyList<ElementSelector> segments,
        int depth)
    {
        var isPassword = UiaOperationGuard.Read(() => element.Properties.IsPassword.ValueOrDefault, false);
        var rectangle = UiaOperationGuard.Read(() => element.BoundingRectangle, System.Drawing.Rectangle.Empty);
        var stablePath = "$window";
        if (segments.Count > 0)
        {
            var displays = new string[segments.Count];
            for (var index = 0; index < segments.Count; index++)
            {
                displays[index] = segments[index].Display;
            }

            stablePath = $"$window/{string.Join("/", displays)}";
        }

        return new(
            reference,
            parentReference,
            stablePath,
            depth,
            UiaOperationGuard.Read(() => element.ControlType.ToString(), UnknownControlType),
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

    /// <summary>Lists common UIA patterns reported as supported by an element.</summary>
    /// <param name="element">Element to inspect.</param>
    /// <returns>Supported pattern names.</returns>
    private static List<string> GetSupportedPatterns(AutomationElement element)
    {
        var patterns = new List<string>(SupportedPatternCapacity);
        AddIfSupported(patterns, "ExpandCollapse", () => element.Patterns.ExpandCollapse.IsSupported);
        AddIfSupported(patterns, "Invoke", () => element.Patterns.Invoke.IsSupported);
        AddIfSupported(patterns, "SelectionItem", () => element.Patterns.SelectionItem.IsSupported);
        AddIfSupported(patterns, "Toggle", () => element.Patterns.Toggle.IsSupported);
        AddIfSupported(patterns, "Value", () => element.Patterns.Value.IsSupported);
        AddIfSupported(patterns, nameof(Window), () => element.Patterns.Window.IsSupported);
        return patterns;
    }

    /// <summary>Adds a pattern name when a provider reports that pattern as supported.</summary>
    /// <param name="patterns">Target pattern list.</param>
    /// <param name="name">Pattern name.</param>
    /// <param name="isSupported">Deferred provider capability check.</param>
    private static void AddIfSupported(List<string> patterns, string name, Func<bool> isSupported)
    {
        if (UiaOperationGuard.Read(isSupported, false))
        {
            patterns.Add(name);
        }
    }

    /// <summary>Appends a semantic selector without allocating through LINQ.</summary>
    /// <param name="segments">Existing path segments.</param>
    /// <param name="selector">Selector to append.</param>
    /// <returns>A new path including <paramref name="selector"/>.</returns>
    private static ElementSelector[] AppendSegment(IReadOnlyList<ElementSelector> segments, ElementSelector selector)
    {
        var appended = new ElementSelector[segments.Count + 1];
        for (var index = 0; index < segments.Count; index++)
        {
            appended[index] = segments[index];
        }

        appended[^1] = selector;
        return appended;
    }

    /// <summary>Reads provider-neutral identity values from a UIA element.</summary>
    /// <param name="element">Element to inspect.</param>
    /// <returns>The semantic identity.</returns>
    private static ElementIdentity ReadIdentity(AutomationElement element) =>
        new(
            UiaOperationGuard.Read(() => element.ControlType.ToString(), UnknownControlType),
            UiaOperationGuard.ReadString(() => element.AutomationId),
            UiaOperationGuard.ReadString(() => element.Name),
            UiaOperationGuard.ReadString(() => element.ClassName));

    /// <summary>Determines whether a UIA element matches a semantic selector.</summary>
    /// <param name="element">Candidate UIA element.</param>
    /// <param name="selector">Expected semantic selector.</param>
    /// <returns><see langword="true"/> when the candidate matches.</returns>
    private static bool Matches(AutomationElement element, ElementSelector selector)
    {
        if (!string.Equals(UiaOperationGuard.Read(() => element.ControlType.ToString(), UnknownControlType), selector.ControlType, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(selector.AutomationId))
        {
            return string.Equals(UiaOperationGuard.ReadString(() => element.AutomationId), selector.AutomationId, StringComparison.Ordinal);
        }

        return !string.IsNullOrEmpty(selector.Name)
            ? string.Equals(UiaOperationGuard.ReadString(() => element.Name), selector.Name, StringComparison.Ordinal)
            : string.Equals(UiaOperationGuard.ReadString(() => element.ClassName), selector.ClassName, StringComparison.Ordinal);
    }

    /// <summary>Registers a serialized operation before disposal can enqueue behind it.</summary>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The gate acquisition task.</returns>
    private Task EnterAsync(CancellationToken cancellationToken)
    {
        Task acquisition;
        lock (_lifetimeLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            acquisition = _gate.WaitAsync(cancellationToken);
        }

        return acquisition;
    }

    /// <summary>Drains registered operations before disposing owned resources.</summary>
    /// <returns>The disposal completion task.</returns>
    private async Task DisposeCoreAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _locators.Clear();
            _automation.Dispose();
        }
        finally
        {
            _gate.Dispose();
        }
    }

    /// <summary>Resolves an opaque reference and executes a serialized UIA action.</summary>
    /// <param name="elementReference">Opaque element reference from the latest inspection.</param>
    /// <param name="execute">Action applied to the resolved element.</param>
    /// <param name="cancellationToken">Operation cancellation token.</param>
    /// <returns>The deterministic action result.</returns>
    private async ValueTask<PlatformActionResult> ExecuteAsync(
        string elementReference,
        Func<AutomationElement, PlatformActionResult> execute,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(elementReference);
        await EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
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

    /// <summary>Re-resolves an opaque locator against the current UIA tree.</summary>
    /// <param name="locator">Session-scoped semantic locator.</param>
    /// <returns>The matching UIA element or <see langword="null"/> when stale.</returns>
    private AutomationElement? Resolve(ElementLocator locator)
    {
        var current = GetRoot();
        foreach (var selector in locator.Segments)
        {
            AutomationElement? selected = null;
            var ordinal = 0;
            var children = current.FindAllChildren();
            foreach (var child in children)
            {
                if (Matches(child, selector))
                {
                    if (ordinal == selector.Ordinal)
                    {
                        selected = child;
                        break;
                    }

                    ordinal++;
                }
            }

            if (selected is null)
            {
                return null;
            }

            current = selected;
        }

        return current;
    }

    /// <summary>Gets the attached window root and verifies its process ownership.</summary>
    /// <returns>The current attached window root.</returns>
    private AutomationElement GetRoot()
    {
        var root = _automation.FromHandle(new(WindowHandle));
        if (root.Properties.ProcessId.Value != Target.ProcessId)
        {
            throw new InvalidOperationException("The attached window handle no longer belongs to the consented process.");
        }

        return root;
    }

    /// <summary>Queued tree element together with the data needed to produce a snapshot node.</summary>
    /// <param name="Element">UIA element to inspect.</param>
    /// <param name="ParentReference">Opaque parent reference.</param>
    /// <param name="Segments">Semantic path segments.</param>
    /// <param name="Depth">Depth from the attached root.</param>
    private sealed record PendingElement(
        AutomationElement Element,
        string? ParentReference,
        IReadOnlyList<ElementSelector> Segments,
        int Depth);
}
