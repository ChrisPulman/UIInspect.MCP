// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
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
    internal string Display
    {
        get
        {
            if (!string.IsNullOrEmpty(AutomationId))
            {
                return $"{ControlType}[id=\"{Escape(AutomationId)}\",#{Ordinal}]";
            }

            return !string.IsNullOrEmpty(Name)
                ? $"{ControlType}[name=\"{Escape(Name)}\",#{Ordinal}]"
                : $"{ControlType}[class=\"{Escape(ClassName)}\",#{Ordinal}]";
        }
    }

    /// <summary>Escapes a selector literal for a stable, human-readable path.</summary>
    /// <param name="value">Unescaped selector value.</param>
    /// <returns>The escaped value.</returns>
    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
