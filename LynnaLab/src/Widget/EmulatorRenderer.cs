using System.Runtime.InteropServices;
using SameBoySharp;
using SDL;

namespace LynnaLab;

/// <summary>
/// Helper class to render an emulator instance in ImGui. Also deals with audio and input.
/// </summary>
public unsafe class EmulatorRenderer : Frame, IDisposable
{
    const uint SAMPLE_RATE = 44100;

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

        base.WindowFlags |= ImGuiWindowFlags.AlwaysAutoResize;
        base.AddStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

        if (Top.AudioAvailable)
            SetupAudio();
    }

    void SetupAudio()
    {
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
    bool muted = false;

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGuiX.DrawImage(screenTexture, scale);

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            ImGui.OpenPopup("EmulatorPopupMenu");
        }

        if (ImGui.BeginPopup("EmulatorPopupMenu"))
        {
            ImGuiX.Checkbox("Mute", muted, (b) => SetMuted(b));
            ImGui.End();
        }

        // Handle input
        if (ImGui.IsWindowFocused())
        {
            (ImGuiKey, SameBoy.GBKeyMask)[] keyList = [
                (ImGuiKey.RightArrow, SameBoy.GBKeyMask.GB_KEY_RIGHT_MASK),
                (ImGuiKey.LeftArrow, SameBoy.GBKeyMask.GB_KEY_LEFT_MASK),
                (ImGuiKey.UpArrow, SameBoy.GBKeyMask.GB_KEY_UP_MASK),
                (ImGuiKey.DownArrow, SameBoy.GBKeyMask.GB_KEY_DOWN_MASK),
                (ImGuiKey.Z, SameBoy.GBKeyMask.GB_KEY_A_MASK),
                (ImGuiKey.X, SameBoy.GBKeyMask.GB_KEY_B_MASK),
                (ImGuiKey.RightShift, SameBoy.GBKeyMask.GB_KEY_SELECT_MASK),
                (ImGuiKey.Enter, SameBoy.GBKeyMask.GB_KEY_START_MASK),

                // Dvorak weirdo bindings
                (ImGuiKey.E, SameBoy.GBKeyMask.GB_KEY_RIGHT_MASK),
                (ImGuiKey.A, SameBoy.GBKeyMask.GB_KEY_LEFT_MASK),
                (ImGuiKey.Comma, SameBoy.GBKeyMask.GB_KEY_UP_MASK),
                (ImGuiKey.O, SameBoy.GBKeyMask.GB_KEY_DOWN_MASK),
                (ImGuiKey.C, SameBoy.GBKeyMask.GB_KEY_A_MASK),
                (ImGuiKey.H, SameBoy.GBKeyMask.GB_KEY_B_MASK),
                (ImGuiKey.Backslash, SameBoy.GBKeyMask.GB_KEY_SELECT_MASK),
            ];

            SameBoy.GBKeyMask mask = 0;

            foreach (var (imKey, gbKey) in keyList)
            {
                if (ImGui.IsKeyDown(imKey))
                    mask |= gbKey;
            }
            emu.GB.SetKeyMask(mask);

            bool turbo = ImGui.IsKeyDown(ImGuiKey.Space);
            emu.GB.SetTurboMode(turbo);
            if (turbo)
            {
                // Don't let audio stream fall behind
                SDL3.SDL_ClearAudioStream(audioStream);
            }
        }
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

    // ================================================================================
    // Private functions
    // ================================================================================

    void SetMuted(bool b)
    {
        if (b)
            emu.GB.SetSampleRate(0);
        else
            emu.GB.SetSampleRate(SAMPLE_RATE);
        muted = b;
    }

}
