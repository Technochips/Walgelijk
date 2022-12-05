﻿namespace Walgelijk.Video
{
    /// <summary>
    /// Controls the behaviour of gifs and videos
    /// </summary>
    public class VideoSystem : Walgelijk.System
    {
        public override void Update()
        {
            UpdateGifs();
        }

        private void UpdateGifs()
        {
            foreach (var gifComponent in Scene.GetAllComponentsOfType<GifComponent>())
            {
                if (gifComponent.Gif == null)
                    continue;

                if (gifComponent.IsPlaying)
                {
                    var gif = gifComponent.Gif;
                    if (gifComponent.OutputTexture == null)
                    {
                        gifComponent.OutputTexture = GifManager.InitialiseTextureFor(gif);
                        gifComponent.OnTextureInitialised?.Dispatch(gifComponent.OutputTexture);
                    }

                    float dt;
                    if (gifComponent.IgnoreTimeScale)
                        dt = Time.DeltaTimeUnscaled * gifComponent.PlaybackSpeed;
                    else
                        dt = Time.DeltaTime * gifComponent.PlaybackSpeed;

                    gifComponent.PlaybackTime += dt;

                    int frameToDisplay = gif.GetFrameIndexAt(gifComponent.PlaybackTime);

                    if (!gifComponent.Loop && frameToDisplay >= gif.FrameCount - 1)
                        gifComponent.IsPlaying = false;

                    if (frameToDisplay == gifComponent.CurrentDisplayedFrame)
                        continue;

                    gifComponent.CurrentDisplayedFrame = frameToDisplay;

                    GifManager.GetFrame(gif, frameToDisplay, gifComponent.OutputTexture);
                }
            }
        }
    }
}
