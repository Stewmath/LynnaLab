#nullable enable

using System.Runtime.InteropServices;
using SDL;

namespace LynnaLab;

/// <summary>
/// Helper class to render an emulator instance in ImGui. Also deals with audio.
/// </summary>
public unsafe class EmulatorRenderer : Frame, IDisposable
{
    // ================================================================================
    // Constructors
    // ================================================================================

    public EmulatorRenderer(OraclesEmulator emu, int scale = 1) : base("Emulator")
    {
        this.emu = emu;
        this.scale = scale;

        screenTexture = Top.Backend.CreateTexture(160, 144);
        screenTexture.CopyPixels(emu.PixelData, 160, 144, 4);

        // On vblank, update the texture
        emu.GB.SetVBlankCallback((type) =>
        {
            screenTexture.CopyPixels(emu.PixelData, 160, 144, 4);
        });

        WindowFlags |= ImGuiWindowFlags.NoResize;

        if (Top.AudioAvailable)
            SetupAudio();
    }

    void SetupAudio()
    {
        const uint SAMPLE_RATE = 44100;

        SDL_AudioSpec audioSpec;
        audioSpec.format = SDL_AudioFormat.SDL_AUDIO_S16LE;
        audioSpec.channels = 2;
        audioSpec.freq = (int)SAMPLE_RATE;

        audioStream = SDL3.SDL_OpenAudioDeviceStream(
            SDL3.SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK,
            &audioSpec,
            null,
            0
        );

        if (audioStream == null)
            throw new Exception("SDL_OpenAudioDeviceStream error: " + SDL3.SDL_GetError());

        emu.GB.ApuSetSampleCallback((s) =>
        {
            if (!SDL3.SDL_PutAudioStreamData(audioStream, (nint)(&s), 4))
                throw new Exception("SDL_PutAudioStreamData error: " + SDL3.SDL_GetError());
        });

        emu.GB.SetSampleRate(SAMPLE_RATE);

        SDL3.SDL_ResumeAudioStreamDevice(audioStream);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    OraclesEmulator emu;
    RgbaTexture screenTexture;
    SDL_AudioStream* audioStream;

    int scale;

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGuiX.DrawImage(screenTexture, scale);
    }

    public void Dispose()
    {
        emu.GB.SetVBlankCallback(null);
        screenTexture.Dispose();
        screenTexture = null!;

        if (audioStream != null)
        {
            emu.GB.ApuSetSampleCallback(null);
            SDL3.SDL_DestroyAudioStream(audioStream);
            audioStream = null;
        }
    }
}
