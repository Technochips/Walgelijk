﻿using System;
using System.Numerics;

namespace Walgelijk;

internal class DebugConsoleUi : IDisposable
{
    public readonly ActionRenderTask RenderTask;

    public int Padding = 8;
    public int Height = 350;
    public int FilterWidth = 128;
    public int InputHeight = 32;
    public int LineHeight = 16;
    public float AnimationDuration = 0.2f;
    public float BackgroundIntensity = 1;

    public Color BackgroundColour = new(25, 25, 25, 250);
    public Color InputBoxColour = new(15, 15, 15);
    public Color InputTextColour = new(240, 240, 240);
    public Color FilterBoxColour = new(35, 35, 35);
    public Color CaretColour = new(250, 250, 250);
    public IReadableTexture? BackgroundTexture = DebugConsoleAssets.DefaultBg;

    public int MaxLineCount => (Height - InputHeight - Padding) / LineHeight;
    public int VisibleLineCount { get; private set; }
    public int ProcessedLineCount { get; private set; }

    public float CaretBlinkTime = 0;

    private readonly DebugConsole debugConsole;
    private readonly Material mat;
    private readonly VertexBuffer textMesh;
    private readonly TextMeshGenerator textMeshGenerator = new();
    private readonly char[] lineBuffer = new char[256];
    private float animationProgress = 0;
    private int dropdownOffset;
    private Color flashColour;
    private float flashTime;

    private Rect backgroundRect;
    private Rect inputBoxRect;
    private Rect filterBoxRect;

    private readonly FilterIcon[] filterButtons;

    private struct FilterIcon
    {
        public Rect Rect;
        public readonly Color Color;
        public readonly IReadableTexture Texture;
        public readonly ConsoleMessageType MessageType;

        public bool Hover;

        public FilterIcon(IReadableTexture texture, ConsoleMessageType messageType, Color color)
        {
            Rect = default;
            Texture = texture;
            MessageType = messageType;
            Color = color;
            Hover = false;
        }
    }

    public DebugConsoleUi(DebugConsole debugConsole)
    {
        this.debugConsole = debugConsole;
        const int maxTextMeshVerts = 2048;

        RenderTask = new ActionRenderTask(Render);

        textMesh = new VertexBuffer(new Vertex[maxTextMeshVerts], new uint[(int)(maxTextMeshVerts * 1.5f)])
        {
            Dynamic = true,
            AmountOfIndicesToRender = 0,
        };

        mat = new Material(new Shader(ShaderDefaults.WorldSpaceVertex, DebugConsoleAssets.FragmentShader));
        mat.SetUniform("tint", Colors.Magenta);
        mat.SetUniform("mainTex", Texture.White);
        mat.SetUniform("bgTex", Texture.White);

        filterButtons = new FilterIcon[]
        {
            new FilterIcon(DebugConsoleAssets.AlertIcon, ConsoleMessageType.Error, new Color(1, 0.25f, 0.1f) ),
            new FilterIcon(DebugConsoleAssets.AlertIcon, ConsoleMessageType.Warning, new Color(1, 0.7f, 0.2f) ),
            new FilterIcon(DebugConsoleAssets.InfoIcon, ConsoleMessageType.Info, new Color(240, 240, 240) ),
            new FilterIcon(DebugConsoleAssets.DebugIcon, ConsoleMessageType.Debug, new Color(0.8f, 0.2f, 1) ),
        };
    }

    public void Flash(Color colour)
    {
        flashTime = 1;
        flashColour = colour;
    }

    public void Dispose()
    {
        mat.Dispose();
    }

