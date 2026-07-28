// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Windows.Automation;

/// <summary>Pure semantic selector matching shared by inspection and reference resolution.</summary>
internal static class ElementMatching
{
    /// <summary>Determines whether a candidate identity satisfies a semantic selector identity.</summary>
    /// <param name="element">Candidate element identity.</param>
    /// <param name="expected">Expected selector identity.</param>
    /// <returns><see langword="true"/> when the candidate matches.</returns>
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

        return !string.IsNullOrEmpty(expected.Name)
            ? string.Equals(element.Name, expected.Name, StringComparison.Ordinal)
            : string.Equals(element.ClassName, expected.ClassName, StringComparison.Ordinal);
    }

    /// <summary>Counts semantically matching siblings preceding a selected position.</summary>
    /// <param name="siblings">Sibling identities.</param>
    /// <param name="index">Selected sibling position.</param>
    /// <param name="expected">Identity to match.</param>
    /// <returns>The zero-based ordinal among matching siblings.</returns>
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
