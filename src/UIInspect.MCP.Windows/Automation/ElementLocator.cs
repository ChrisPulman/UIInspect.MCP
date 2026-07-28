// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
namespace UIInspect.MCP.Windows.Automation;

/// <summary>One semantic selector segment beneath the attached window.</summary>
/// <param name="ControlType">Expected control type.</param>
/// <param name="AutomationId">Preferred stable automation identifier.</param>
/// <param name="Name">Fallback accessible name.</param>
/// <param name="ClassName">Fallback provider class.</param>
/// <param name="Ordinal">Zero-based ordinal among matching siblings.</param>
internal sealed record ElementSelector(
    string ControlType,
    string AutomationId,
    string Name,
    string ClassName,
    int Ordinal)
{
    /// <summary>Gets a stable explanatory segment without exposing it as the action identifier.</summary>
    public string Display =>
        !string.IsNullOrEmpty(AutomationId)
            ? $"{ControlType}[id=\"{Escape(AutomationId)}\"]"
            : !string.IsNullOrEmpty(Name)
                ? $"{ControlType}[name=\"{Escape(Name)}\",#{Ordinal}]"
                : $"{ControlType}[class=\"{Escape(ClassName)}\",#{Ordinal}]";

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}

/// <summary>Immutable semantic route to an element.</summary>
/// <param name="Segments">Ordered selectors below the attached root.</param>
internal sealed record ElementLocator(IReadOnlyList<ElementSelector> Segments);

/// <summary>Provider-neutral identity values used to compare semantic siblings.</summary>
internal sealed record ElementIdentity(
    string ControlType,
    string AutomationId,
    string Name,
    string ClassName);

/// <summary>Pure semantic selector matching shared by inspection and reference resolution.</summary>
internal static class ElementMatching
{
    internal static bool Matches(ElementIdentity element, ElementIdentity expected)
    {
        if (!string.Equals(element.ControlType, expected.ControlType, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(expected.AutomationId))
        {
            return string.Equals(element.AutomationId, expected.AutomationId, StringComparison.Ordinal);
        }

        if (!string.IsNullOrEmpty(expected.Name))
        {
            return string.Equals(element.Name, expected.Name, StringComparison.Ordinal);
        }

        return string.Equals(element.ClassName, expected.ClassName, StringComparison.Ordinal);
    }

    internal static int CountPriorMatches(
        IReadOnlyList<ElementIdentity> siblings,
        int index,
        ElementIdentity expected)
    {
        var matches = 0;
        for (var siblingIndex = 0; siblingIndex < index; siblingIndex++)
        {
            if (Matches(siblings[siblingIndex], expected))
            {
                matches++;
            }
        }

        return matches;
    }
}
