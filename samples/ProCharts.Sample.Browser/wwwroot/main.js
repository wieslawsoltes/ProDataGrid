import { dotnet } from './_framework/dotnet.js';

// This page only hosts the .NET/Avalonia application. There is no HTML chart implementation.
globalThis.chartDownloads = [];
try {
    const runtime = await dotnet.withDiagnosticTracing(false).create();
    runtime.setModuleImports('chart-host', {
        save(name, mimeType, base64) {
            const binary = atob(base64);
            const bytes = Uint8Array.from(binary, c => c.charCodeAt(0));
            const url = URL.createObjectURL(new Blob([bytes], { type: mimeType }));
            const link = document.createElement('a');
            link.href = url; link.download = name; link.style.display = 'none';
            document.body.appendChild(link); link.click(); link.remove();
            setTimeout(() => URL.revokeObjectURL(url), 10000);
            globalThis.chartDownloads.push({ name, mimeType, length: bytes.length });
        }
    });
    const config = runtime.getConfig();
    const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
    globalThis.proCharts = exports.ProCharts.Sample.Browser.Program;
    await runtime.runMain(config.mainAssemblyName, []);
} catch (error) {
    console.error(error);
    const message = document.createElement('pre');
    message.id = 'boot-error'; message.textContent = String(error?.stack ?? error);
    document.body.appendChild(message);
    globalThis.chartBootError = message.textContent;
}
