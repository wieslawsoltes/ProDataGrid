using ProDiagnostics.Transport;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Transport;

public class WildcardMatcherTests
{
    [Theory]
    [InlineData("ProDataGrid.DataGrid.RefreshRows", "ProDataGrid.*", true)]
    [InlineData("prodatagrid.refresh.time", "prodatagrid.*.time", true)]
    [InlineData("ProDataGrid.DataGrid.RefreshRows", "*.Selection*", false)]
    [InlineData("Metrics", "*", true)]
    [InlineData("Metrics", "", true)]
    public void Matches_Wildcards(string value, string pattern, bool expected)
    {
        Assert.Equal(expected, WildcardMatcher.IsMatch(value, pattern));
    }
}
