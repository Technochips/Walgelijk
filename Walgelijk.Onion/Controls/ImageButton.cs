﻿using System.Runtime.CompilerServices;
using Walgelijk.SimpleDrawing;

namespace Walgelijk.Onion.Controls;

public readonly struct ImageButton : IControl
{
    public readonly IReadableTexture Texture;
    public readonly ImageContainmentMode ContainmentMode;

    public ImageButton(IReadableTexture texture, ImageContainmentMode containmentMode)
    {
        Texture = texture;
        ContainmentMode = containmentMode;
    }

    public static bool Hold(IReadableTexture texture, ImageContainmentMode containmentMode = ImageContainmentMode.Stretch, int identity = 0, [CallerLineNumber] int site = 0)
    {
        return Start(texture, containmentMode, identity, site).Held;
    }

    public static bool Click(IReadableTexture texture, ImageContainmentMode containmentMode = ImageContainmentMode.Stretch, int identity = 0, [CallerLineNumber] int site = 0)
    {
        return Start(texture, containmentMode, identity, site).Up;
    }

    public static InteractionReport Start(IReadableTexture texture, ImageContainmentMode containmentMode, int identity = 0, [CallerLineNumber] int site = 0)
    {
        var (instance, node) = Onion.Tree.Start(IdGen.Create(nameof(ImageButton).GetHashCode(), identity, site), new ImageButton(texture, containmentMode));
        instance.RenderFocusBox = false;
        Onion.Tree.End();
        return new InteractionReport(instance, node, InteractionReport.CastingBehaviour.Up);
    }

    public void OnAdd(in ControlParams p) { }

    public void OnRemove(in ControlParams p) { }

    public void OnStart(in ControlParams p)
    {
    }

    public void OnProcess(in ControlParams p)
    {
        ControlUtils.ProcessButtonLike(p);

        //if (instance.State.HasFlag(ControlState.Hover))
        //    IControl.SetCursor(DefaultCursor.Pointer);
    }

    public void OnRender(in ControlParams p)
    {
        (ControlTree tree, Layout.LayoutQueue layout, Input input, GameState state, Node node, ControlInstance instance) = p;

        var t = node.GetAnimationTime();
        var anim = instance.Animations;

        var fg = p.Theme.Foreground[instance.State];
        Draw.Colour = fg.Color;
        Draw.Texture = fg.Texture;
        Draw.ImageMode = fg.ImageMode;
        Draw.OutlineColour = p.Theme.OutlineColour[instance.State];
        Draw.OutlineWidth = p.Theme.OutlineWidth[instance.State];

        anim.AnimateRect(ref instance.Rects.Rendered, t);
        anim.AnimateColour(ref Draw.OutlineColour, t);
        anim.AnimateColour(ref Draw.Colour, t);
        Draw.Quad(instance.Rects.Rendered, 0, p.Theme.Rounding);

        Draw.ResetMaterial();
        Draw.ImageMode = default;
        Draw.Colour = p.Theme.Image[instance.State];
        anim.AnimateColour(ref Draw.Colour, t);
        Draw.OutlineWidth = 0;

        Draw.Image(Texture, instance.Rects.Rendered, ContainmentMode, 0, p.Theme.Rounding);
        Draw.ResetTexture();
    }

    public void OnEnd(in ControlParams p)
    {
    }
}
