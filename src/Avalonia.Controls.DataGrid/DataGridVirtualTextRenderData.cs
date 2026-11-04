// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Rendering.SceneGraph;

namespace Avalonia.Controls;

internal sealed class DataGridVirtualTextLayout : IDisposable
{
    public DataGridVirtualTextLayout(TextLayout layout)
    {
        Layout = layout;
        RenderData = DataGridVirtualTextRenderData.TryCreate(layout);
    }

    public TextLayout Layout { get; }

    public DataGridVirtualTextRenderData? RenderData { get; }

    public void Dispose()
    {
        Layout.Dispose();
        RenderData?.Release();
    }
}

internal sealed class DataGridVirtualTextRenderData
{
    private readonly GlyphRunData[] _glyphRuns;
    private int _referenceCount = 1;

    private DataGridVirtualTextRenderData(GlyphRunData[] glyphRuns)
    {
        _glyphRuns = glyphRuns;
    }

    public int GlyphRunCount => _glyphRuns.Length;

    internal int ReferenceCount => Volatile.Read(ref _referenceCount);

    public static DataGridVirtualTextRenderData? TryCreate(TextLayout layout)
    {
        var glyphRuns = new List<GlyphRunData>(layout.TextLines.Count);
        double currentY = 0d;

        try
        {
            for (int lineIndex = 0; lineIndex < layout.TextLines.Count; lineIndex++)
            {
                TextLine line = layout.TextLines[lineIndex];
                double currentX = line.Start;
                IReadOnlyList<TextRun> textRuns = line.TextRuns;
                for (int runIndex = 0; runIndex < textRuns.Count; runIndex++)
                {
                    if (textRuns[runIndex] is not ShapedTextRun shapedRun)
                    {
                        if (textRuns[runIndex] is DrawableTextRun)
                        {
                            DisposeGlyphRuns(glyphRuns);
                            return null;
                        }

                        continue;
                    }

                    TextRunProperties properties = shapedRun.Properties;
                    if (properties.BackgroundBrush is not null || properties.TextDecorations is not null)
                    {
                        DisposeGlyphRuns(glyphRuns);
                        return null;
                    }

                    if (shapedRun.GlyphRun.GlyphInfos.Count != 0 &&
                        properties.Typeface != default &&
                        properties.ForegroundBrush is not null)
                    {
                        IImmutableGlyphRunReference? glyphRun = shapedRun.GlyphRun.TryCreateImmutableGlyphRunReference();
                        if (glyphRun is null)
                        {
                            DisposeGlyphRuns(glyphRuns);
                            return null;
                        }

                        IImmutableBrush? foreground = properties.ForegroundBrush switch
                        {
                            IImmutableBrush immutableBrush => immutableBrush,
                            IMutableBrush mutableBrush => mutableBrush.ToImmutable(),
                            _ => null,
                        };
                        if (foreground is null)
                        {
                            glyphRun.Dispose();
                            DisposeGlyphRuns(glyphRuns);
                            return null;
                        }

                        glyphRuns.Add(new GlyphRunData(
                            foreground,
                            glyphRun,
                            new Point(currentX, currentY + GetBaselineOffset(line, shapedRun))));
                    }

                    currentX += shapedRun.Size.Width;
                }

                currentY += line.Height;
            }

            return glyphRuns.Count == 0
                ? null
                : new DataGridVirtualTextRenderData(glyphRuns.ToArray());
        }
        catch
        {
            DisposeGlyphRuns(glyphRuns);
            throw;
        }
    }

    public void AddReference()
    {
        int count = Volatile.Read(ref _referenceCount);
        while (count != 0)
        {
            int observed = Interlocked.CompareExchange(ref _referenceCount, count + 1, count);
            if (observed == count)
            {
                return;
            }

            count = observed;
        }

        throw new ObjectDisposedException(nameof(DataGridVirtualTextRenderData));
    }

    public void Release()
    {
        if (Interlocked.Decrement(ref _referenceCount) != 0)
        {
            return;
        }

        for (int index = 0; index < _glyphRuns.Length; index++)
        {
            _glyphRuns[index].GlyphRun.Dispose();
        }
    }

