﻿using System.Buffers;
using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using Walgelijk.SimpleDrawing;

namespace Walgelijk.Onion;

public class Navigator
{
    /// <summary>
    /// Control currently capturing the cursor
    /// </summary>
    public int? HoverControl
    {
        get => hoverControl;
        set
        {
            ProcessStateChange(hoverControl, value, ControlState.Hover);
            hoverControl = value;
        }
    }

    /// <summary>
    /// Control currently capturing the scroll wheel (scroll view, sliders, dropdowns, etc.)
    /// </summary>
    public int? ScrollControl
    {
        get => scrollControl;
        set
        {
            ProcessStateChange(scrollControl, value, ControlState.Scroll);
            scrollControl = value;
        }
    }

    /// <summary>
    /// Control that is currently selected (textboxes, dropdowns, sliders, etc.)
    /// </summary>
    public int? FocusedControl
    {
        get => focusedControl;
        set
        {
            ProcessStateChange(focusedControl, value, ControlState.Focus);
            focusedControl = value;
        }
    }

    /// <summary>
    /// Control that is actively being used (buttons held, dropdowns held, sliders sliding, etc.)
    /// </summary>
    public int? ActiveControl
    {
        get => activeControl;
        set
        {
            ProcessStateChange(activeControl, value, ControlState.Active);
            activeControl = value;
        }
    }

    /// <summary>
    /// Control that is ready for extended interactivity (dropdowns open)
    /// </summary>
    public int? TriggeredControl
    {
        get => triggeredControl;
        set
        {
            ProcessStateChange(triggeredControl, value, ControlState.Triggered);
            triggeredControl = value;
        }
    }

    /// <summary>
    /// Control that is currently capturing keyboard input
    /// </summary>
    public int? KeyControl
    {
        get => keyControl;
        set
        {
            ProcessStateChange(keyControl, value, ControlState.Key);
            keyControl = value;
        }
    }

    /// <summary>
    /// This event is dispatched when a control's state is changed (on hover, focus, click, etc.)
    /// Provides the control ID and new state
    /// </summary>
    public static event Action<int, ControlState>? OnStateChange;

    /// <summary>
    /// Should the navigator respond to keyboard input? E.g. tab to switch focus
    /// </summary>
    public static bool KeyboardNavigation { get; set; } = true;

    internal int? ScrollbarOverride;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessStateChange(int? currentId, int? newId, ControlState s)
    {
        if (currentId == newId)
            return;

        if (currentId != null && Onion.Tree.Instances.TryGetValue(currentId.Value, out var old) && old is not null)
            old.LastStateChangeTime = Onion.Clock;

        if (!newId.HasValue || !Onion.Tree.Instances.TryGetValue(newId.Value, out var inst) || inst is null)
            return;

        if (!inst.Muted)
            Onion.PlaySound(s);

        OnStateChange?.Invoke(inst.Identity, s);

        inst.LastStateChangeTime = Onion.Clock;
    }

    public bool IsBeingUsed =>
        hoverControl.HasValue || scrollControl.HasValue || activeControl.HasValue || triggeredControl.HasValue || keyControl.HasValue;

    public ReadOnlySpan<SortedNode> SortedByDepth => sortedByDepthRaw.AsSpan(0, sortedByDepthCount);

    private int? hoverControl;
    private int? scrollControl;
    private int? focusedControl;
    private int? activeControl;
    private int? triggeredControl;
    private int? keyControl;

    // private readonly Stack<int> orderStack = new();
    private SortedNode[]? sortedByDepthRaw = null;
    private int sortedByDepthCount = 0;
    private int alwaysOnTopCounter = 0;

    private readonly Dictionary<int, float> highlightControls = [];

