﻿using System.Numerics;
using Walgelijk;
using Walgelijk.SimpleDrawing;

namespace Playgrounds;

public struct AudioTestScene : ISceneCreator
{
    public Scene Load(Game game)
    {
        var scene = new Scene(game);
        scene.AddSystem(new AudioTestSystem());

        game.UpdateRate = 120;
        game.FixedUpdateRate = 50;

        return scene;
    }

    public class AudioTestSystem : Walgelijk.System
    {
        private Sound OneShot = new Sound(Resources.Load<FixedAudioData>("cannot-build.wav"));
        private Sound Streaming = new Sound(Resources.Load<StreamAudioData>("perfect-loop.ogg"), true);

        public override void FixedUpdate()
        {
            if (Input.IsKeyHeld(Key.Space))
                Audio.PlayOnce(OneShot);
        }

        public override void Update()
        {
            if (Input.IsKeyPressed(Key.Q))
                if (Audio.IsPlaying(Streaming))
                    Audio.Pause(Streaming);
                else
                    Audio.Play(Streaming);

            Draw.Reset();
            Draw.ScreenSpace = true;
            Draw.Colour = Colors.Black;
            Draw.Quad(new Rect(0, 0, Window.Width, Window.Height));

            Draw.Colour = Colors.White;
            if (Audio is Walgelijk.OpenTK.OpenALAudioRenderer audio)
            {
                Draw.Text("Sources in use: " + audio.TemporarySourceBuffer.Count(), new Vector2(30, 30), Vector2.One, HorizontalTextAlign.Left, VerticalTextAlign.Top);
                Draw.Text("Sources created: " + audio.CreatedTemporarySourceCount, new Vector2(30, 40), Vector2.One, HorizontalTextAlign.Left, VerticalTextAlign.Top);
            }

            var p = Audio.GetTime(Streaming) / (float)Streaming.Data.Duration.TotalSeconds;

            Draw.Colour = Colors.Gray.Brightness(0.5f);
            Draw.Quad(new Rect(30, 100, 30 + 500, 120));
            Draw.Colour = Colors.Cyan;
            Draw.Quad(new Rect(30, 100, 30 + 500 * p, 120).Expand(-2));
        }
    }
}