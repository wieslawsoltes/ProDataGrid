#!/usr/bin/env python3
"""Build a fresh external XAML consumer of the four local NuGet packages, without project references."""
from __future__ import annotations
import argparse, base64, hashlib, json, os, re, subprocess, tempfile, zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

PROGRAM = r'''using System;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ProCharts;
using ProCharts.Controls;
using ProCharts.Avalonia;

namespace Consumer;
public sealed class ConsumerApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Consumer/"))
        { Source = new Uri("avares://ProCharts.Controls/Themes/Generic.axaml") });
    }
}
public sealed partial class ConsumerView : UserControl
{
    public ConsumerView() => InitializeComponent();
}
internal static class Program
{
    [STAThread]
    public static void Main()
    {
        AppBuilder.Configure<ConsumerApp>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        using ChartModel model = new() { DataSource = new Source() };
        ConsumerView view = new();
        ProChart chart = view.FindControl<ProChart>("Chart")!;
        chart.Model = model;
        Window window = new() { Width = 1000, Height = 700, Content = view };
        int checks = 0;
        void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException(name); checks++; Console.WriteLine("PASS " + name); }
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Check(chart.Title == "External XAML consumer", "compiled XAML uses packaged chart type");
            Check(chart.HasData, "packaged core model feeds control");
            Check(chart.GetVisualDescendants().OfType<ProChartView>().Count() == 1, "packaged plot resource");
            Check(chart.GetVisualDescendants().OfType<ProChartToolbar>().Count() == 1, "packaged toolbar resource");
            Check(chart.GetVisualDescendants().OfType<ProChartLegend>().Count() == 1, "packaged legend resource");
            Check(chart.GetVisualDescendants().OfType<ProChartNavigator>().Count() == 1, "packaged navigator resource");
            Check(chart.GetVisualDescendants().OfType<ProChartToolTip>().Count() == 1, "packaged tooltip resource");
            chart.ZoomInCommand.Execute(null);
            model.TryGetVisibleWindow(out int total, out _, out int count);
            Check(count < total, "packaged viewport command");
            chart.ResetViewCommand.Execute(null);
            Check(chart.CreateExport(ChartExportFormat.Png).Data.Take(8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}), "packaged native PNG encoder");
            Check(Encoding.UTF8.GetString(chart.CreateExport(ChartExportFormat.Svg).Data).Contains("<svg"), "packaged SVG exporter");
            Check(Encoding.UTF8.GetString(chart.CreateExport(ChartExportFormat.Csv).Data).Contains("Series,Point"), "packaged CSV exporter");
            Console.WriteLine("CONSUMER_CHECKS=" + checks);
        }
        finally { window.Close(); }
    }
    private sealed class Source : IChartDataSource
    {
        public event EventHandler? DataInvalidated { add { } remove { } }
        private readonly ChartDataSnapshot _snapshot = new(
            Enumerable.Range(0, 40).Select(i => (string?)i.ToString()).ToArray(),
            new[] { new ChartSeriesSnapshot("Signal", ChartSeriesKind.Line,
                Enumerable.Range(0, 40).Select(i => (double?)(30 + 20 * Math.Sin(i * .2))).ToArray()) });
        public ChartDataSnapshot BuildSnapshot(ChartDataRequest request) => _snapshot;
    }
}
'''


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packages', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    feed = args.packages.resolve(); output = args.output.resolve(); output.mkdir(parents=True, exist_ok=True)
    required = {'ProCharts', 'ProCharts.Skia', 'ProCharts.Avalonia', 'ProCharts.Controls'}
    packages: dict[str, tuple[str, Path]] = {}
    for path in feed.glob('*.nupkg'):
        with zipfile.ZipFile(path) as archive:
            nuspec = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))))
            ident = nuspec.findtext('.//{*}metadata/{*}id'); version = nuspec.findtext('.//{*}metadata/{*}version')
            if ident in required:
                assert ident not in packages, f'Duplicate package {ident}'
                assert version
                for tfm in ('net8.0', 'net10.0'):
                    assert f'lib/{tfm}/{ident}.dll' in archive.namelist(), (ident, tfm)
                packages[ident] = (version, path)
    assert set(packages) == required, packages
    versions = {value[0] for value in packages.values()}; assert len(versions) == 1, versions
    version = next(iter(versions))
    result: dict = {'passed': False, 'version': version, 'packages': {}, 'commands': []}
    try:
        with tempfile.TemporaryDirectory(prefix='procharts-external-consumer-') as directory:
            root = Path(directory)
            (root/'Consumer.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFrameworks>net8.0;net10.0</TargetFrameworks><Nullable>enable</Nullable><AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault></PropertyGroup>
<ItemGroup><PackageReference Include="ProCharts.Controls" Version="{version}" />
<PackageReference Include="Avalonia.Headless" Version="12.1.0" /><PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.0" />
<PackageReference Include="SkiaSharp.NativeAssets.Linux" Version="3.119.4" /></ItemGroup></Project>''')
            config = ET.Element('configuration'); sources = ET.SubElement(config, 'packageSources'); ET.SubElement(sources, 'clear')
            ET.SubElement(sources, 'add', key='local', value=str(feed))
            ET.SubElement(sources, 'add', key='nuget', value='https://api.nuget.org/v3/index.json')
            mapping = ET.SubElement(config, 'packageSourceMapping'); local = ET.SubElement(mapping, 'packageSource', key='local')
            for pattern in ('ProCharts', 'ProCharts.*'): ET.SubElement(local, 'package', pattern=pattern)
            ET.SubElement(ET.SubElement(mapping, 'packageSource', key='nuget'), 'package', pattern='*')
            ET.ElementTree(config).write(root/'NuGet.Config', encoding='utf-8', xml_declaration=True)
            (root/'Program.cs').write_text(PROGRAM)
            (root/'ConsumerView.axaml').write_text('''<UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:c="using:ProCharts.Controls" x:Class="Consumer.ConsumerView"><c:ProChart Name="Chart" Title="External XAML consumer" /></UserControl>''')
            env = dict(os.environ, NUGET_PACKAGES=str(root/'cache'))
            commands = [
                ['dotnet','build','Consumer.csproj','-c','Release','-f','net8.0'],
                ['dotnet','run','--project','Consumer.csproj','-c','Release','-f','net10.0']
            ]
            with (output/'consumer.log').open('w') as log:
                for command in commands:
                    completed = subprocess.run(command, cwd=root, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=300)
                    log.write('$ ' + ' '.join(command) + '\n' + completed.stdout + '\n'); log.flush()
                    result['commands'].append({'command': command, 'exitCode': completed.returncode})
                    if completed.returncode:
                        print(completed.stdout[-16000:], flush=True)
                        raise RuntimeError('External package consumer failed')
                    match = re.search(r'CONSUMER_CHECKS=(\d+)', completed.stdout)
                    if match: result['runtimeChecks'] = int(match.group(1))
            assert result.get('runtimeChecks',0) >= 11
            for ident, (v, path) in packages.items():
                installed = root/'cache'/ident.lower()/v.lower()/(ident.lower()+'.'+v.lower()+'.nupkg.sha512')
                digest = base64.b64encode(hashlib.sha512(path.read_bytes()).digest()).decode()
                assert installed.read_text().strip() == digest, f'{ident} did not restore the exact local package bytes'
                result['packages'][ident] = {'version':v,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'restoredExactBytes':True}
            result['passed'] = True
    finally:
        (output/'consumer.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result),flush=True)

if __name__ == '__main__': main()