    public void Process(Input input, float dt)
    {
        ProcessHighlights(dt);
        RefreshOrder();

        if (input.EscapePressed)
        {
            focusedControl = null;
            scrollControl = null;
            activeControl = null;
            triggeredControl = null;
            hoverControl = null;
            ScrollbarOverride = null;
            return;
        }

        if (ScrollbarOverride.HasValue)
            return;

        HoverControl = Raycast(input.MousePosition.X, input.MousePosition.Y, CaptureFlags.Hover);

        if (input.ScrollDelta.LengthSquared() > float.Epsilon)
            ScrollControl = Raycast(input.MousePosition, CaptureFlags.Scroll);
        else
            ScrollControl = null;

        //if (input.DirectionKeyReleased.LengthSquared() > 0 || input.TextEntered.Length > 0)
        //    KeyControl = Raycast(input.MousePosition.X, input.MousePosition.Y, CaptureFlags.Key);
        //else 
        KeyControl = null;

        if (ActiveControl.HasValue)
            if (!Onion.Tree.IsAlive(ActiveControl.Value))
                ActiveControl = null;

        if (FocusedControl.HasValue)
        {
            var inst = Onion.Tree.EnsureInstance(FocusedControl.Value);

            if (inst.CaptureFlags.HasFlag(CaptureFlags.Key))
                KeyControl = inst.Identity;

            if (!Onion.Tree.IsAlive(FocusedControl.Value))
                FocusedControl = null;
            else if (HoverControl == null && input.MousePrimaryPressed)
                FocusedControl = null;
            else if (KeyControl == null)
                ProcessKeyboardNavigation(input);
        }
        else
            ProcessKeyboardNavigation(input);

#if DEBUG
        if (Game.Main.State.Input.IsKeyReleased(Key.F4))
        {
            var v = RaycastAll(input.MousePosition.X, input.MousePosition.Y, CaptureFlags.Hover);
            RoutineScheduler.Start(routine());
            IEnumerator<IRoutineCommand> routine()
            {
                foreach (var id in v)
                {
                    HighlightControl(id, 0.5f);
                    yield return new RoutineDelay(0.1f);
                }
            }

        }
#endif
    }

    private void ProcessKeyboardNavigation(Input input)
    {
        if (!KeyboardNavigation)
            return;

        if (!FocusedControl.HasValue)
        {
            if (input.TabReleased)
                FocusedControl = Onion.Tree.Nodes.Values.Where(static c =>
                {
                    //TODO voor de een of andere reden pakt hij de eerste niet
                    var n = Onion.Tree.EnsureInstance(c.Identity);
                    return (c.AliveLastFrame &&
                            n.CaptureFlags.HasFlag(CaptureFlags.Hover) &&
                            n.Rects.Raycast.HasValue &&
                            n.Rects.Raycast.Value.Area > float.Epsilon);
                }).FirstOrDefault()?.Identity ?? null; //TODO dit kan mooier
            else if (input.DirectionKeyReleased.LengthSquared() > 0)
            {
                var minDist = float.MaxValue;
                ControlInstance? nearest = null;

                foreach (var instance in Onion.Tree.Instances.Values)
                {
                    if (Onion.Tree.Nodes.TryGetValue(instance.Identity, out var node))
                    {
                        if (!node.AliveLastFrame)
                            continue;
                        var dist = Vector2.DistanceSquared(instance.Rects.Rendered.GetCenter(), input.MousePosition);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            nearest = instance;
                        }
                    }
                }

                if (nearest != null)
                {
                    FocusedControl = nearest.Identity;
                    HighlightControl(nearest.Identity, 0.5f);
                }
            }

            return;
        }

