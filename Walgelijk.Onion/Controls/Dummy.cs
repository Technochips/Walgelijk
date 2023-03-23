﻿using Walgelijk.Onion.Layout;
using Walgelijk.SimpleDrawing;

namespace Walgelijk.Onion.Controls;

public readonly struct Dummy : IControl
{
    public readonly bool DrawBackground;

    public Dummy(bool drawBackground = false)
    {
        DrawBackground = drawBackground;
    }

    public void OnAdd(in ControlParams p)
    {
    }

    public void OnStart(in ControlParams p)
    {
        Onion.Layout.FitContainer(1, 1);
        p.Instance.Rects.Local = new Rect(0, 0, 1, 1);
        p.Instance.CaptureFlags = CaptureFlags.None;
        p.Instance.Rects.Raycast = null;
        p.Instance.Rects.DrawBounds = null;
    }

    public void OnProcess(in ControlParams p)
    {
        p.Instance.Rects.Rendered = p.Instance.Rects.ComputedGlobal;
        if (DrawBackground)
        {
            p.Instance.CaptureFlags = CaptureFlags.Hover;
            p.Instance.Rects.Raycast = p.Instance.Rects.Rendered;
            p.Instance.Rects.DrawBounds = p.Instance.Rects.Rendered;
        }
    }

    public void OnRender(in ControlParams p)
    {
        if (DrawBackground)
        {
            (ControlTree tree, Layout.Layout layout, Input input, GameState state, Node node, ControlInstance instance) = p;

            instance.Rects.Rendered = instance.Rects.ComputedGlobal;
            var t = node.GetAnimationTime();

            if (t <= float.Epsilon)
                return;

            var anim = instance.Animations;

            var fg = Onion.Theme.Foreground;
            Draw.Colour = fg.Color;
            Draw.Texture = fg.Texture;

            anim.AnimateRect(ref instance.Rects.Rendered, t);
            anim.AnimateColour(ref Draw.Colour, t);
            Draw.Quad(instance.Rects.Rendered, 0, Onion.Theme.Rounding);
        }
    }

    public void OnEnd(in ControlParams p) { }

    public void OnRemove(in ControlParams p) { }
}
