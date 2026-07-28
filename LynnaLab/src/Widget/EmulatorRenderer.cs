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
    // Properties
    // ================================================================================

    public int Scale { get { return scale; } }
    public OraclesEmulator Emulator { get { return emu; } }

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
}


/// <summary>
/// A SizedWidget that renders the emulator image.
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

        overlayBoxUser = new(this);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    EmulatorRenderer parent;
    OraclesEmulator emu;
    RgbaTexture screenTexture;

    EmulatorOverlayBoxUser overlayBoxUser;
    List<OverlayBox> overlayBoxes = new();
    Dictionary<ushort, EmulatorObjectOverlayBox> overlayBoxMap = new();

    // ================================================================================
    // Properties
    // ================================================================================

    public override Vector2 WidgetSize { get { return new Vector2(160, 144) * Scale; } }

    public OraclesEmulator Emulator { get { return emu; } }
    public int Scale { get { return parent.Scale; } }

    // ================================================================================
    // Public methods
    // ================================================================================


    public void Render()
    {
        base.RenderPrep();

        ImGuiX.DrawImage(screenTexture, Scale);

        lock (emu)
        {
            UpdateOverlayBoxes();
            IOverlayBoxUser.UpdateOverlayBoxes(null, overlayBoxUser, this, overlayBoxes);
        }
    }

    public void Dispose()
    {
        screenTexture.Dispose();
        screenTexture = null!;
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void UpdateOverlayBoxes()
    {
        GameboySnapshot state = emu.LatestSnapshot;
        HashSet<ushort> seen = new();

        foreach (var obj in state.GetObjects())
        {
            ushort addr = obj.Address;
            seen.Add(addr);

            if (!overlayBoxMap.ContainsKey(addr))
            {
                var com = new EmulatorObjectOverlayBox(this, addr);
                overlayBoxMap[addr] = com;
                overlayBoxes.Add(com);
            }
        }

        var toRemove = overlayBoxMap.Keys.Where(addr => !seen.Contains(addr)).ToList();
        foreach (var addr in toRemove)
        {
            var com = overlayBoxMap[addr];
            overlayBoxMap.Remove(addr);
            overlayBoxes.Remove(com);
            if (overlayBoxUser.Selected == com)
                overlayBoxUser.Selected = null;
        }
    }

    public class EmulatorOverlayBoxUser : LynnaLab.IOverlayBoxUser
    {
        readonly ScreenWidget screen;

        public EmulatorOverlayBoxUser(ScreenWidget screen)
        {
            this.screen = screen;
        }

        public bool SelectingRectangle { get { return false; } }
        public bool DraggingComponent { get; set; }
        public Vector2 DraggingComponentOffset { get; private set; }
        public OverlayBox Selected { get; set; }
        public float Scale { get { return screen.Scale; } }

        public void SetSelected(OverlayBox comp, bool startDrag = false, Vector2? dragOffset = null)
        {
            // Even if the OverlayBox object changed, the underlying data may actually be the same as before.
            // "isDifferent" checks the underlying data rather than the OverlayBox themselves.
            bool isDifferent = comp == null || !comp.Compare(Selected);
            if (isDifferent || startDrag)
            {
                DraggingComponent = startDrag;
                if (startDrag)
                    DraggingComponentOffset = (Vector2)dragOffset;
            }

            if (Selected == comp)
                return;
            OverlayBox old = Selected;
            Selected = comp;
        }
    }
}

/// <summary>
/// Draggable object in the ScreenWidget
/// </summary>
class EmulatorObjectOverlayBox : OverlayBox
{
    readonly ScreenWidget parent;
    readonly ushort address;

    // ================================================================================
    // Constructors
    // ================================================================================

    public EmulatorObjectOverlayBox(ScreenWidget parent, ushort address) : base(null)
    {
        this.parent = parent;
        this.address = address;
    }

    // ================================================================================
    // OverlayBox implementations
    // ================================================================================

    public override Color? BoxColor { get { return null; } }
    public override bool Deletable { get { return false; } }
    public override bool HasXY { get { return true; } }
    public override bool HasShortenedXY { get { return false; } }

    // Not: Get is not symmetrical with Set, as Set doesn't handle transitions between screens
    // the same way. Shouldn't really matter.
    public override int X
    {
        get { return (int)GetRect().Center.X; }
        set { Emulator.Wram[address - 0xc000 + 0xd] = (byte)(value + Emulator.LatestSnapshot.CameraX); }
    }
    public override int Y
    {
        get { return (int)GetRect().Center.Y; }
        set { Emulator.Wram[address - 0xc000 + 0xb] = (byte)(value + Emulator.LatestSnapshot.CameraY - 16); }
    }

    public override int BoxWidth { get { return (int)GetRect().Width; } }
    public override int BoxHeight { get { return (int)GetRect().Height; } }
    public override FRect BoxRectangle { get { return GetRect(); } }

    public override void Render(bool selected)
    {
        parent.AddRect(GetRect() * parent.Scale, Color.DarkOrange, parent.Scale);
    }

    public override bool Compare(OverlayBox com)
    {
        return address == (com as EmulatorObjectOverlayBox)?.address;
    }

    public override string GetTransactionIdentifier()
    {
        return $"emulatorObject#{address:X4}";
    }

    // ================================================================================
    // Other properties
    // ================================================================================

    OraclesEmulator Emulator { get { return parent.Emulator; } }
    GameboyStateBase State { get { return Emulator.LatestSnapshot; } }

    // ================================================================================
    // Private methods
    // ================================================================================

    EmulatorObject GetObject()
    {
        return new EmulatorObject(State, address);
    }

    FRect GetRect()
    {
        var obj = GetObject();
        FRect rect = obj.GetRectangle() + new Vector2(0, 16);

        bool inTransition = State.ReadSymbolByte("wScreenTransitionState") == 5;
        int direction = State.ReadSymbolByte("wScreenTransitionDirection") & 3;

        Vector2 roomSize = State.ReadSymbolByte("wRoomIsLarge") != 0
            ? new Vector2(240, 176) : new Vector2(160, 128);

        if (inTransition)
        {
            if ((obj.Enabled & 3) != 2)
            {
                switch (direction)
                {
                    case 0:
                        rect -= new Vector2(0, roomSize.Y);
                        break;
                    case 1:
                        rect += new Vector2(roomSize.X, 0);
                        break;
                    case 2:
                        rect += new Vector2(0, roomSize.Y);
                        break;
                    case 3:
                        rect -= new Vector2(roomSize.X, 0);
                        break;
                }
            }
        }

        rect -= new Vector2(State.CameraX, State.CameraY);
        return rect;
    }
}
