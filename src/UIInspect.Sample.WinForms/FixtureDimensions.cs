// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Drawing;

namespace UIInspect.Sample.WinForms;

/// <summary>Centralizes deterministic fixture dimensions.</summary>
internal static class FixtureDimensions
{
    /// <summary>The primary editable control width.</summary>
    internal const int ControlWidth = 430;

    /// <summary>The content padding.</summary>
    internal const int ContentPadding = 16;

    /// <summary>The group padding.</summary>
    internal const int GroupPadding = 12;

    /// <summary>The navigation tree height.</summary>
    internal const int TreeHeight = 150;

    /// <summary>The header and footer height.</summary>
    internal const int HeaderFooterHeight = 48;

    /// <summary>The header font size.</summary>
    internal const int HeaderFontSize = 16;

    /// <summary>The layout's number of rows.</summary>
    internal const int LayoutRows = 3;

    /// <summary>The layout's number of columns.</summary>
    internal const int LayoutColumns = 1;

    /// <summary>The header row index.</summary>
    internal const int HeaderRow = 0;

    /// <summary>The controls row index.</summary>
    internal const int ControlsRow = 1;

    /// <summary>The footer row index.</summary>
    internal const int FooterRow = 2;

    /// <summary>The only layout column index.</summary>
    internal const int OnlyColumn = 0;

    /// <summary>The percentage allocated to the controls row.</summary>
    internal const float ContentPercent = 100F;

    /// <summary>The fixture form width.</summary>
    internal const int FormWidth = 520;

    /// <summary>The fixture form height.</summary>
    internal const int FormHeight = 620;

    /// <summary>The fixture form's client size.</summary>
    internal static readonly Size FormClientSize = new(FormWidth, FormHeight);
}
