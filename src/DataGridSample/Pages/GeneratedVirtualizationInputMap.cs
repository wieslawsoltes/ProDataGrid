// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Controls;
using Avalonia.Input;

namespace DataGridSample.Pages;

public sealed class GeneratedVirtualizationInputMap : IDataGridGeneratedInputMap
{
    private readonly IDataGridGeneratedInputMap _commands = DataGridGeneratedInputMap.Create(
        DataGridGeneratedPerformanceProfile.Spreadsheet);

    public DataGridKeyboardGestures CreateKeyboardGestureOverrides(KeyModifiers commandModifiers) =>
        new()
        {
            MoveUp = new KeyGesture(Key.K),
            MoveDown = new KeyGesture(Key.J),
            BeginEdit = new KeyGesture(Key.Enter)
        };

    public bool TryMatch(
        Key key,
        KeyModifiers keyModifiers,
        KeyModifiers commandModifiers,
        out DataGridGeneratedInputAction action) =>
        _commands.TryMatch(key, keyModifiers, commandModifiers, out action);
}