    public void Update(Game game)
    {
        var input = game.State.Input;
        var width = game.Window.Width;
        var dt = game.State.Time.DeltaTimeUnscaled / AnimationDuration;

        if (!debugConsole.IsActive)
            dt *= -1;

        flashTime = Utilities.Clamp(flashTime - game.State.Time.DeltaTimeUnscaled);

        animationProgress = Utilities.Clamp(animationProgress + dt);
        dropdownOffset = (int)(animationProgress * Height - Height);
        mat.SetUniform("time", game.State.Time.SecondsSinceLoadUnscaled * 0.04f);
        var bottom = Height + dropdownOffset;

        backgroundRect = new Rect(0, 0, width, Height).Translate(0, dropdownOffset);
        inputBoxRect = new Rect(0, -InputHeight, width - FilterWidth, 0).Translate(0, Height + dropdownOffset);

        const float iconSize = 24;
        var filtersPos = new Vector2(width - FilterWidth, bottom - InputHeight / 2);
        var iconRect = new Rect(-iconSize / 2, -iconSize / 2, iconSize / 2, iconSize / 2).Translate(filtersPos);
        float padding = filterBoxRect.Height - iconSize;

        filterBoxRect = new Rect(filtersPos.X, filtersPos.Y - InputHeight / 2, filtersPos.X + FilterWidth, filtersPos.Y + InputHeight / 2);

        for (int i = 0; i < filterButtons.Length; i++)
        {
            float x = Utilities.MapRange(0, 1, padding, filterBoxRect.Width - iconSize - padding, i / ((float)filterButtons.Length - 1));
            var r = filterButtons[i].Rect = iconRect.Translate(x + 12, 0);
            var hover = filterButtons[i].Hover = r.ContainsPoint(input.WindowMousePosition);
            if (input.IsButtonPressed(MouseButton.Left) && hover)
                debugConsole.Filter ^= filterButtons[i].MessageType;
        }
    }

    public void Render(IGraphics graphics)
    {
        if (animationProgress < float.Epsilon)
            return;

        var target = graphics.CurrentTarget;
        var op = target.ProjectionMatrix;
        var ov = target.ViewMatrix;
        target.ProjectionMatrix = target.OrthographicMatrix;
        target.ViewMatrix = Matrix4x4.Identity;
        {
            graphics.DrawBounds = new DrawBounds(new Vector2(target.Size.X, Height), Vector2.Zero);

            DrawPanels(graphics);
            DrawFilterButtons(graphics);
            DrawConsoleBuffer(graphics);
            DrawInputText(graphics);

            // draw flash
            if (flashTime > float.Epsilon)
            {
                graphics.DrawBounds = new DrawBounds(inputBoxRect);
                mat.SetUniform("mainTex", Texture.White);
                mat.SetUniform("bgIntensity", 0f);
                DrawQuad(graphics, inputBoxRect, flashColour * Easings.Quad.In(flashTime));
            }

            graphics.DrawBounds = DrawBounds.DisabledBounds;
        }
        target.ProjectionMatrix = op;
        target.ViewMatrix = ov;
    }

    private void DrawConsoleBuffer(IGraphics graphics)
    {
        var b = debugConsole.GetBuffer();
        var lineRect = new Rect(0, 0, graphics.CurrentTarget.Size.X, LineHeight).Expand(-Padding).Translate(0, dropdownOffset);
        var totalRect = (backgroundRect with { MaxY = Height - InputHeight }).Translate(0, dropdownOffset);

        graphics.DrawBounds = new DrawBounds(totalRect);

        int lineBufferIndex = 0;
        int lineIndex = 0;
        int lineOffset = debugConsole.ScrollOffset;

        ProcessedLineCount = 0;
        VisibleLineCount = 0;
        // split the buffer into lines
        for (int i = 0; i < b.Length; i++)
        {
            var c = (char)b[i];
            int offsetLineIndex = lineIndex - lineOffset;

            // found a new line, time to render what we already found and reset the cursor
            if (c == '\n')
            {
                // are we in line offset range
                if (lineIndex >= lineOffset)
                {
                    // get the actual line text
                    ReadOnlySpan<char> t = lineBuffer.AsSpan(0, lineBufferIndex).Trim();
                    if (t.IsEmpty || t.IsWhiteSpace())
                    {
                        DrawText(graphics, "<empty line>", lineRect.Translate(0, LineHeight * offsetLineIndex), Colors.White.WithAlpha(0.2f), false);
                        VisibleLineCount++;
                    }
                    else
                    {
                        var messageType = debugConsole.DetectMessageType(t);
                        var col = GetColourForMessageType(messageType);
                        if ((messageType == ConsoleMessageType.None && debugConsole.Filter is not ConsoleMessageType.All) || !debugConsole.Filter.HasFlag(messageType))
                        {
                            lineBufferIndex = 0;
                            continue;
                            //col = InputTextColour * 0.3f;
                        }
                        DrawText(graphics, t, lineRect.Translate(0, LineHeight * offsetLineIndex), col, false);
                        VisibleLineCount++;
                    }
                }

                ProcessedLineCount++;
                lineIndex++;
                if (offsetLineIndex >= MaxLineCount - 1)
                    return;
                lineBufferIndex = 0;
                continue;
            }
            else if (char.IsControl(c))
                continue;

            if (lineBufferIndex >= lineBuffer.Length - 1)
                continue;

            lineBuffer[lineBufferIndex] = c;
            lineBufferIndex++;
        }
    }

