using System.Runtime.InteropServices;
using SameBoySharp;
using Emulation;
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

        base.WindowFlags |= ImGuiWindowFlags.AlwaysAutoResize;
        base.AddStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

        if (Top.AudioAvailable)
            SetupAudio();

        renderWidget = new(this, emu);
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
    SDL_AudioStream* audioStream;
    ScreenWidget renderWidget;

    readonly int scale;
    bool muted = false;

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        renderWidget.Render();

        if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            ImGui.OpenPopup("EmulatorPopupMenu");
        }

        if (ImGui.BeginPopup("EmulatorPopupMenu"))
        {
            ImGuiX.Checkbox("Mute", muted, (b) => SetMuted(b));
            ImGui.EndPopup();
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
        renderWidget.Dispose();

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


    /// <summary>
    /// A SizedWidget that renders the image.
    /// </summary>
    class ScreenWidget : SizedWidget, IDisposable
    {
        // ================================================================================
        // Constructors
        // ================================================================================
        public ScreenWidget(EmulatorRenderer parent, OraclesEmulator emu) : base()
        {
            this.parent = parent;
            this.emu = emu;

            base.CenterX = true;
            base.CenterY = true;

            screenTexture = Top.Backend.CreateTexture(160, 144);
            screenTexture.CopyPixels(emu.PixelData, 160, 144, (uint)Scale);

            // On vblank, update the texture
            emu.GB.SetVBlankCallback((type) =>
            {
                screenTexture.CopyPixels(emu.PixelData, 160, 144, (uint)Scale);
            });

        }

        // ================================================================================
        // Variables
        // ================================================================================

        EmulatorRenderer parent;
        OraclesEmulator emu;
        RgbaTexture screenTexture;

        // ================================================================================
        // Properties
        // ================================================================================

        public override Vector2 WidgetSize { get { return new Vector2(160, 144) * Scale; } }

        public int Scale { get { return parent.scale; } }

        // ================================================================================
        // Public methods
        // ================================================================================


        public void Render()
        {
            base.RenderPrep();

            ImGuiX.DrawImage(screenTexture, Scale);

            HandleObjectDrag();
        }

        public void Dispose()
        {
            screenTexture.Dispose();
            screenTexture = null!;
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        /// <summary>
        /// Handle using the mouse to drag objects around.
        /// TODO: Synchronize this with gameboy vblank for stability?
        /// </summary>
        void HandleObjectDrag()
        {
            GameboySnapshot state = emu.LatestSnapshot;

            foreach (var obj in state.GetObjects())
            {
                FRect rect = (obj.GetRectangle() + new Vector2(0, 16));

                bool inTransition = emu.ReadSymbolByte("wScreenTransitionState") == 5;
                int direction = emu.ReadSymbolByte("wScreenTransitionDirection") & 3;

                if (inTransition)
                {
                    if ((obj.Enabled & 3) != 2)
                    {
                        // TODO: handle large rooms
                        switch (direction)
                        {
                            case 0:
                                rect -= new Vector2(0, 128);
                                break;
                            case 1:
                                rect += new Vector2(160, 0);
                                break;
                            case 2:
                                rect += new Vector2(0, 128);
                                break;
                            case 3:
                                rect -= new Vector2(160, 0);
                                break;
                        }
                    }
                }

                rect -= new Vector2(state.CameraX, state.CameraY);

                base.AddRect(rect * Scale, Color.DarkOrange, Scale);
            }
        }
    }
}
