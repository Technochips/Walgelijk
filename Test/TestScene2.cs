﻿using System;
using System.Diagnostics;
using System.Numerics;
using Walgelijk;
using Walgelijk.Imgui;
using Walgelijk.SimpleDrawing;

namespace TestWorld;

public struct TestScene2
{
    public static Scene Load(Game game)
    {
        var scene = new Scene(game);

        scene.AddSystem(new TransformSystem());
        scene.AddSystem(new CameraSystem() { ExecutionOrder = -1 });
        scene.AddSystem(new TestSystem());
        scene.AddSystem(new GuiSystem());

        var camera = scene.CreateEntity();
        scene.AttachComponent(camera, new TransformComponent());
        scene.AttachComponent(camera, new CameraComponent
        {
            PixelsPerUnit = 1,
            OrthographicSize = 1,
            ClearColour = new Color("#a8a3c1")
        });

        return scene;
    }

    public class TestSystem : Walgelijk.System
    {
        Stopwatch sw = new();
        int fixedPerSecond = 0;
        Vector2 last;
        Vector2 target;

        public override void Initialise()
        {
            sw.Start();
        }

        public override void Update()
        {
            Draw.Reset();
            Draw.Order = new RenderOrder(50, 0);
            Draw.ScreenSpace = true;
            Draw.Colour = Colors.Purple;
            Draw.Circle(Vector2.Lerp(last, target, Time.Interpolation), new Vector2(25));

            Draw.Colour = Colors.Aqua;
            Draw.Circle(target, new Vector2(15));

            Draw.Order = new RenderOrder(25, 0);
            Draw.Colour = Colors.Blue;
            Draw.Circle(new Vector2((Time.SecondsSinceLoad * 45) % 512), new Vector2(32));

            if (sw.Elapsed.TotalSeconds >= 1d)
            {
                Logger.Debug($"{fixedPerSecond} fixed updates per second");
                fixedPerSecond = 0;
                sw.Restart();
            }

            if (Input.IsKeyHeld(Key.Space))
                Gui.Label("test", new Vector2(64), style: new Style() { Text = Colors.WhiteSmoke });
        }

        public override void FixedUpdate()
        {
            last = target;
            target = Input.WindowMousePosition;
            fixedPerSecond++;
        }
    }
}