// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Security;

/// <summary>Defines the fixed unattended approval windows offered to users.</summary>
public static class UnattendedApprovalDurations
{
    /// <summary>Supported whole-hour durations.</summary>
    private static readonly int[] AllowedHours = [1, 2, 5, 8, 12, 24];

    /// <summary>Gets the supported whole-hour durations.</summary>
    public static IReadOnlyList<int> SupportedHours => AllowedHours;

    /// <summary>Determine whether a whole-hour duration is one of the fixed choices.</summary>
    /// <param name="hours">Requested hours.</param>
    /// <returns><see langword="true"/> when the duration is supported.</returns>
    public static bool IsSupported(int hours) => Array.IndexOf(AllowedHours, hours) >= 0;

    /// <summary>Convert a supported whole-hour choice into a duration.</summary>
    /// <param name="hours">Requested hours.</param>
    /// <returns>Validated duration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The choice is not one of the supported windows.</exception>
    public static TimeSpan FromHours(int hours)
    {
        if (!IsSupported(hours))
        {
            throw new ArgumentOutOfRangeException(
                nameof(hours),
                hours,
                "Approval hours must be one of: 1, 2, 5, 8, 12, or 24.");
        }

        return TimeSpan.FromHours(hours);
    }
}
