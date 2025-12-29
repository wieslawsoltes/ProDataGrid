using System;

namespace ProDiagnostics.Viewer.ViewModels;

public sealed class TrendRangeOption
{
    public TrendRangeOption(string title, TimeSpan range)
    {
        Title = title;
        Range = range;
    }

    public string Title { get; }

    public TimeSpan Range { get; }
}
