// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace ProDataGrid.SourceGenerators;

internal interface IViewGenerationStrategy
{
    string GetBaseType(ViewModelViewModel model);
}

internal static class ViewGenerationStrategyRegistry
{
    private static readonly IViewGenerationStrategy s_avalonia = new AvaloniaViewGenerationStrategy();
    private static readonly IViewGenerationStrategy s_reactiveUi = new ReactiveUiViewGenerationStrategy();

    public static IViewGenerationStrategy Get(ViewFrameworkModel framework) =>
        framework == ViewFrameworkModel.ReactiveUI ? s_reactiveUi : s_avalonia;
}

internal sealed class AvaloniaViewGenerationStrategy : IViewGenerationStrategy
{
    public string GetBaseType(ViewModelViewModel model) => model.BaseType == null
        ? "global::Avalonia.Controls.UserControl"
        : model.BaseType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
}

internal sealed class ReactiveUiViewGenerationStrategy : IViewGenerationStrategy
{
    public string GetBaseType(ViewModelViewModel model)
    {
        if (model.BaseType != null)
        {
            return model.BaseType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        }

        string viewModelType = model.ViewModelType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        return "global::ReactiveUI.Avalonia.ReactiveUserControl<" + viewModelType + ">";
    }
}
