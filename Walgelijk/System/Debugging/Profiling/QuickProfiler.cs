﻿using System;
using System.Numerics;

namespace Walgelijk
{
    internal class QuickProfiler
    {
        private const float Padding = 5;

        private readonly Matrix4x4 textModel = Matrix4x4.CreateScale(1f, -1f, 1) * Matrix4x4.CreateTranslation(Padding, Padding, 0);
        private readonly TextComponent text;
        private readonly RectangleShapeComponent background;
        private readonly Profiler profiler;

        public QuickProfiler(Profiler profiler)
        {
            text = new TextComponent("?");
            text.RenderTask.ScreenSpace = true;

            background = new RectangleShapeComponent();
            background.Color = new Color(0, 0, 0, 0.2f);
            background.Pivot = Vector2.Zero;
            background.RenderTask.ScreenSpace = true;

            this.profiler = profiler;
        }

        public void Render(RenderQueue queue)
        {
            string t = ConstructDisplayText(queue.Length);

            text.String = t;
            text.RenderTask.ModelMatrix = textModel;

            Rect textBounds = text.LocalBoundingBox;
            background.RenderTask.ModelMatrix = Matrix4x4.CreateScale(new Vector3(textBounds.Width + (2 * Padding), -textBounds.Height - (2 * Padding), 1));

            queue.Add(background.RenderTask, RenderOrder.DebugUI);
            queue.Add(text.RenderTask, RenderOrder.DebugUI);
        }

        private string ConstructDisplayText(int queuelength)
        {
            float renderTime = MathF.Round(1000 / profiler.FramesPerSecond, 3);

            string t =
                $"render time {renderTime}ms\n" +
                $"{MathF.Floor(1000 / renderTime)} fps\n" +
                $"{queuelength} render tasks\n\n";

            foreach (var item in profiler.GetProfiledTasks())
                t += $"{item.Name}: {Math.Round(item.Duration.TotalMilliseconds, 3)}ms\n";

            return t;
        }
    }
}
