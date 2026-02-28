// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.SceneGraph;

namespace Avalonia.Controls
{
    internal sealed class DataGridCustomDrawingCompositionVisualHandler : CompositionCustomVisualHandler
    {
        public static readonly object ClearMessage = new();

        private ICustomDrawOperation _drawOperation;

        public override void OnMessage(object message)
        {
            if (ReferenceEquals(message, ClearMessage))
            {
                if (SetDrawOperation(null))
                {
                    Invalidate();
                }

                return;
            }

            if (message is ICustomDrawOperation drawOperation)
            {
                if (SetDrawOperation(drawOperation))
                {
                    Invalidate();
                }
            }
        }

        public override void OnRender(ImmediateDrawingContext drawingContext)
        {
            _drawOperation?.Render(drawingContext);
        }

        private bool SetDrawOperation(ICustomDrawOperation drawOperation)
        {
            if (ReferenceEquals(_drawOperation, drawOperation))
            {
                return false;
            }

            if (_drawOperation != null &&
                drawOperation != null &&
                (_drawOperation.Equals(drawOperation) || drawOperation.Equals(_drawOperation)))
            {
                // Keep the currently hosted operation to avoid redundant compositor invalidation.
                drawOperation.Dispose();
                return false;
            }

            var oldOperation = _drawOperation;
            _drawOperation = drawOperation;
            oldOperation?.Dispose();
            return true;
        }
    }
}