        if (input.TabReleased)
        {
            int targetOrder = Onion.Tree.Nodes[FocusedControl.Value].ChronologicalPositionLastFrame;
            bool greaterThan = !input.ShiftHeld;
            Func<Node, bool> predicate = greaterThan ? (Node n) => n.ChronologicalPositionLastFrame > targetOrder : (Node n) => n.ChronologicalPositionLastFrame < targetOrder;
            //TODO dit kan ook mooier
            var found = Onion.Tree.Nodes
                .Select(static c => c.Value)
                .Where(static c =>
                {
                    var n = Onion.Tree.EnsureInstance(c.Identity);
                    return (c.AliveLastFrame &&
                            n.CaptureFlags.HasFlag(CaptureFlags.Hover) &&
                            n.Rects.Raycast.HasValue &&
                            n.Rects.Raycast.Value.Area > float.Epsilon);
                })
                .OrderBy(c => greaterThan ? c.ChronologicalPositionLastFrame : -c.ChronologicalPositionLastFrame)
                .FirstOrDefault(predicate);

            if (found != null)
            {
                FocusedControl = found.Identity;
                HighlightControl(found.Identity, 0.5f);
            }
        }
        else if (input.DirectionKeyReleased.LengthSquared() > 0)
        {
            var hit = FindInDirection(FocusedControl.Value, input.DirectionKeyReleased);
            if (hit.HasValue)
                FocusedControl = hit;
        }
    }

    private void ProcessHighlights(float dt)
    {
        int removeCount = 0;
        var removeBuffer = ArrayPool<int>.Shared.Rent(highlightControls.Count);
        foreach (var item in highlightControls.Keys)
        {
            var v = highlightControls[item];
            if (v > 0)
                highlightControls[item] = v - dt;
            else
                removeBuffer[removeCount++] = item;
        }

        foreach (var key in removeBuffer.AsSpan(0, removeCount))
            highlightControls.Remove(key);
        ArrayPool<int>.Shared.Return(removeBuffer);

        Draw.Reset();
        Draw.Order = new RenderOrder(Onion.Configuration.RenderLayer, int.MaxValue);
        Draw.ScreenSpace = true;
        Draw.Colour = Colors.Transparent;

        foreach (var item in highlightControls)
        {
            var id = item.Key;
            var p = item.Value;
            var inst = Onion.Tree.EnsureInstance(id);

            Draw.OutlineWidth = inst.Theme.FocusBoxWidth;
            Draw.OutlineColour = inst.Theme.Highlight;
            Draw.OutlineColour.A = Utilities.Clamp(Easings.Expo.Out(p));
            Draw.Quad(inst.Rects.Rendered.Expand(inst.Theme.FocusBoxSize), 0, inst.Theme.Rounding + inst.Theme.FocusBoxSize);
        }
    }

    public void HighlightControl(int id, float duration = 1)
    {
        if (!highlightControls.TryAdd(id, duration))
            highlightControls[id] = duration;
    }

    private void RecurseNodeOrder(Node node, ref int index, ref int treeDepth)
    {
        if (!node.AliveLastFrame)
            return;

        if (sortedByDepthRaw!.Length <= index + 1)
            Array.Resize(ref sortedByDepthRaw, index + 32);

        if (node.AlwaysOnTop)
            alwaysOnTopCounter++;

        treeDepth++;
        foreach (var child in node.GetLivingChildren().OrderByDescending(static s => s.RequestedLocalOrder))
            RecurseNodeOrder(child, ref index, ref treeDepth);
        treeDepth--;

        // resize again because it mightve increased again
        if (sortedByDepthRaw!.Length <= index + 1)
            Array.Resize(ref sortedByDepthRaw, index + 32);

        sortedByDepthRaw![index] = new SortedNode(node.Identity, index, alwaysOnTopCounter > 0);
        index++;

        if (node.AlwaysOnTop)
            alwaysOnTopCounter--;
    }

    private void RefreshOrder()
    {
        if (sortedByDepthRaw == null)
            sortedByDepthRaw = new SortedNode[Onion.Tree.Nodes.Count];
        else if (sortedByDepthRaw.Length <= Onion.Tree.Nodes.Count)
            Array.Resize(ref sortedByDepthRaw, Onion.Tree.Nodes.Count);

        sortedByDepthCount = 0;
        alwaysOnTopCounter = 0;
        int treeDepth = 0;
        RecurseNodeOrder(Onion.Tree.Root, ref sortedByDepthCount, ref treeDepth);

        sortedByDepthRaw.AsSpan(0, sortedByDepthCount).Sort(static (a, b) =>
        {
            if (a.OnTop != b.OnTop) //TODO er gaat hier soms random iets fout
            {
                if (a.OnTop)
                    return -1;
                return 1;
            }

            return a.Order - b.Order;
        });

        int p = sortedByDepthCount;
        foreach (var item in SortedByDepth)
            Onion.Tree.Nodes[item.Identity].ComputedGlobalOrder = p--;
    }

    public int? Raycast(Vector2 pos, CaptureFlags captureFlags) => Raycast(pos.X, pos.Y, captureFlags);

    public int? Raycast(float x, float y, CaptureFlags captureFlags)
    {
        if (sortedByDepthRaw == null)
            return null;

        var v = new Vector2(x, y);

        for (int i = 0; i < SortedByDepth.Length; i++)
        {
            var c = SortedByDepth[i];
            var node = Onion.Tree.Nodes[c.Identity];
            if (!node.AliveLastFrame)
                continue;

            var inst = Onion.Tree.EnsureInstance(c.Identity);
            if (inst.Rects.Raycast.HasValue && (inst.CaptureFlags & captureFlags) != 0)
            {
                if (inst.Rects.Raycast.Value.ContainsPoint(v))
                    return c.Identity;
            }
        }

        return null;
    }

    public IEnumerable<int> RaycastAll(float x, float y, CaptureFlags captureFlags)
    {
        if (sortedByDepthRaw == null)
            yield break;

        var v = new Vector2(x, y);

        for (int i = 0; i < SortedByDepth.Length; i++)
        {
            var c = SortedByDepth[i];
            var node = Onion.Tree.Nodes[c.Identity];
            if (!node.AliveLastFrame)
                continue;

            var inst = Onion.Tree.EnsureInstance(c.Identity);
            if (inst.Rects.Raycast.HasValue && (inst.CaptureFlags & captureFlags) != 0)
            {
                if (inst.Rects.Raycast.Value.ContainsPoint(v))
                    yield return c.Identity;
            }
        }
    }

    public int? FindInDirection(int origin, Vector2 direction)
    {
        direction = Vector2.Normalize(direction);
        direction.Y *= -1;

        var originNode = Onion.Tree.Nodes[origin];
        var originInstance = Onion.Tree.EnsureInstance(origin);
        var originPos = originInstance.Rects.ComputedGlobal.GetCenter();
        var originOrder = originNode.ComputedGlobalOrder;

        List<DirectionalNode> found = new();

        foreach (var item in Onion.Tree.Nodes)
        {
            var id = item.Key;

            if (id == origin)
                continue;

            var node = item.Value;
            var inst = Onion.Tree.EnsureInstance(id);

            if (!node.AliveLastFrame ||
                inst.Rects.ComputedDrawBounds.Area <= float.Epsilon ||
                !inst.Rects.Raycast.HasValue ||
                inst.Rects.Raycast.Value.Area <= float.Epsilon
               )
                continue;

            if (!inst.CaptureFlags.HasFlag(CaptureFlags.Hover))
                continue;

            var pos = inst.Rects.ComputedGlobal.GetCenter();
            var dot = Vector2.Dot(pos - originPos, direction);

            if (dot < .5f)
                continue;

            var d = (pos - originPos).LengthSquared();

            found.Add(new DirectionalNode(id, dot, d, Math.Abs(originOrder - node.ComputedGlobalOrder)));
        }

        found.Sort(static (a, b) => (int)(a.Dot * 100000) - (int)(b.Dot * 100000));
        found.Sort(static (a, b) => a.OrderDelta - b.OrderDelta);
        found.Sort(static (a, b) => a.Distance > b.Distance ? 1 : -1);

        int? v = found.Any() ? found.First().Identity : null;

        if (v.HasValue)
            HighlightControl(v.Value, 0.5f);

        return v;
    }

    public void Clear()
    {
        // orderStack.Clear();
        highlightControls.Clear();
        sortedByDepthRaw = null;
    }

    private readonly struct DirectionalNode
    {
        public readonly int Identity;
        public readonly float Dot;
        public readonly float Distance;
        public readonly int OrderDelta;

        public DirectionalNode(int identity, float dot, float distance, int orderDelta)
        {
            Identity = identity;
            Dot = dot;
            Distance = distance;
            OrderDelta = orderDelta;
        }
    }

    public readonly struct SortedNode
    {
        public readonly int Identity;
        public readonly int Order;
        public readonly bool OnTop;

        public SortedNode(int identity, int order, bool onTop)
        {
            Identity = identity;
            OnTop = onTop;
            Order = order;
        }

        public override string? ToString() => $"Id: {Identity},Order: {Order}, OnTop: {OnTop}";
    }
}