    public Color GetColourForMessageType(ConsoleMessageType type)
        => type switch
        {
            ConsoleMessageType.Debug => Colors.Purple,
            ConsoleMessageType.Info => Colors.White,
            ConsoleMessageType.Warning => Colors.Orange,
            ConsoleMessageType.Error => Colors.Red,
            _ => InputTextColour.WithAlpha(0.8f),
        };

    private void DrawInputText(IGraphics graphics)
    {
        float cursorPos = 0;
        var r = inputBoxRect.Expand(-Padding);

        if (!string.IsNullOrWhiteSpace(debugConsole.CurrentInput))
        {
            graphics.DrawBounds = new DrawBounds(inputBoxRect);
            DrawText(graphics, debugConsole.CurrentInput.AsSpan(0, Math.Min(debugConsole.CurrentInput.Length, 256)), r, InputTextColour, false);
            cursorPos = textMeshGenerator.CalculateTextWidth(debugConsole.CurrentInput.AsSpan(0, debugConsole.CursorPosition));
        }

        if (CaretBlinkTime % 1 < 0.5f)
        {
            mat.SetUniform("mainTex", Texture.White);
            DrawQuad(graphics,
                new Rect(new Vector2(cursorPos + Padding + 2, (r.MinY + r.MaxY) / 2), new Vector2(1, r.Height)),
                CaretColour);
        }
    }

    private void DrawPanels(IGraphics graphics)
    {
        mat.SetUniform("mainTex", Texture.White);
        mat.SetUniform("bgIntensity", BackgroundIntensity);
        mat.SetUniform("bgTex", BackgroundTexture ?? Texture.White);
        DrawQuad(graphics, backgroundRect, BackgroundColour);

        mat.SetUniform("bgIntensity", 0f);
        DrawQuad(graphics, inputBoxRect, InputBoxColour);
        DrawQuad(graphics, filterBoxRect, FilterBoxColour);
    }

    private void DrawFilterButtons(IGraphics graphics)
    {
        foreach (var item in filterButtons)
        {
            mat.SetUniform("mainTex", item.Texture);
            var enabled = debugConsole.Filter.HasFlag(item.MessageType);

            var col = enabled ? item.Color : InputBoxColour;
            var r = item.Rect;

            if (item.Hover)
                col.A = 0.7f;

            DrawQuad(graphics, r, col);
        }
    }

    private void DrawQuad(IGraphics graphics, Rect rect, Color color)
    {
        graphics.CurrentTarget.ModelMatrix =
            new Matrix4x4(
                Matrix3x2.CreateScale(rect.GetSize()) *
                Matrix3x2.CreateTranslation(rect.BottomLeft)
            );
        mat.SetUniform("tint", color);
        graphics.Draw(PrimitiveMeshes.Quad, mat);
    }

    private TextMeshResult DrawText(IGraphics graphics, ReadOnlySpan<char> text, Rect rect, Color color, bool wrap = true)
    {
        if (text.IsEmpty || text.IsWhiteSpace())
            return default;

        graphics.CurrentTarget.ModelMatrix =
            new Matrix4x4(
                Matrix3x2.CreateScale(1, -1) *
                Matrix3x2.CreateTranslation(rect.BottomLeft)
            );

        textMeshGenerator.Color = color;
        textMeshGenerator.ParseRichText = false;
        textMeshGenerator.WrappingWidth = wrap ? rect.Width : float.MaxValue;
        textMeshGenerator.Font = Font.Default;
        textMeshGenerator.Multiline = false;

        var result = textMeshGenerator.Generate(text, textMesh.Vertices, textMesh.Indices);
        textMesh.AmountOfIndicesToRender = result.IndexCount;
        textMesh.ForceUpdate();

        mat.SetUniform("mainTex", Font.Default.Pages![0]);
        mat.SetUniform("bgIntensity", 0f);
        mat.SetUniform("tint", Colors.White);
        graphics.Draw(textMesh, mat);

        return result;
    }
}