    public void Render(ImmediateDrawingContext context, Point origin)
    {
        for (int index = 0; index < _glyphRuns.Length; index++)
        {
            GlyphRunData glyphRun = _glyphRuns[index];
            Point translatedOrigin = origin + glyphRun.Origin;
            using (context.PushPreTransform(Matrix.CreateTranslation(translatedOrigin.X, translatedOrigin.Y)))
            {
                context.DrawGlyphRun(glyphRun.Foreground, glyphRun.GlyphRun);
            }
        }
    }

    private static double GetBaselineOffset(TextLine line, DrawableTextRun run)
    {
        double baseline = run.Baseline;
        double offset = -baseline;
        switch (run.Properties?.BaselineAlignment)
        {
            case BaselineAlignment.Baseline:
                return offset + line.Baseline;
            case BaselineAlignment.Top:
            case BaselineAlignment.TextTop:
                return offset + line.Height - line.Extent + (run.Size.Height / 2d);
            case BaselineAlignment.Center:
                return offset + (line.Height / 2d) + baseline - (run.Size.Height / 2d);
            case BaselineAlignment.Subscript:
            case BaselineAlignment.Bottom:
            case BaselineAlignment.TextBottom:
                return offset + line.Height - run.Size.Height + baseline;
            case BaselineAlignment.Superscript:
                return offset + baseline;
            default:
                throw new ArgumentOutOfRangeException(nameof(run), run.Properties?.BaselineAlignment, null);
        }
    }

    private static void DisposeGlyphRuns(List<GlyphRunData> glyphRuns)
    {
        for (int index = 0; index < glyphRuns.Count; index++)
        {
            glyphRuns[index].GlyphRun.Dispose();
        }
    }

    private readonly record struct GlyphRunData(
        IImmutableBrush Foreground,
        IImmutableGlyphRunReference GlyphRun,
        Point Origin);
}

internal readonly record struct DataGridVirtualTextDrawCommand(
    DataGridVirtualTextRenderData RenderData,
    Point Origin,
    Rect? Clip);

internal sealed class DataGridVirtualTextDrawOperation : ICustomDrawOperation
{
    private DataGridVirtualTextDrawCommand[]? _commands;
    private int _count;

    public DataGridVirtualTextDrawOperation(
        Rect bounds,
        List<DataGridVirtualTextDrawCommand> commands)
    {
        Bounds = bounds;
        _count = commands.Count;
        _commands = ArrayPool<DataGridVirtualTextDrawCommand>.Shared.Rent(_count);
        int copied = 0;
        try
        {
            for (; copied < _count; copied++)
            {
                DataGridVirtualTextDrawCommand command = commands[copied];
                command.RenderData.AddReference();
                _commands[copied] = command;
            }
        }
        catch
        {
            for (int index = 0; index < copied; index++)
            {
                _commands[index].RenderData.Release();
            }

            ArrayPool<DataGridVirtualTextDrawCommand>.Shared.Return(_commands, clearArray: true);
            _commands = null;
            _count = 0;
            throw;
        }
    }

    public Rect Bounds { get; }

    public bool HitTest(Point point) => false;

    public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);

    public void Render(ImmediateDrawingContext context)
    {
        DataGridVirtualTextDrawCommand[]? commands = _commands;
        if (commands is null)
        {
            return;
        }

        for (int index = 0; index < _count; index++)
        {
            DataGridVirtualTextDrawCommand command = commands[index];
            if (command.Clip is { } clip)
            {
                using (context.PushClip(clip))
                {
                    command.RenderData.Render(context, command.Origin);
                }
            }
            else
            {
                command.RenderData.Render(context, command.Origin);
            }
        }
    }

    public void Dispose()
    {
        DataGridVirtualTextDrawCommand[]? commands = Interlocked.Exchange(ref _commands, null);
        if (commands is null)
        {
            return;
        }

        int count = _count;
        _count = 0;
        for (int index = 0; index < count; index++)
        {
            commands[index].RenderData.Release();
        }

        ArrayPool<DataGridVirtualTextDrawCommand>.Shared.Return(commands, clearArray: true);
    }
}
