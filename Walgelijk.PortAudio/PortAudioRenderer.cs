﻿using PortAudioSharp;
using System.Numerics;
using System.Runtime.InteropServices;
using PA = PortAudioSharp.PortAudio;

namespace Walgelijk.PortAudio;

public class PortAudioRenderer : AudioRenderer
{
    public override float Volume { get => aggregator.Volume; set => aggregator.Volume = value; }
    public override bool Muted { get => aggregator.Muted; set => aggregator.Muted = value; }
    public override Vector3 ListenerPosition { get => aggregator.ListenerPosition; set => aggregator.ListenerPosition = value; }
    public override (Vector3 Forward, Vector3 Up) ListenerOrientation { get => aggregator.ListenerOrientation; set => aggregator.ListenerOrientation = value; }
    public override AudioDistanceModel DistanceModel { get => aggregator.DistanceModel; set => aggregator.DistanceModel = value; }

    public readonly int MaxVoices;

    private PortAudioSharp.Stream? stream;
    private int currentDeviceIndex = PA.NoDevice;
    private SampleAggregator aggregator;
    private float[] sampleBuffer;

    internal const int SampleRate = 44100;
    internal const int FramesPerBuffer = 256;
    internal const int ChannelCount = 2;
    internal const double SecondsPerSample = 1d / SampleRate;

    public PortAudioRenderer(int maxVoices = 512)
    {
        aggregator = new(MaxVoices, 100);
        sampleBuffer = new float[ChannelCount * FramesPerBuffer];

        PA.LoadNativeLibrary();
        PA.Initialize();

        ReinitialiseStream(PA.DefaultOutputDevice);
        MaxVoices = maxVoices;
    }

    private void ReinitialiseStream(int deviceIndex)
    {
        if (stream != null)
        {
            stream.Close();
            stream.Dispose();
        }

        var outParams = new StreamParameters
        {
            device = deviceIndex,
            channelCount = ChannelCount,
            sampleFormat = SampleFormat.Float32,
            suggestedLatency = PA.GetDeviceInfo(deviceIndex).defaultLowOutputLatency
        };

        stream = new PortAudioSharp.Stream(null, outParams, SampleRate, FramesPerBuffer, default, OnPaCallback, null);

        stream.Start();
    }

    private StreamCallbackResult OnPaCallback(nint input, nint output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, nint userDataPtr)
    {
        /* TODO 
        /* - Remember: this function could be invoked at ANY TIME, it's likely to be on a different thread. Offload most of the work away from this function.
        /* - Make SoundProcessor object to keep track of currently playing sounds
        /* - Loop through all processors and get their samples
        /* - 3D audio, volume, attenuation, mute state, pitch, etc.
        /* - BONUS: effects? convolve? SIMD? HDR!!!
        */

        aggregator.GetNextSamples(sampleBuffer); // todo do this somewhere else, read results in this function
        Marshal.Copy(sampleBuffer, 0, output, sampleBuffer.Length);

        return StreamCallbackResult.Continue;
    }

    public override void DisposeOf(AudioData audioData)
    {
    }

    public override void DisposeOf(Sound sound)
    {
    }

    public override IEnumerable<string> EnumerateAvailableAudioDevices()
    {
        for (int i = 0; i < PA.DeviceCount; i++)
        {
            var info = PA.GetDeviceInfo(i);
            yield return info.name;
        }
    }

    public override string GetCurrentAudioDevice()
    {
        var info = PA.GetDeviceInfo(currentDeviceIndex);
        return info.name;
    }

    public override int GetCurrentSamples(Sound sound, Span<float> arr)
    {
        return 0;
    } 

    public override float GetTime(Sound sound)
    {
        aggregator.GetShared(sound, out var voice);
        return (float)voice.Time;
    }

    public override bool IsPlaying(Sound sound)
    {
        return aggregator.Contains(sound) && sound.State == SoundState.Playing;
    }

    public override FixedAudioData LoadSound(string path)
    {
        throw new NotImplementedException();
    }

    public override StreamAudioData LoadStream(string path)
    {
        throw new NotImplementedException();
    }

    public override void Pause(Sound sound)
    {
        aggregator.GetShared(sound, out var voice);
        voice.Pause();
    }

    public override void PauseAll()
    {
    }

    public override void PauseAll(AudioTrack track)
    {
    }

    public override void Play(Sound sound, float volume = 1)
    {
        aggregator.GetShared(sound, out var voice);
        voice.Play();
    }

    public override void Play(Sound sound, Vector3 worldPosition, float volume = 1)
    {
        aggregator.GetShared(sound, out var voice);
        voice.Position = worldPosition;
        voice.Play();
    }

    public override void PlayOnce(Sound sound, float volume = 1, float pitch = 1, AudioTrack? track = null)
    {
    }

    public override void PlayOnce(Sound sound, Vector3 worldPosition, float volume = 1, float pitch = 1, AudioTrack? track = null)
    {
    }

    public override void Process(float dt)
    {
    }

    public override void Release()
    {
        try
        {
            stream?.Dispose();
            PA.Terminate();
            aggregator.Dispose();
        }
        catch (Exception e)
        {
            Logger.Error(e);
        }
    }

    public override void ResumeAll()
    {
    }

    public override void ResumeAll(AudioTrack track)
    {
    }

    public override void SetAudioDevice(string device)
    {
        for (int i = 0; i < PA.DeviceCount; i++)
        {
            var info = PA.GetDeviceInfo(i);
            if (info.name.Equals(device))
            {
                ReinitialiseStream(i);
                return;
            }
        }

        throw new Exception($"Could not find audio device matching {device}");
    }

    public override void SetPosition(Sound sound, Vector3 worldPosition)
    {
        aggregator.GetShared(sound, out var voice);
        voice.Position = worldPosition;
    }

    public override void SetTime(Sound sound, float seconds)
    {
        aggregator.GetShared(sound, out var voice);
        voice.Time = seconds;
    }

    public override void Stop(Sound sound)
    {
        aggregator.GetShared(sound, out var voice);
        voice.Stop();
    }

    public override void StopAll()
    {
    }

    public override void StopAll(AudioTrack track)
    {
    }

    public override void UpdateTracks()
    {
    }
}
