using System.Diagnostics;
using System.Runtime.InteropServices;
using SameBoySharp;
using Emulation;
using SDL;
using System.ComponentModel;

namespace LynnaLab;

/// <summary>
/// An input accepted by EmulatorRenderer.
/// </summary>
public enum EmulatorKey
{
    Right,
    Left,
    Up,
    Down,
    A,
    B,
    Select,
    Start,
    Speedup,
}

/// <summary>
/// Helper class to render an emulator instance in ImGui. Also deals with audio and input.
/// </summary>
public unsafe class EmulatorRenderer : Frame, IDisposable
{
    const uint SAMPLE_RATE = 44100;

    // ================================================================================
    // Constructors
    // ================================================================================

    public EmulatorRenderer(EmulatorManager manager) : base("Emulator")
    {
        this.emu = manager.Emulator;
        this.Manager = manager;
        this.scale = Top.GlobalConfig.EmulatorScale;

        base.WindowFlags |= ImGuiWindowFlags.NoScrollbar;
        base.AddStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

        base.RequestSize(EmulatorContentSize);

        if (Top.AudioAvailable)
            SetupAudio();

        Top.GlobalConfig.PropertyChanged += OnGlobalConfigPropertyChanged;
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

        SetVolume(Top.GlobalConfig.EmulatorVolume);
        SDL3.SDL_ResumeAudioStreamDevice(audioStream);
    }

    // ================================================================================
    // Properties
    // ================================================================================

    public int Scale { get { return scale; } }
    public EmulatorManager Manager { get; init; }
    public OraclesEmulator Emulator { get { return emu; } }

    Vector2 EmulatorContentSize { get { return new Vector2(160, 144) * Scale + new Vector2(0, ImGui.GetFrameHeight() * 2); } }

    // ================================================================================
    // Variables
    // ================================================================================

    OraclesEmulator emu;
    SDL_AudioStream* audioStream;
    ScreenWidget renderWidget;

    int scale;
    bool muted = false;

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGui.BeginGroup();

        RenderRibbon();
        renderWidget.Render();

        #if DEBUG
        ImGui.Text($"FPS: {renderWidget.FramesPerSecond:F1}");
        #endif

        SameBoy.GBKeyMask mask = 0;
        bool turbo = false;

        // Handle input
        if (Top.GlobalConfig.EmulatorUnfocusedInput || ImGui.IsWindowFocused())
        {
            foreach (var (imKey, emulatorKey) in Top.GlobalConfig.EmulatorKeybindings.KeyMap)
            {
                if (!ImGui.IsKeyDown(imKey))
                    continue;

                if (emulatorKey == EmulatorKey.Speedup)
                    turbo = true;
                else
                    mask |= ToGBKeyMask(emulatorKey);
            }
        }

        // The SameBoy API docs claim that SetKeyMask is thread-safe, but I don't think this
        // entirely true. (TODO: redundant?)
        // https://github.com/LIJI32/SameBoy/issues/774
        lock (emu)
        {
            emu.GB.SetKeyMask(mask);
        }

        emu.GB.SetTurboMode(turbo);
        if (turbo)
        {
            // Don't let audio stream fall behind
            SDL3.SDL_ClearAudioStream(audioStream);
        }

        ImGui.EndGroup();
    }

    public void Dispose()
    {
        Top.GlobalConfig.PropertyChanged -= OnGlobalConfigPropertyChanged;
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
    // Protected methods
    // ================================================================================

    /// <summary>
    /// For now, to ensure integer scaling, just ignore DPI.
    /// </summary>
    protected override Vector2 RecalculateSizeOnDPIChange(Vector2 lastWindowSize)
    {
        return EmulatorContentSize + new Vector2(0, ImGui.GetFrameHeight());
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void RenderRibbon()
    {
        ImGui.BeginChild("Ribbon", new Vector2(EmulatorContentSize.X, 0.0f),
            ImGuiChildFlags.AutoResizeY);

        ImGuiX.Checkbox("Mute", muted, (b) => SetMuted(b));

        ImGui.SameLine();

        ImGuiX.Checkbox("Objects", new(() => Top.GlobalConfig.EmulatorDrawObjectBoxes));

        ImGui.SameLine();

        if (Manager.Paused)
        {
            if (ImGui.Button("Continue"))
                Manager.Continue();
        }
        else
        {
            if (ImGui.Button("Pause"))
                Manager.Interrupt();
        }

        ImGui.SameLine();

        ImGuiLL.HelpButton("Emulator.md", Manager.Project);

        ImGui.EndChild();
    }

    void OnGlobalConfigPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(GlobalConfig.EmulatorScale):
                SetScale(Top.GlobalConfig.EmulatorScale);
                break;
            case nameof(GlobalConfig.EmulatorVolume):
                SetVolume(Top.GlobalConfig.EmulatorVolume);
                break;
        }
    }

    void SetMuted(bool b)
    {
        lock (emu)
        {
            if (b)
                emu.GB.SetSampleRate(0);
            else
                emu.GB.SetSampleRate(SAMPLE_RATE);
        }
        muted = b;
    }

    void SetScale(int scale)
    {
        this.scale = scale;
        RequestSize(EmulatorContentSize);
    }

    void SetVolume(float volume)
    {
        volume *= 0.5f;
        if (audioStream != null)
            SDL3.SDL_SetAudioStreamGain(audioStream, volume);
    }

    // ================================================================================
    // Static methods
    // ================================================================================

    static SameBoy.GBKeyMask ToGBKeyMask(EmulatorKey key)
    {
        return key switch
        {
            EmulatorKey.Right => SameBoy.GBKeyMask.GB_KEY_RIGHT_MASK,
            EmulatorKey.Left => SameBoy.GBKeyMask.GB_KEY_LEFT_MASK,
            EmulatorKey.Up => SameBoy.GBKeyMask.GB_KEY_UP_MASK,
            EmulatorKey.Down => SameBoy.GBKeyMask.GB_KEY_DOWN_MASK,
            EmulatorKey.A => SameBoy.GBKeyMask.GB_KEY_A_MASK,
            EmulatorKey.B => SameBoy.GBKeyMask.GB_KEY_B_MASK,
            EmulatorKey.Select => SameBoy.GBKeyMask.GB_KEY_SELECT_MASK,
            EmulatorKey.Start => SameBoy.GBKeyMask.GB_KEY_START_MASK,
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };
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

        base.CenterX = false;
        base.CenterY = false;

        screenTexture = Top.Backend.CreateTexture(160, 144);
        screenTexture.CopyPixels(emu.PixelData, 160, 144, 4);

        // On vblank, update the texture
        emu.GB.SetVBlankCallback((type) =>
        {
            screenTexture.CopyPixels(emu.PixelData, 160, 144, 4);
        });
        emu.VBlankEvent += OnVBlank;

        overlayBoxUser = new(parent, this);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    readonly EmulatorOverlayBoxUser overlayBoxUser;

    EmulatorRenderer parent;
    OraclesEmulator emu;
    RgbaTexture screenTexture;

    List<OverlayBox> overlayBoxes = new();
    Dictionary<ushort, EmulatorObjectOverlayBox> overlayBoxMap = new();

    // FPS tracking
    readonly object fpsLock = new();
    long fpsStartTimestamp = Stopwatch.GetTimestamp();
    int framesSinceFpsUpdate;
    float framesPerSecond;

    // ================================================================================
    // Properties
    // ================================================================================

    public override Vector2 WidgetSize { get { return new Vector2(160, 144) * Scale; } }

    public OraclesEmulator Emulator { get { return emu; } }
    public int Scale { get { return parent.Scale; } }
    public float FramesPerSecond
    {
        get
        {
            lock (fpsLock)
            {
                UpdateFramesPerSecond();
                return framesPerSecond;
            }
        }
    }

    // ================================================================================
    // Public methods
    // ================================================================================


    public void Render()
    {
        base.RenderPrep();

        ImGui.BeginGroup();

        // Don't allow ObjectBoxes to be draw outside of the game screen
        Vector2 clipRectStart = ImGui.GetCursorScreenPos();
        ImGui.PushClipRect(clipRectStart, clipRectStart + WidgetSize, true);

        Vector2 renderPos = ImGui.GetCursorScreenPos();
        ImGuiX.DrawImage(screenTexture, Scale);
        // Add an InvisibleButton so that the window can't be dragged by clicking on the game screen.
        ImGui.SetCursorScreenPos(renderPos);
        ImGui.InvisibleButton("Screen covering", new Vector2(160, 144) * Scale);

        if (Top.GlobalConfig.EmulatorDrawObjectBoxes)
        {
            lock (emu)
            {
                UpdateOverlayBoxes();
                IOverlayBoxUser.UpdateOverlayBoxes(
                    null,
                    overlayBoxUser,
                    this,
                    overlayBoxes,
                    alwaysFullPrecision: true,
                    allowMove: true,
                    clipToCanvas: true);
            }
        }
        else
        {
            overlayBoxes.Clear();
            overlayBoxMap.Clear();
        }

        ImGui.PopClipRect();
        ImGui.EndGroup();
    }

    public void Dispose()
    {
        emu.VBlankEvent -= OnVBlank;
        Top.DoNextFrame(screenTexture.Dispose);
        screenTexture = null!;
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Invoked on every emulated VBlank. Differs from SameBoy's vblank callback, which does not
    /// always get invoked, only when it wants to present new pixel data.
    /// </summary>
    void OnVBlank()
    {
        lock (fpsLock)
        {
            framesSinceFpsUpdate++;
            UpdateFramesPerSecond();
        }
    }

    void UpdateFramesPerSecond()
    {
        long now = Stopwatch.GetTimestamp();
        long elapsed = now - fpsStartTimestamp;
        if (elapsed < Stopwatch.Frequency)
            return;

        framesPerSecond = framesSinceFpsUpdate * (float)Stopwatch.Frequency / elapsed;
        framesSinceFpsUpdate = 0;
        fpsStartTimestamp = now;
    }

    void UpdateOverlayBoxes()
    {
        GameboySnapshot state = emu.Snapshot;
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
        }
    }

    public class EmulatorOverlayBoxUser : LynnaLab.IOverlayBoxUser
    {
        readonly EmulatorRenderer renderer;
        readonly ScreenWidget screen;

        public EmulatorOverlayBoxUser(EmulatorRenderer renderer, ScreenWidget screen)
        {
            this.renderer = renderer;
            this.screen = screen;
        }

        public bool SelectingRectangle { get { return false; } }
        public bool DraggingComponent { get; set; }
        public Vector2 DraggingComponentOffset { get; private set; }
        public float Scale { get { return screen.Scale; } }

        public OverlayBox? Selected
        {
            get
            {
                if (renderer.Manager.SelectedObject == null ||
                    renderer.Manager.SelectedObject.Enabled == 0)
                    return null;
                return new EmulatorObjectOverlayBox(screen, renderer.Manager.SelectedObject.Address);
            }
        }

        public void SetSelected(OverlayBox comp, bool startDrag = false, Vector2? dragOffset = null)
        {
            if (comp != Selected || startDrag)
            {
                DraggingComponent = startDrag;
                if (startDrag && dragOffset != null)
                    DraggingComponentOffset = (Vector2)dragOffset;
            }

            if (Selected == comp)
                return;

            renderer.Manager.SetSelectedObject((comp as EmulatorObjectOverlayBox)?.address);
        }
    }
}

/// <summary>
/// Draggable object in the ScreenWidget
/// </summary>
record class EmulatorObjectOverlayBox : OverlayBox
{
    readonly ScreenWidget parent;
    public readonly ushort address;

    // ================================================================================
    // Constructors
    // ================================================================================

    public EmulatorObjectOverlayBox(ScreenWidget parent, ushort address) : base((RoomLayoutEditor?)null)
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
        set { GetObject().X = (byte)(value + Emulator.Snapshot.CameraX); }
    }
    public override int Y
    {
        get { return (int)GetRect().Center.Y; }
        set { GetObject().Y = (byte)(value + Emulator.Snapshot.CameraY - 16); }
    }

    public override int BoxWidth { get { return (int)GetRect().Width; } }
    public override int BoxHeight { get { return (int)GetRect().Height; } }
    public override FRect BoxRectangle { get { return GetRect(); } }

    public override void Render(bool selected)
    {
        parent.AddRect(GetRect() * parent.Scale, Color.DarkOrange, parent.Scale);
    }

    public override string GetTransactionIdentifier()
    {
        return $"emulatorObject#{address:X4}";
    }

    // ================================================================================
    // Other properties
    // ================================================================================

    OraclesEmulator Emulator { get { return parent.Emulator; } }
    GameboyStateBase State { get { return Emulator.Snapshot; } }

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
