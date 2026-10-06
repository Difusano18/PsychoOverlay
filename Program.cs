using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace PsychoOverlay;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using OverlayForm form = new();
        Application.Run(form);
    }
}

public sealed class OverlayForm : Form
{
    private enum EffectMode
    {
        Flow = 1,
        TextureTrip = 2,
        GlyphGlitch = 3,
        FullTrip = 4,
        Chaos = 5,
        Nilk = 6,
        NilkSeek = 7
    }

    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WDA_EXCLUDEFROMCAPTURE = 0x11;
    private const uint CURSOR_SHOWING = 0x00000001;
    private const int GWL_EXSTYLE = -20;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const int WM_HOTKEY = 0x0312;
    private const int ULW_ALPHA = 0x02;
    private const int BI_RGB = 0;
    private const uint DIB_RGB_COLORS = 0;
    private const byte AC_SRC_OVER = 0;
    private const byte AC_SRC_ALPHA = 1;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_NOREPEAT = 0x4000;
    private const float ModePaletteCycleSeconds = 96f;
    private const float NilkDatamoshIntensity = 0.51f;
    private const double NilkTotalDurationSeconds = 60d * 60d;
    private const double NilkPaletteBaseShuffleSeconds = 2d * 60d;
    private const double NilkFirstPaletteRefreshSeconds = 1d / 30d;
    private static readonly double[] NilkSeekPhaseTimesSeconds = [38d * 60d, 48d * 60d + 30d, 52d * 60d, 58d * 60d + 30d];
    private static readonly float[][][] NilkPalettes = LoadNilkPalettes();

    private readonly record struct NilkPaletteEvent(double TimeSeconds, int PaletteIndex);

    private const int HotkeyIntensityUp = 101;
    private const int HotkeyIntensityDown = 102;
    private const int HotkeyTogglePause = 103;
    private const int HotkeyExit = 104;
    private const int HotkeyFlowMode = 105;
    private const int HotkeyTextureMode = 106;
    private const int HotkeyGlyphMode = 107;
    private const int HotkeyFullMode = 108;
    private const int HotkeyNextMode = 109;
    private const int HotkeyPrevMode = 110;
    private const int HotkeyFlowModeF = 111;
    private const int HotkeyTextureModeF = 112;
    private const int HotkeyGlyphModeF = 113;
    private const int HotkeyFullModeF = 114;
    private const int HotkeyIntensityDownF = 115;
    private const int HotkeyIntensityUpF = 116;
    private const int HotkeyTogglePauseF = 117;
    private const int HotkeyExitF = 118;
    private const int HotkeyNextModeF = 119;
    private const int HotkeyPrevModeF = 120;
    private const int HotkeyChaosMode = 121;
    private const int HotkeyToggleScreenCapture = 122;
    private const int HotkeyNilkMode = 123;
    private const int HotkeyNilkSeekMode = 124;
    private const int HotkeyNilkNextPhase = 125;
    private const int HotkeyNilkPrevPhase = 126;

    private static readonly char[] GlyphBank =
        "░▒▓█▓▒░ ᚠᚢᚦᚨᚱᚲ ΨΩΔΛΣΞ ЖЙФЮЯ 目電幻夢零壱弐参 NILK VOID COSMOS LSD 0123456789 @#$%&*<>/\\".ToCharArray();

    private readonly System.Windows.Forms.Timer timer;
    private readonly System.Windows.Forms.Timer cursorTimer;
    private readonly System.Windows.Forms.Timer nilkStateTimer;
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private Random nilkRandom = new();
    private readonly List<NilkPaletteEvent> nilkPaletteSchedule = [];
    private int nilkPaletteSchedulePosition;
    private readonly Rectangle desktopBounds = SystemInformation.VirtualScreen;
    private readonly Color[] nilkPalette =
    [
        Color.FromArgb(255, 23, 8, 52),
        Color.FromArgb(255, 91, 0, 139),
        Color.FromArgb(255, 196, 39, 198),
        Color.FromArgb(255, 255, 71, 154),
        Color.FromArgb(255, 255, 178, 76),
        Color.FromArgb(255, 48, 255, 198),
        Color.FromArgb(255, 55, 109, 255),
        Color.FromArgb(255, 13, 9, 38)
    ];

    private readonly Font hintFont = new("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font glyphFont = new("Consolas", 11f, FontStyle.Bold, GraphicsUnit.Point);
    private readonly Font glyphFontLarge = new("Segoe UI Symbol", 26f, FontStyle.Bold, GraphicsUnit.Point);

    private readonly Bitmap? noiseTexture;
    private readonly Bitmap? cosmosTexture;
    private readonly Bitmap? psychedelicTexture;
    private readonly Bitmap? voidTexture;

    private Bitmap? frameBitmap;
    private Graphics? frameGraphics;
    private Bitmap? tripBitmap;
    private Graphics? tripGraphics;
    private Bitmap? previousTripBitmap;
    private Graphics? previousTripGraphics;
    private WebView2? gpuView;
    private byte[]? tripPixelBuffer;
    private byte[]? previousTripPixelBuffer;
    private byte[]? noisePixels;
    private nint memoryDc;
    private nint dibBitmap;
    private nint oldBitmap;
    private nint bits;
    private int surfaceWidth;
    private int surfaceHeight;
    private int tripWidth;
    private int tripHeight;
    private int noiseWidth;
    private int noiseHeight;
    private int noiseStride;

    private EffectMode mode = EffectMode.FullTrip;
    private float time;
    private float previousSeconds;
    private float intensity = 0.52f;
    private float targetIntensity = 0.52f;
    private float surge;
    private float hintSeconds = 7f;
    private bool paused;
    private bool needsFrame = true;
    private bool gpuRendererActive;
    private string? gpuRendererError;
    private string? hintOverride;
    private int cursorHideCalls;
    private int lastCursorX = int.MinValue;
    private int lastCursorY = int.MinValue;
    private bool captureActive;
    private double nilkElapsedSeconds;
    private int nilkPaletteIndex = -1;
    private int lastPostedNilkPaletteIndex = -1;
    private int nilkSeekPhaseIndex = -1;
    private long nilkLastUpdateTimestamp;
    private int captureWidth;
    private int captureHeight;
    private nint previousForegroundWindow;

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = desktopBounds;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.Black;
        DoubleBuffered = false;
        KeyPreview = false;

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);

        string root = FindAssetRoot();
        noiseTexture = LoadAsset(root, "Assets", "Textures", "Extra", "Noise", "WavyBlotchNoiseDetailed.png");
        cosmosTexture = LoadAsset(root, "Assets", "Textures", "Extra", "Cosmos.png");
        psychedelicTexture = LoadAsset(root, "Assets", "Textures", "Extra", "Psychedelic.png");
        voidTexture = LoadAsset(root, "Assets", "Textures", "Extra", "Void.png");

        CacheNoiseTexture();

        // The renderer is intentionally low-res; 16 ms lets Windows present as fast as it can.
        timer = new System.Windows.Forms.Timer { Interval = 16 };
        timer.Tick += (_, _) => RenderTick();
        cursorTimer = new System.Windows.Forms.Timer { Interval = 16 };
        cursorTimer.Tick += (_, _) =>
        {
            UpdateNativeCursorVisibility();
            PostCursorPosition();
        };
        nilkStateTimer = new System.Windows.Forms.Timer { Interval = 33 };
        nilkStateTimer.Tick += (_, _) => UpdateNilkShaderState();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        MakeClickThrough();
        _ = SetWindowDisplayAffinity(Handle, WDA_EXCLUDEFROMCAPTURE);
        RegisterOverlayHotkeys();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        previousForegroundWindow = GetForegroundWindow();
        if (previousForegroundWindow == Handle)
            previousForegroundWindow = 0;

        if (await TryStartGpuRendererAsync())
        {
            MakeInteractive();
            return;
        }

        ShowHint($"Fullscreen Nilk needs WebView2 ({gpuRendererError ?? "initialization failed"}).");
        StartCpuRenderer();
    }

    private void StartCpuRenderer()
    {
        CreateRenderSurface(Math.Max(1, Width), Math.Max(1, Height));
        timer.Start();
        RenderTick();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Stop();
        cursorTimer.Stop();
        nilkStateTimer.Stop();
        RestoreNativeCursor();
        gpuView?.Dispose();
        gpuView = null;
        UnregisterOverlayHotkeys();
        DisposeAssets();
        DisposeRenderSurface();
        base.OnFormClosed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            switch (m.WParam.ToInt32())
            {
                case HotkeyIntensityUp:
                case HotkeyIntensityUpF:
                    targetIntensity = Math.Clamp(targetIntensity + 0.06f, 0f, (int)mode >= (int)EffectMode.Nilk ? 1f : 0.72f);
                    ShowHint();
                    break;
                case HotkeyIntensityDown:
                case HotkeyIntensityDownF:
                    targetIntensity = Math.Clamp(targetIntensity - 0.06f, 0f, (int)mode >= (int)EffectMode.Nilk ? 1f : 0.72f);
                    ShowHint();
                    break;
                case HotkeyTogglePause:
                case HotkeyTogglePauseF:
                    paused = !paused;
                    nilkLastUpdateTimestamp = Stopwatch.GetTimestamp();
                    ShowHint();
                    break;
                case HotkeyFlowMode:
                    SetEffectMode(EffectMode.Flow);
                    break;
                case HotkeyTextureMode:
                    SetEffectMode(EffectMode.TextureTrip);
                    break;
                case HotkeyGlyphMode:
                    SetEffectMode(EffectMode.GlyphGlitch);
                    break;
                case HotkeyFullMode:
                    SetEffectMode(EffectMode.FullTrip);
                    break;
                case HotkeyChaosMode:
                    SetEffectMode(EffectMode.Chaos);
                    break;
                case HotkeyNilkMode:
                    SetEffectMode(EffectMode.Nilk);
                    break;
                case HotkeyNilkSeekMode:
                    SetEffectMode(EffectMode.NilkSeek);
                    break;
                case HotkeyNilkNextPhase:
                    StepNilkSeekPhase(1);
                    break;
                case HotkeyNilkPrevPhase:
                    StepNilkSeekPhase(-1);
                    break;
                case HotkeyPrevMode:
                case HotkeyPrevModeF:
                    CycleEffectMode(-1);
                    break;
                case HotkeyNextMode:
                case HotkeyNextModeF:
                    CycleEffectMode(1);
                    break;
                case HotkeyToggleScreenCapture:
                    ToggleScreenCapture();
                    break;
                case HotkeyExit:
                case HotkeyExitF:
                    Close();
                    return;
            }
        }

        base.WndProc(ref m);
    }

    private void ShowHint(string? message = null)
    {
        hintOverride = message;
        hintSeconds = 2.6f;
        needsFrame = true;
        PushGpuState();
    }

    private void SetEffectMode(EffectMode nextMode)
    {
        if ((int)nextMode >= (int)EffectMode.Nilk && !gpuRendererActive && gpuRendererError is not null)
        {
            ShowHint($"Fullscreen Nilk needs WebView2 ({gpuRendererError}).");
            return;
        }

        if ((int)nextMode >= (int)EffectMode.Nilk && (int)mode < (int)EffectMode.Nilk)
            targetIntensity = 1f;
        else if ((int)nextMode < (int)EffectMode.Nilk && (int)mode >= (int)EffectMode.Nilk)
            targetIntensity = 0.52f;

        bool enteringSeekMode = mode != nextMode && nextMode == EffectMode.NilkSeek;
        bool startNilkRun = nextMode == EffectMode.Nilk && mode != EffectMode.Nilk ||
                            enteringSeekMode && (int)mode < (int)EffectMode.Nilk;
        if (mode != nextMode)
        {
            if (startNilkRun)
                StartNilkRun();
            else if ((int)mode >= (int)EffectMode.Nilk && (int)nextMode < (int)EffectMode.Nilk)
                StopNilkRun();
        }

        mode = nextMode;
        if (enteringSeekMode)
            SetNilkSeekPhase(0);
        ShowHint();
    }

    private void StepNilkSeekPhase(int direction)
    {
        if (mode != EffectMode.NilkSeek || nilkSeekPhaseIndex < 0)
            return;

        int nextPhase = Math.Clamp(nilkSeekPhaseIndex + direction, 0, NilkSeekPhaseTimesSeconds.Length - 1);
        if (nextPhase == nilkSeekPhaseIndex)
            return;

        SetNilkSeekPhase(nextPhase);
        ShowHint();
    }

    private void SetNilkSeekPhase(int phaseIndex)
    {
        nilkSeekPhaseIndex = Math.Clamp(phaseIndex, 0, NilkSeekPhaseTimesSeconds.Length - 1);
        nilkElapsedSeconds = NilkSeekPhaseTimesSeconds[nilkSeekPhaseIndex];
        UpdateNilkPaletteForTime();
        nilkLastUpdateTimestamp = Stopwatch.GetTimestamp();
        nilkStateTimer.Start();
        PostNilkShaderState();
    }

    private void ToggleScreenCapture()
    {
        if (gpuView?.CoreWebView2 is null)
            return;

        nint foreground = GetForegroundWindow();
        if (foreground != 0 && foreground != Handle)
            previousForegroundWindow = foreground;

        TopMost = true;
        MakeInteractive();
        _ = gpuView.CoreWebView2.ExecuteScriptAsync("window.toggleScreenCapture && window.toggleScreenCapture()");
    }

    private void HandleGpuWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string message;
        try
        {
            message = e.TryGetWebMessageAsString();
        }
        catch
        {
            return;
        }

        if (message == "capture-started")
        {
            captureActive = true;
            TopMost = true;
            lastCursorX = int.MinValue;
            lastCursorY = int.MinValue;
            UpdateNativeCursorVisibility();
            PostCursorPosition();
            cursorTimer.Start();
            MakeClickThrough();
            if (previousForegroundWindow != 0)
                _ = SetForegroundWindow(previousForegroundWindow);
        }
        else if (message.StartsWith("capture-size:", StringComparison.Ordinal))
        {
            string[] dimensions = message["capture-size:".Length..].Split(',');
            if (dimensions.Length >= 2 &&
                int.TryParse(dimensions[0], out int width) &&
                int.TryParse(dimensions[1], out int height))
            {
                captureWidth = width;
                captureHeight = height;
                string surface = dimensions.Length >= 3 ? dimensions[2] : "unknown";
                bool isNotMonitor = surface == "window" || surface == "browser" ||
                                    (surface == "unknown" && !MatchesMonitorSize(width, height));
                if (isNotMonitor)
                {
                    _ = gpuView?.CoreWebView2?.ExecuteScriptAsync(
                        "window.rejectNonMonitorCapture && window.rejectNonMonitorCapture()");
                    return;
                }

                MatchCaptureToDisplay(width, height);
                PostCursorPosition();
            }
        }
        else if (message.StartsWith("capture-", StringComparison.Ordinal))
        {
            captureActive = false;
            captureWidth = 0;
            captureHeight = 0;
            cursorTimer.Stop();
            RestoreNativeCursor();
            Bounds = desktopBounds;
            TopMost = true;
            MakeInteractive();
        }
    }

    private void MatchCaptureToDisplay(int captureWidth, int captureHeight)
    {
        if (captureWidth <= 0 || captureHeight <= 0)
            return;

        Rectangle bestBounds = desktopBounds;
        double bestScore = DisplaySizeScore(desktopBounds, captureWidth, captureHeight);
        foreach (Screen screen in Screen.AllScreens)
        {
            Rectangle candidate = screen.Bounds;
            double score = DisplaySizeScore(candidate, captureWidth, captureHeight);
            if (score < bestScore)
            {
                bestBounds = candidate;
                bestScore = score;
            }
        }

        Bounds = bestBounds;
        lastCursorX = int.MinValue;
        lastCursorY = int.MinValue;
        TopMost = true;
    }

    private static double DisplaySizeScore(Rectangle bounds, int captureWidth, int captureHeight)
    {
        double aspectDelta = Math.Abs(Math.Log((bounds.Width / (double)bounds.Height) / (captureWidth / (double)captureHeight)));
        double scaleDelta = Math.Abs(Math.Log(bounds.Width / (double)captureWidth)) +
                            Math.Abs(Math.Log(bounds.Height / (double)captureHeight));
        return aspectDelta * 100d + scaleDelta;
    }

    private bool MatchesMonitorSize(int captureWidth, int captureHeight)
    {
        double bestScore = DisplaySizeScore(desktopBounds, captureWidth, captureHeight);
        foreach (Screen screen in Screen.AllScreens)
            bestScore = Math.Min(bestScore, DisplaySizeScore(screen.Bounds, captureWidth, captureHeight));
        return bestScore <= 0.45d;
    }

    private void PostCursorPosition()
    {
        if (gpuView?.CoreWebView2 is null || !GetCursorPos(out NativePoint point))
            return;

        if (point.X == lastCursorX && point.Y == lastCursorY)
            return;

        lastCursorX = point.X;
        lastCursorY = point.Y;
        float u = (point.X - Bounds.Left) / (float)Math.Max(Bounds.Width, 1);
        float v = 1f - (point.Y - Bounds.Top) / (float)Math.Max(Bounds.Height, 1);
        bool visible = u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        string uValue = u.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string vValue = v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string visibleValue = visible ? "true" : "false";
        gpuView.CoreWebView2.PostWebMessageAsJson(
            $"{{\"type\":\"cursor\",\"u\":{uValue},\"v\":{vValue},\"visible\":{visibleValue}}}");
    }

    private void UpdateNativeCursorVisibility()
    {
        if (!captureActive || !GetCursorPos(out NativePoint point))
        {
            if (!captureActive)
                RestoreNativeCursor();
            return;
        }

        if (Bounds.Contains(point.X, point.Y))
            KeepNativeCursorHidden();
        else
            RestoreNativeCursor();
    }

    private void KeepNativeCursorHidden()
    {
        CursorInfo cursorInfo = new() { Size = (uint)Marshal.SizeOf<CursorInfo>() };
        if (!GetCursorInfo(ref cursorInfo))
            return;

        if ((cursorInfo.Flags & CURSOR_SHOWING) == 0)
            return;

        for (int attempt = 0; attempt < 32; attempt++)
        {
            int visibility = ShowCursor(false);
            cursorHideCalls++;
            if (visibility < 0)
                break;
        }
    }

    private void RestoreNativeCursor()
    {
        while (cursorHideCalls > 0)
        {
            ShowCursor(true);
            cursorHideCalls--;
        }
    }

    private void CycleEffectMode(int direction)
    {
        const int modeCount = 7;
        int next = ((int)mode - 1 + direction + modeCount) % modeCount + 1;
        SetEffectMode((EffectMode)next);
    }

    private void UpdateTimerInterval()
    {
        timer.Interval = 16;
    }

    private void RenderTick()
    {
        if (frameBitmap is null || frameGraphics is null || memoryDc == 0 || surfaceWidth != Width || surfaceHeight != Height)
            CreateRenderSurface(Math.Max(1, Width), Math.Max(1, Height));

        float seconds = (float)stopwatch.Elapsed.TotalSeconds;
        float dt = Math.Clamp(seconds - previousSeconds, 0f, 0.10f);
        previousSeconds = seconds;

        float oldIntensity = intensity;
        if (!paused)
        {
            time += dt;
            needsFrame = true;
        }

        intensity = Lerp(intensity, targetIntensity, 1f - MathF.Pow(0.002f, dt));
        surge = ComputeSurge();

        if (MathF.Abs(intensity - oldIntensity) > 0.001f)
            needsFrame = true;

        if (hintSeconds > 0f)
        {
            hintSeconds -= dt;
            needsFrame = true;
        }

        if (!needsFrame)
            return;

        needsFrame = false;
        Graphics g = frameGraphics!;
        g.Clear(Color.Transparent);

        RenderLsdOverlay(g, surfaceWidth, surfaceHeight);
        UpdateLayeredWindowFast();
    }

    private float ComputeSurge()
    {
        float modeBoost = 0.035f * ((int)mode - 1);
        float slow = Wave01(time * 0.42f);
        float fast = Wave01(time * 1.28f + 1.1f);
        float hit = MathF.Pow(Wave01(time * 0.19f + 0.7f), 7f);
        return Math.Clamp(modeBoost + slow * 0.055f + fast * 0.030f + hit * 0.095f, 0f, 0.26f);
    }

    private float LsdPower
    {
        get
        {
            float modeScale = mode switch
            {
                EffectMode.Flow => 0.72f,
                EffectMode.TextureTrip => 0.92f,
                EffectMode.GlyphGlitch => 0.98f,
                EffectMode.FullTrip => 1.24f,
                EffectMode.Chaos => 1.34f,
                _ => 1f
            };
            return Math.Clamp(intensity * modeScale + surge, 0f, 0.78f);
        }
    }

    private void CreateRenderSurface(int width, int height)
    {
        DisposeRenderSurface();

        surfaceWidth = width;
        surfaceHeight = height;

        nint screenDc = GetDC(0);
        memoryDc = CreateCompatibleDC(screenDc);

        BitmapInfo bmi = new();
        bmi.Header.Size = Marshal.SizeOf<BitmapInfoHeader>();
        bmi.Header.Width = width;
        bmi.Header.Height = -height;
        bmi.Header.Planes = 1;
        bmi.Header.BitCount = 32;
        bmi.Header.Compression = BI_RGB;

        dibBitmap = CreateDIBSection(screenDc, ref bmi, DIB_RGB_COLORS, out bits, 0, 0);
        ReleaseDC(0, screenDc);

        if (dibBitmap == 0 || bits == 0)
            throw new InvalidOperationException("Could not create a 32-bit DIB section for the overlay.");

        oldBitmap = SelectObject(memoryDc, dibBitmap);
        frameBitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, bits);
        frameGraphics = Graphics.FromImage(frameBitmap);
        ConfigureGraphics(frameGraphics);
        needsFrame = true;
    }

    private static void ConfigureGraphics(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.CompositingMode = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighSpeed;
        g.InterpolationMode = InterpolationMode.Low;
        g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
    }

    private void RenderLsdOverlay(Graphics g, int w, int h)
    {
        if (intensity > 0.004f && (int)mode < (int)EffectMode.Nilk)
        {
            float p = LsdPower;
            RenderFullTripLowRes(g, w, h, p);
        }

        DrawHint(g, w, h);
    }

    private async Task<bool> TryStartGpuRendererAsync()
    {
        try
        {
            string userDataFolder = Path.Combine(Path.GetTempPath(), "PsychoOverlay_WebView2");
            string overlayContentFolder = Path.Combine(userDataFolder, "overlay-content");
            Directory.CreateDirectory(overlayContentFolder);
            string nilkNoiseAsset = Path.Combine(AppContext.BaseDirectory, "Assets", "Textures", "Extra", "Noise", "NilkPerlinNoise.png");
            if (File.Exists(nilkNoiseAsset))
                File.Copy(nilkNoiseAsset, Path.Combine(overlayContentFolder, "nilk-noise.png"), overwrite: true);
            await File.WriteAllTextAsync(
                Path.Combine(overlayContentFolder, "overlay.html"),
                BuildGpuOverlayHtml(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            gpuView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.Transparent,
                AllowExternalDrop = false
            };

            Controls.Add(gpuView);
            gpuView.BringToFront();
            await gpuView.EnsureCoreWebView2Async(environment);
            CoreWebView2 core = gpuView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.IsWebMessageEnabled = true;
            core.SetVirtualHostNameToFolderMapping(
                "psychooverlay.local",
                overlayContentFolder,
                CoreWebView2HostResourceAccessKind.DenyCors);
            core.WebMessageReceived += HandleGpuWebMessage;
            core.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess)
                    return;

                // A Nilk state can be posted while the document is still loading.
                // Force the palette to be sent again once its WebGL listener exists.
                lastPostedNilkPaletteIndex = -1;
                PushGpuState();
            };
            gpuRendererActive = true;
            gpuView.Source = new Uri("https://psychooverlay.local/overlay.html");
            PushGpuState();
            return true;
        }
        catch (Exception ex)
        {
            gpuRendererActive = false;
            gpuRendererError = ex.GetType().Name;
            if (gpuView is not null)
            {
                Controls.Remove(gpuView);
                gpuView.Dispose();
                gpuView = null;
            }

            return false;
        }
    }

    private static float[][][] LoadNilkPalettes()
    {
        using Stream? stream = typeof(OverlayForm).Assembly.GetManifestResourceStream("PsychoOverlay.NoxusReferences.NilkShaderPalettes.json");
        if (stream is null)
            throw new InvalidOperationException("The original Nilk palette resource is missing from the build.");

        using JsonDocument document = JsonDocument.Parse(stream, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip
        });
        float[][][] palettes = document.RootElement.EnumerateObject()
            .Select(palette => palette.Value.EnumerateArray()
                .Select(color => color.GetString()!.Split(',')
                    .Select(component => float.Parse(component.Trim(), System.Globalization.CultureInfo.InvariantCulture))
                    .ToArray())
                .ToArray())
            .ToArray();
        if (palettes.Length == 0 || palettes.Any(palette => palette.Length != 8 || palette.Any(color => color.Length != 3)))
            throw new InvalidOperationException("Nilk palettes must each contain eight RGB colors.");
        return palettes;
    }

    private void StartNilkRun()
    {
        nilkRandom = new Random(Random.Shared.Next());
        nilkElapsedSeconds = 0d;
        nilkSeekPhaseIndex = -1;
        nilkPaletteIndex = nilkRandom.Next(NilkPalettes.Length);
        BuildNilkPaletteSchedule();
        lastPostedNilkPaletteIndex = -1;
        nilkLastUpdateTimestamp = Stopwatch.GetTimestamp();
        nilkStateTimer.Start();
        PostNilkShaderState();
    }

    private void StopNilkRun()
    {
        UpdateNilkShaderState();
        nilkStateTimer.Stop();
        nilkElapsedSeconds = 0d;
        nilkPaletteIndex = -1;
        nilkPaletteSchedule.Clear();
        nilkPaletteSchedulePosition = 0;
        lastPostedNilkPaletteIndex = -1;
        nilkSeekPhaseIndex = -1;
        nilkLastUpdateTimestamp = 0;
    }

    private void UpdateNilkShaderState()
    {
        long now = Stopwatch.GetTimestamp();
        bool advanceTimeline = (mode == EffectMode.Nilk || mode == EffectMode.NilkSeek) && !paused;
        if (nilkLastUpdateTimestamp != 0 && advanceTimeline)
        {
            double dt = (now - nilkLastUpdateTimestamp) / (double)Stopwatch.Frequency;
            nilkElapsedSeconds = Math.Min(NilkTotalDurationSeconds, nilkElapsedSeconds + Math.Max(0d, dt));
        }
        nilkLastUpdateTimestamp = now;

        if (nilkPaletteIndex < 0)
            nilkPaletteIndex = nilkRandom.Next(NilkPalettes.Length);

        if (advanceTimeline)
            UpdateNilkPaletteForTime();

        if (advanceTimeline && nilkElapsedSeconds >= NilkTotalDurationSeconds)
            nilkStateTimer.Stop();

        PostNilkShaderState();
    }

    private void BuildNilkPaletteSchedule()
    {
        nilkPaletteSchedule.Clear();
        nilkPaletteSchedulePosition = 0;
        nilkPaletteSchedule.Add(new NilkPaletteEvent(0d, nilkPaletteIndex));

        // The source manager refreshes once on its first update, then schedules
        // subsequent changes using the same independent probability checks.
        double changeTime = NilkFirstPaletteRefreshSeconds;
        while (changeTime < NilkTotalDurationSeconds)
        {
            nilkPaletteSchedule.Add(new NilkPaletteEvent(changeTime, nilkRandom.Next(NilkPalettes.Length)));
            changeTime += NextNilkPaletteIntervalSeconds();
        }
    }

    private void UpdateNilkPaletteForTime()
    {
        if (nilkPaletteSchedule.Count == 0)
            return;

        while (nilkPaletteSchedulePosition + 1 < nilkPaletteSchedule.Count &&
               nilkPaletteSchedule[nilkPaletteSchedulePosition + 1].TimeSeconds <= nilkElapsedSeconds)
        {
            nilkPaletteSchedulePosition++;
        }

        while (nilkPaletteSchedulePosition > 0 &&
               nilkPaletteSchedule[nilkPaletteSchedulePosition].TimeSeconds > nilkElapsedSeconds)
        {
            nilkPaletteSchedulePosition--;
        }

        nilkPaletteIndex = nilkPaletteSchedule[nilkPaletteSchedulePosition].PaletteIndex;
    }

    private double NextNilkPaletteIntervalSeconds()
    {
        double duration = NilkPaletteBaseShuffleSeconds;
        if (nilkRandom.Next(3) == 0)
            duration = 60d;
        if (nilkRandom.Next(6) == 0)
            duration = 24d;
        if (nilkRandom.Next(9) == 0)
            duration = 3d;
        if (nilkRandom.Next(15) == 0)
            duration = 0.6d;
        return duration;
    }

    private float NilkIntensity
    {
        get
        {
            float completion = (float)Math.Clamp(nilkElapsedSeconds / NilkTotalDurationSeconds, 0d, 1d);
            if (completion >= 1f)
                return 0f;
            if (completion < 0.3f)
                return completion / 0.3f;
            if (completion <= 0.9f)
                return 1f;
            return 1f - (completion - 0.9f) / 0.1f;
        }
    }

    private void PostNilkShaderState()
    {
        if (!gpuRendererActive || gpuView?.CoreWebView2 is null || nilkPaletteIndex < 0)
            return;

        float[][]? palette = nilkPaletteIndex != lastPostedNilkPaletteIndex ? NilkPalettes[nilkPaletteIndex] : null;
        lastPostedNilkPaletteIndex = nilkPaletteIndex;
        string message = JsonSerializer.Serialize(new
        {
            type = "nilk-state",
            globalTime = (float)nilkElapsedSeconds,
            intensity = NilkIntensity,
            palette,
            running = (mode == EffectMode.Nilk || mode == EffectMode.NilkSeek) && !paused && nilkElapsedSeconds < NilkTotalDurationSeconds
        });
        gpuView.CoreWebView2.PostWebMessageAsJson(message);
    }

    private void PushGpuState()
    {
        if (!gpuRendererActive || gpuView?.CoreWebView2 is null)
            return;

        string intensityValue = targetIntensity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string pausedValue = paused ? "true" : "false";
        _ = gpuView.CoreWebView2.ExecuteScriptAsync($"window.setOverlayState && window.setOverlayState({intensityValue}, {pausedValue}, {(int)mode});");
        PostNilkShaderState();
    }

    private static string BuildGpuOverlayHtml()
    {
        return """
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<style>
html,body{margin:0;width:100%;height:100%;overflow:hidden;background:transparent;}
canvas{position:fixed;inset:0;width:100vw;height:100vh;display:block;background:transparent;}
#hud{display:none;position:fixed;left:12px;bottom:10px;font:12px Segoe UI,Arial,sans-serif;color:rgba(218,252,255,.82);background:rgba(4,3,17,.34);padding:5px 8px;border-radius:7px;pointer-events:none;user-select:none}
#capturePanel{position:fixed;z-index:5;left:50%;top:50%;transform:translate(-50%,-50%);width:min(460px,calc(100vw - 40px));box-sizing:border-box;padding:22px 24px;border:1px solid rgba(166,116,255,.66);border-radius:16px;background:rgba(10,7,24,.94);box-shadow:0 18px 70px rgba(0,0,0,.65),0 0 36px rgba(119,47,255,.24);color:#eee9ff;pointer-events:auto;}
#capturePanel h1{margin:0 0 9px;font-size:20px;font-weight:650;letter-spacing:.02em;color:#fff;}
#capturePanel p{margin:0 0 16px;font-size:13px;line-height:1.5;color:#c8bfdf;}
#capturePanel button{border:0;border-radius:9px;padding:10px 14px;background:linear-gradient(110deg,#8f42ff,#e13fcb);color:#fff;font:600 13px Segoe UI,Arial,sans-serif;cursor:pointer;}
#capturePanel button:disabled{opacity:.55;cursor:wait;}
#capturePanel small{display:block;margin-top:12px;font-size:11px;line-height:1.45;color:#aaa0c4;}
#captureStatus{min-height:18px;margin:12px 0 0!important;color:#d9c8ff!important;}
video{position:fixed;width:1px;height:1px;left:-10px;top:-10px;opacity:0;pointer-events:none;}
body.capture-active{cursor:none!important;}
body.capture-active *{cursor:none!important;}
</style>
</head>
<body>
<div id="capturePanel">
  <h1>Desktop distortion</h1>
  <p>Choose an entire monitor for the full-screen Nilk effect. The processed image keeps updating while you Alt+Tab.</p>
  <button id="startCapture" type="button">Choose monitor</button>
  <p id="captureStatus" aria-live="polite">Windows must explicitly allow screen capture.</p>
  <small>Press Ctrl+Alt+R to stop sharing. Capture permission ends when sharing stops.</small>
</div>
<video id="screenVideo" autoplay muted playsinline></video>
<canvas id="c"></canvas><div id="hud">Ctrl+Alt+1-7 | Ctrl+Alt+PageUp/PageDown: Nilk phases | Space: pause | Esc: exit</div>
<script>
(() => {
  const canvas = document.getElementById('c');
  const hud = document.getElementById('hud');
  const screenVideo = document.getElementById('screenVideo');
  const capturePanel = document.getElementById('capturePanel');
  const startCaptureButton = document.getElementById('startCapture');
  const captureStatus = document.getElementById('captureStatus');
  const gl = canvas.getContext('webgl', {alpha:true, premultipliedAlpha:false, antialias:false, depth:false, stencil:false, preserveDrawingBuffer:false, powerPreference:'high-performance'});
  if (!gl) { hud.textContent = 'WebGL unavailable'; return; }

  const vert = `
attribute vec2 a;
varying vec2 v;
void main(){v=a*.5+.5;gl_Position=vec4(a,0.0,1.0);}
`;
  const frag = `
precision highp float;
uniform vec2 r;
uniform float t;
uniform float intensity;
uniform float mode;
uniform sampler2D screenFrame;
uniform sampler2D previousScreenFrame;
uniform sampler2D nilkNoiseTexture;
uniform sampler2D nilkOverlayTexture;
uniform sampler2D nilkPaletteTexture;
uniform float captureActive;
uniform float historyValid;
uniform float nilkGlobalTime;
uniform float nilkIntensity;
uniform vec2 cursorUv;
uniform vec2 frameSize;
uniform float cursorVisible;
varying vec2 v;

float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453123);}
float noise(vec2 p){
  vec2 i=floor(p), f=fract(p);
  vec2 u=f*f*(3.0-2.0*f);
  return mix(mix(hash(i),hash(i+vec2(1.0,0.0)),u.x),mix(hash(i+vec2(0.0,1.0)),hash(i+vec2(1.0,1.0)),u.x),u.y);
}
float fbm(vec2 p){
  float a=.5, s=0.0;
  mat2 m=mat2(1.62,1.17,-1.17,1.62);
  for(int i=0;i<5;i++){s+=a*noise(p);p=m*p+vec2(.17,.31);a*=.52;}
  return s;
}
float ridge(float x,float c,float w){return 1.0-smoothstep(0.0,w,abs(x-c));}
float crackle(vec2 p,float c,float w){
  float a=fbm(p);
  float b=fbm(p*2.05+a*1.85);
  return ridge(b+a*.22,c,w);
}
vec2 rot(vec2 p,float a){
  float c=cos(a), s=sin(a);
  return mat2(c,-s,s,c)*p;
}
vec3 sat(vec3 c,float s){float l=dot(c,vec3(.299,.587,.114));return mix(vec3(l),c,s);}
vec3 spectrum(float x){
  vec3 c=.5+.5*cos(6.2831853*(x+vec3(.00,.34,.67)));
  return pow(c,vec3(.72));
}
vec3 paletteGrade(vec3 c,float id,float n){
  float l=dot(c,vec3(.299,.587,.114));
  float m=mod(id,7.0);
  vec3 shadow=vec3(.004,.006,.018), mid=vec3(.12,.04,.45), hi=vec3(.95,.12,.88);
  if(m<1.0){shadow=vec3(.004,.010,.025);mid=vec3(.06,.20,.12);hi=vec3(.55,1.00,.72);}
  else if(m<2.0){shadow=vec3(.006,.002,.025);mid=vec3(.35,.05,.72);hi=vec3(1.00,.13,.90);}
  else if(m<3.0){shadow=vec3(.012,.005,.010);mid=vec3(.48,.10,.035);hi=vec3(1.00,.70,.18);}
  else if(m<4.0){shadow=vec3(.002,.008,.028);mid=vec3(.03,.18,.55);hi=vec3(.18,.88,1.00);}
  else if(m<5.0){shadow=vec3(.015,.000,.018);mid=vec3(.55,.02,.14);hi=vec3(1.00,.18,.35);}
  else if(m<6.0){shadow=vec3(.000,.004,.020);mid=vec3(.05,.30,.38);hi=vec3(.08,1.00,.48);}
  else{shadow=vec3(.010,.006,.030);mid=vec3(.16,.07,.62);hi=vec3(.85,.82,1.00);}
  vec3 mapped=mix(shadow,mid,smoothstep(.025,.54,l));
  mapped=mix(mapped,hi,smoothstep(.46,1.0,l));
  float band=.5+.5*sin(n*6.2831853+id*.73+l*2.4);
  vec3 localA=spectrum(id*.17+n*.62+l*.18);
  vec3 localB=spectrum(id*.31-n*.38+l*.52+.27);
  vec3 multi=mix(localA,localB,smoothstep(.24,.86,band));
  mapped=mix(mapped,multi,(.18+.16*smoothstep(.12,.90,l)));
  return mix(mapped,c,.34);
}

float cross2(vec2 a,vec2 b){return a.x*b.y-a.y*b.x;}
float triangleMask(vec2 p,vec2 a,vec2 b,vec2 c){
  float ab=cross2(b-a,p-a), bc=cross2(c-b,p-b), ca=cross2(a-c,p-c);
  float positive=step(0.0,ab)*step(0.0,bc)*step(0.0,ca);
  float negative=step(ab,0.0)*step(bc,0.0)*step(ca,0.0);
  return max(positive,negative);
}
float cursorShape(vec2 p){
  float head=triangleMask(p,vec2(0.0,0.0),vec2(0.0,-22.0),vec2(15.0,-7.0));
  float stemA=triangleMask(p,vec2(4.0,-9.0),vec2(10.0,-14.0),vec2(17.0,-5.0));
  float stemB=triangleMask(p,vec2(4.0,-9.0),vec2(17.0,-5.0),vec2(11.0,1.0));
  return max(head,max(stemA,stemB));
}
vec3 nilkPalette(float value){
  float index=clamp(value*6.0,0.0,6.0);
  float startIndex=floor(index);
  vec3 a=texture2D(nilkPaletteTexture,vec2((startIndex+.5)/8.0,.5)).rgb;
  vec3 b=texture2D(nilkPaletteTexture,vec2((startIndex+1.5)/8.0,.5)).rgb;
  return mix(a,b,fract(value*6.0));
}

void main(){
  vec2 uv=gl_FragCoord.xy/r;
  vec2 p=(uv-.5)*vec2(r.x/r.y,1.0);
  if(captureActive>.5){
    float captureTime=t*.72;
    float aspect=r.x/max(r.y,1.0);
    float strength=clamp(intensity*1.18,0.0,.92);
    vec2 sampleUv=uv;
    vec3 screenColor=vec3(0.0);
    float colorBlend=0.0;
    float colorPhase=0.0;
    if(mode>5.5){
      float opacity=1.0;
      float smoothOpacity=opacity*opacity*(3.0-opacity*2.0);
      float effective=nilkIntensity*smoothOpacity;
      float globalTime=nilkGlobalTime;
      vec2 unmodifiedCoords=uv;
      vec2 coords=uv;
      float offsetTime=globalTime*.7;
      coords.x+=cos(offsetTime+coords.y*6.283)*effective*.05;
      coords.y+=cos(offsetTime+coords.x*6.283)*effective*.05;
      vec4 baseColor=texture2D(screenFrame,coords);
      coords.y+=(sin(coords.x*300.0-coords.y*32.0+globalTime*20.0)*.004+
                 sin(coords.x*20.0+coords.y*105.0+globalTime*10.0)*.003)*effective;

      vec4 distortedScreenColor=texture2D(screenFrame,coords);
      vec4 previousScreenColor=texture2D(previousScreenFrame,coords);
      float blendNoise=texture2D(nilkNoiseTexture,unmodifiedCoords*1.4+vec2(previousScreenColor.r)).r+
                       texture2D(nilkNoiseTexture,unmodifiedCoords*.9+vec2(previousScreenColor.b)).r;
      float blendInterpolant=smoothstep(1.0-.51,1.0,blendNoise*.5);
      vec4 color=mix(distortedScreenColor,previousScreenColor,blendInterpolant*pow(effective,2.5)*historyValid);
      float centerDistance=distance(coords,vec2(.5));
      float blurInterpolant=1.0-smoothstep(.05,.20,centerDistance);
      if(blurInterpolant>0.0){
        vec4 blurredColor=vec4(0.0);
        for(int i=-6;i<6;i++)
          blurredColor+=texture2D(screenFrame,coords+vec2(float(i),0.0)*effective*.001)/13.0;
        color=mix(color,blurredColor,blurInterpolant);
      }
      float luminosity=dot(color.rgb,vec3(.3,.6,.1));
      float paletteInterpolant=sin(luminosity*6.283-globalTime*1.5)*.5+.5;
      vec4 evilColor=vec4(nilkPalette(paletteInterpolant),1.0);
      evilColor-=distance(coords,vec2(.5))*.6;
      vec4 overlayColor=texture2D(nilkOverlayTexture,unmodifiedCoords);
      evilColor=mix(evilColor,overlayColor,overlayColor.a);
      screenColor=mix(baseColor,evilColor,effective).rgb;
      sampleUv=coords;
    }else{
      vec2 delta=vec2(0.0);
      float fieldA=fbm(p*1.8+vec2(captureTime*.08,-captureTime*.05));
      float fieldB=fbm(p*2.1+vec2(-captureTime*.06,captureTime*.07));
      if(mode<1.5){
        delta=vec2(sin(p.y*7.0+captureTime)*.014+(fieldA-.5)*.026,
                   cos(p.x*5.4-captureTime*.8)*.011+(fieldB-.5)*.020)*strength;
        colorBlend=.10*strength;
        colorPhase=fieldA+captureTime*.035;
      }else if(mode<2.5){
        delta=(vec2(fieldA,fieldB)-.5)*.075*strength;
        delta+=vec2(sin(p.y*12.0+captureTime),cos(p.x*10.0-captureTime))*.010*strength;
        colorBlend=.16*strength;
        colorPhase=fieldB+captureTime*.06;
      }else if(mode<3.5){
        float row=floor(uv.y*118.0);
        float slice=step(.84,hash(vec2(row,floor(captureTime*5.0))));
        float tear=(hash(vec2(row,floor(captureTime*8.0)+19.0))-.5)*.075*slice*strength;
        delta.x=tear+sin(uv.y*118.0+captureTime*1.8)*.0025*strength;
        delta.y=sin(uv.x*44.0+captureTime)*.002*strength;
        colorBlend=.055*strength;
        colorPhase=hash(vec2(row,floor(captureTime*2.0)))+captureTime*.03;
      }else if(mode<4.5){
        float radius=length(p)+.0001;
        float wave=sin(radius*25.0-captureTime*1.35+fieldA*2.0);
        delta=normalize(p)*wave*.032*strength;
        delta+=vec2(-p.y,p.x)*(.018*strength/(radius+.35));
        colorBlend=.14*strength;
        colorPhase=radius*.7+captureTime*.045;
      }else{
        vec2 lens=vec2(.28*sin(captureTime*.43),.22*cos(captureTime*.37));
        vec2 d=p-lens;
        float radius=length(d)+.025;
        float vortex=sin(radius*31.0-captureTime*2.0+fieldB*4.0);
        delta=(vec2(fieldA,fieldB)-.5)*.105*strength;
        delta+=vec2(-d.y,d.x)*(vortex*.045*strength/(radius+.22));
        delta-=d*(sin(radius*16.0-captureTime)*.018*strength/(radius+.30));
        colorBlend=.18*strength;
        colorPhase=fieldA+fieldB+captureTime*.10;
      }
      sampleUv=clamp(uv+vec2(delta.x/aspect,delta.y),vec2(.001),vec2(.999));
      float split=.0012*strength;
      if(mode>4.5)split=.0030*strength;
      if(mode>3.5&&mode<4.5)split=.0020*strength;
      screenColor.r=texture2D(screenFrame,clamp(sampleUv+vec2(split,0.0),vec2(.001),vec2(.999))).r;
      screenColor.g=texture2D(screenFrame,sampleUv).g;
      screenColor.b=texture2D(screenFrame,clamp(sampleUv-vec2(split,0.0),vec2(.001),vec2(.999))).b;
      if(mode>2.5&&mode<3.5){
        float scan=.93+.07*step(.5,fract(uv.y*236.0));
        screenColor*=scan;
      }
      vec3 tint=spectrum(colorPhase);
      screenColor=mix(screenColor,screenColor*.88+tint*.12,colorBlend);
    }
    if(cursorVisible>.5){
      vec2 cursorPoint=(sampleUv-cursorUv)*frameSize;
      if(cursorPoint.x>-2.0&&cursorPoint.x<19.0&&cursorPoint.y>-24.0&&cursorPoint.y<5.0){
        float cursorFill=cursorShape(cursorPoint);
        float cursorOutline=max(cursorFill,max(max(cursorShape(cursorPoint+vec2(1.2,0.0)),cursorShape(cursorPoint-vec2(1.2,0.0))),
                               max(cursorShape(cursorPoint+vec2(0.0,1.2)),cursorShape(cursorPoint-vec2(0.0,1.2)))));
        screenColor=mix(screenColor,vec3(.008,.006,.020),cursorOutline);
        screenColor=mix(screenColor,vec3(.96,.96,1.0),cursorFill);
      }
    }
    gl_FragColor=vec4(clamp(screenColor,0.0,1.0),1.0);
    return;
  }
  // Mode 6 is the monitor-capture Nilk shader only. Never fall through to the
  // generic Chaos overlay while its monitor capture has not started.
  if(mode>5.5){gl_FragColor=vec4(0.0);return;}
  float flowMode=1.0-step(1.5,mode);
  float textureMode=step(1.5,mode)*(1.0-step(2.5,mode));
  float glyphMode=step(2.5,mode)*(1.0-step(3.5,mode));
  float fullMode=step(3.5,mode)*(1.0-step(4.5,mode));
  float chaos=step(4.5,mode);
  float power=clamp(intensity*1.55*(1.0-.20*flowMode+.06*textureMode+.12*fullMode+.18*chaos),0.0,1.0);
  float chaosPower=chaos*power;
  float time=t*.72;

  vec2 q=p;
  q += vec2(fbm(p*1.15+vec2(time*.07,-time*.04)), fbm(p*1.10+vec2(-time*.05,time*.06)))*.72-.36;
  q += vec2(sin(p.y*7.0+time*.9), cos(p.x*6.0-time*.7))*.035*power*(1.0-.34*flowMode);
  q += vec2(sin(p.y*12.0+time*.44),cos(p.x*10.0-time*.38))*(.010*textureMode+.014*fullMode)*power;
  float pr=length(p)+.001;
  float pa=atan(p.y,p.x);
  vec2 panic=rot(p,time*(.10+.10*chaos));
  float thought=sin(panic.x*32.0+fbm(panic*3.3+time*.08)*8.0+time*2.7)*
                cos(panic.y*27.0-fbm(panic*4.1-time*.06)*7.0-time*2.1);
  float fieldA=fbm(p*3.4+vec2(time*.08,-time*.05));
  float fieldB=fbm(rot(p,time*.06)*4.1+vec2(-time*.06,time*.07));
  vec2 spaceFlow=normalize(vec2(fieldA-.5,fieldB-.5)+vec2(.001,-.001));
  vec2 liquidDrift=vec2(
    sin((p.y+fieldA*.42)*10.0+time*1.08+fieldB*4.0),
    cos((p.x-fieldB*.38)*9.0-time*.93+fieldA*3.6));
  float fold=sin((p.x*3.1+p.y*4.4)+fieldA*5.6-fieldB*3.1-time*1.45);
  q += spaceFlow*(.034+.018*fold)*chaosPower;
  q += liquidDrift*.022*chaosPower;
  q += vec2(sin(thought*2.2+time*1.2),cos(thought*2.0-time*1.0))*.010*chaosPower;

  vec2 h1=vec2(.52*sin(time*.17), .32*cos(time*.13));
  vec2 h2=vec2(.62*sin(time*.11+2.1), .38*cos(time*.16+1.4));
  vec2 h3=vec2(.40*sin(time*.21+4.2), .26*cos(time*.10+3.3));
  vec2 hx=vec2(.78*sin(time*.20+1.1), .34*sin(time*.13+2.0));
  vec2 dx=q-hx;
  float rx=length(dx)+.001;
  float bossBeat=pow(.5+.5*sin(time*.42),3.0);
  float waveA=.5+.5*sin(time*.31+1.7);
  float waveB=.5+.5*sin(time*.47+4.1);
  float noxusReach=smoothstep(1.05,.025,rx)*power;
  float noxusSwirl=(.070+.020*bossBeat)/(rx+.060)*noxusReach;
  float noxusPull=(.040+.018*bossBeat)/(rx+.105)*noxusReach;
  q += vec2(-dx.y,dx.x)*noxusSwirl - dx*noxusPull;
  q += dx/rx*(ridge(rx,.31+.12*waveA,.090)+ridge(rx,.56+.18*waveB,.120)*.62)*(.018+.025*bossBeat)*power;
  vec2 d1=q-h1, d2=q-h2, d3=q-h3;
  float r1=length(d1)+.001, r2=length(d2)+.001, r3=length(d3)+.001;
  float l1=smoothstep(.45,.035,r1)*power;
  float l2=smoothstep(.38,.030,r2)*power*.82;
  float l3=smoothstep(.32,.026,r3)*power*.62;
  q += vec2(-d1.y,d1.x)*l1*(.052/(r1+.040)) - d1*l1*(.060/(r1+.12));
  q += vec2(-d2.y,d2.x)*l2*(.045/(r2+.045)) - d2*l2*(.048/(r2+.12));
  q += vec2(-d3.y,d3.x)*l3*(.038/(r3+.050)) - d3*l3*(.040/(r3+.13));

  vec2 q2=q;
  q2 += vec2(fbm(q*2.0+time*.10), fbm(q*2.2-time*.09))*.34-.17;
  q2 += vec2(sin(q.y*15.0+time*1.1), cos(q.x*13.0-time*.9))*.018*power*(1.0-.22*flowMode+.20*chaos);
  float n1=fbm(q2*2.15+vec2(time*.035,-time*.025));
  float n2=fbm(q2*5.40+vec2(-time*.055,time*.045)+n1*1.7);
  float n3=fbm(q2*12.0+vec2(time*.12,-time*.10)+n2*2.1);

  float flow=sin((q2.y*8.0+q2.x*2.1+n1*3.5)-time*.82);
  float cross=sin((q2.x*7.8-q2.y*4.7+n2*4.2)+time*.64);
  float plasma=clamp(n1*.42+n2*.36+n3*.18+flow*.11+cross*.055,0.0,1.0);
  float marble=fbm(q2*1.55+vec2(-time*.025,time*.018)+n1*.85);
  float abyss=smoothstep(.18,.86,marble+n2*.22);
  float voidPool=1.0-smoothstep(.34,.74,marble+plasma*.18);
  float vein=crackle(q2*3.2+vec2(time*.020,-time*.015)+n2*.7,.56,.135);
  float veinFine=crackle(q2*8.6+vec2(-time*.050,time*.035)+n3*1.4,.52,.070);
  float silverEdge=clamp(vein*veinFine*1.55 + ridge(plasma+marble*.20,.68,.070)*.42,0.0,1.0);
  float blueVein=ridge(marble+flow*.075+n3*.18,.48,.115)*smoothstep(.22,.90,abyss+n1*.25);
  vec2 kp=rot(q2*.92,time*.055)*3.0+vec2(n1*.24,n2*.18);
  vec2 k=abs(vec2(sin(kp.x*6.2831853+time*.18),cos(kp.y*6.2831853-time*.16))*.5);
  float kaleido=ridge(length(k),.235,.125)*smoothstep(.20,.95,n2+n3*.35);
  float aurora=ridge(sin((q2.x*2.2+q2.y*5.8+n1*2.8)+time*.52)*.5+.5,.62,.24);
  float cellular=crackle(q2*5.15+vec2(sin(time*.17),cos(time*.13))*1.2+n3*.55,.50,.105);
  float ribbon=ridge(sin((q2.x*8.5-q2.y*3.2+n2*4.7)-time*.95)*.5+.5,.54,.16);
  float amber=clamp(smoothstep(.48,.92,flow+n1*.38)*ridge(plasma,.36,.26),0.0,1.0);
  float crimson=clamp(ridge(plasma+n2*.18,.80,.18)*smoothstep(.25,1.0,cross+n3*.50),0.0,1.0);
  float rainbowField=ridge(sin((q2.x*4.8+q2.y*3.9+n1*3.8)+time*.70)*.5+.5,.50,.30);
  float prism=ridge(plasma+n3*.28+sin(time*.31)*.08,.52,.24)*smoothstep(.18,1.0,abs(flow)+n2*.45);
  float textureLace=max(ridge(fract(marble*7.0+n2*.55-time*.026),.50,.075),
                        ridge(fract(plasma*6.0+n3*.35+time*.018),.50,.065))*textureMode;
  float polarAngle=atan(q.y,q.x);
  float polarRadius=length(q);
  float sector=abs(fract(polarAngle*8.0/6.2831853+.5)-.5);
  float radialFold=abs(fract(polarRadius*7.0-time*.045)-.5);
  float mandala=(max(ridge(sector,.015,.035),ridge(sector,.485,.035)*.72)*
                 (.28+.72*ridge(radialFold,.44,.065)))*fullMode;
  vec2 glyphUv=uv*vec2(58.0,32.0);
  glyphUv.x+=sin(glyphUv.y*.36+time*.58)*.16*glyphMode;
  vec2 glyphId=floor(glyphUv);
  vec2 glyphCell=fract(glyphUv)-.5;
  float glyphSeed=hash(glyphId+vec2(17.0,43.0));
  float glyphV=(1.0-smoothstep(.025,.075,abs(glyphCell.x-.13)))*
               (1.0-smoothstep(.22,.48,abs(glyphCell.y)))*step(.34,glyphSeed);
  float glyphH=(1.0-smoothstep(.025,.070,abs(glyphCell.y+.16)))*
               (1.0-smoothstep(.19,.45,abs(glyphCell.x)))*step(.55,hash(glyphId+vec2(31.0,7.0)));
  float glyphD=(1.0-smoothstep(.026,.070,abs(glyphCell.y-glyphCell.x*(glyphSeed>.5?1.0:-1.0))))*
               step(.70,hash(glyphId+vec2(5.0,29.0)));
  float glyphInk=max(max(glyphV,glyphH),glyphD)*glyphMode;
  float glyphScan=ridge(fract(uv.y*110.0+time*.12),.5,.028)*glyphMode;
  float deepPulse=.72+.28*sin(time*.95+n1*5.0+marble*3.2);
  float violet=ridge(plasma,.58,.32);
  float magenta=ridge(plasma,.72,.22)*smoothstep(-.15,.95,flow);
  float emerald=ridge(plasma,.42,.23)*smoothstep(-.75,.95,-flow+n2*.7);
  float pale=ridge(plasma,.86,.11)*.45;
  float sparks=smoothstep(.975,1.0,noise(q2*72.0+vec2(time*.9,-time*.7)));
  sparks += smoothstep(.985,1.0,noise(q2*128.0+vec2(-time*1.2,time*.8)))*blueVein*.55;
  vec2 sx=q2-hx;
  float sr=length(sx)+.001;
  float sa=atan(sx.y,sx.x);
  float diskNoise=noise(vec2(sa*1.35+time*.95, sr*7.0-time*.50))+
                  noise(vec2(sa*3.10-time*.55, sr*13.0+time*.20))*.5;
  float noxusLens=ridge(sr,.360+.055*bossBeat,.130)*(.45+.55*noise(vec2(sa*2.0+time*.42,sr*5.0)))*noxusReach;
  float noxusShock=(ridge(sr,.24+.18*waveA,.036)*(1.0-waveA*.42)+ridge(sr,.55+.18*waveB,.052)*(1.0-waveB*.35))*.62*power;
  float noxusDust=smoothstep(.982,1.0,noise(vec2(sa*9.0-sr*18.0+time*1.6, sr*32.0-time*.8)))*
                  smoothstep(.65,.060,sr)*noxusReach;
  float noxusTidal=ridge(sin(sa*2.0+sr*18.0-time*2.2+n2*2.0)*.5+.5,.52,.26)*smoothstep(.50,.05,sr)*noxusReach;
  vec2 shatter=rot(q2,time*.16);
  float chaosShard=ridge(sin((shatter.x*13.0-shatter.y*9.0+n3*4.7)+time*1.8)*.5+.5,.50,.210)*chaosPower;
  float chaosPulse=smoothstep(.88,1.0,fbm(q2*18.0+vec2(time*.55,-time*.48)+n1*2.0))*chaosPower;
  float liquidSurge=smoothstep(.42,.94,fbm(q2*2.55+vec2(time*.10,-time*.08)+n1*1.2))*smoothstep(.10,1.0,n2+n3*.25)*chaosPower;
  float meltFold=ridge(marble+flow*.10+n2*.18,.54,.240)*chaosPower;
  float lensBody=clamp((l1*.68+l2*.58+l3*.48+noxusReach*.42)*chaos,0.0,1.0);
  float lensRim=((1.0-smoothstep(.07,.34,r1))*l1*.42+
                 (1.0-smoothstep(.06,.29,r2))*l2*.36+
                 (1.0-smoothstep(.05,.25,r3))*l3*.30+
                 noxusLens*.42)*chaosPower;
  float shearWarp=smoothstep(.42,.90,fbm(q2*3.7+vec2(time*.12,-time*.10)+n2*1.4))*max(lensBody,liquidSurge*.45)*chaosPower;
  float thoughtWeb=smoothstep(.62,1.0,fbm(shatter*6.2+vec2(time*.16,-time*.14)+n1*1.6))*smoothstep(.20,.95,n2+n3*.4)*chaosPower;
  float meltEcho=ridge(fbm(q2*5.8+vec2(time*.20,-time*.17)+fieldB*1.6),.58,.240)*chaosPower;
  float chromaSlip=clamp((lensRim*.58+shearWarp*.58+meltEcho*.34+liquidSurge*.24)*chaos,0.0,1.0);

  vec3 base=mix(vec3(.006,.008,.026),vec3(.010,.028,.070),abyss*.64);
  vec3 col=base;
  col=mix(col,vec3(.012,.035,.120),blueVein*.52*deepPulse);
  col=mix(col,vec3(.020,.085,.190),smoothstep(.25,.95,blueVein+n2*.35)*.36);
  col=mix(col,vec3(.015,.010,.030),voidPool*.42);
  col=mix(col,vec3(.18,.05,.54),violet*.62);
  col=mix(col,vec3(.66,.06,.95),magenta*.78);
  col=mix(col,vec3(.05,.78,.28),emerald*.58);
  col=mix(col,vec3(.00,.86,1.00),aurora*.38);
  col=mix(col,vec3(.14,.16,.95),kaleido*.42);
  col=mix(col,vec3(.95,.82,.18),amber*.44);
  col=mix(col,vec3(.96,.06,.12),crimson*.46);
  col=mix(col,spectrum(plasma+n1*.37+time*.055),rainbowField*.48);
  col=mix(col,spectrum(n2+n3*.45-flow*.10+time*.075),prism*.38);
  col=mix(col,spectrum(marble*.31+n2*.42+time*.055),voidPool*.24*power);
  col=mix(col,vec3(.56,.02,.96),cellular*.28*smoothstep(.25,1.0,violet+magenta));
  col=mix(col,vec3(.02,.95,.55),cellular*.30*smoothstep(.20,1.0,emerald+blueVein));
  col+=vec3(.10,.62,1.00)*ribbon*.26;
  col=mix(col,vec3(.78,.36,.08),smoothstep(.45,1.0,flow+n1*.45)*.28);
  col=mix(col,vec3(.82,.90,1.0),pale);
  col=mix(col,vec3(.63,.76,.94),silverEdge*.42);
  col+=vec3(.06,.26,.72)*blueVein*.32;
  col+=vec3(.95,.12,.88)*sparks*.70;
  col+=vec3(.16,.95,.35)*sparks*smoothstep(.52,1.0,emerald+magenta)*.38;
  col+=vec3(.20,.45,1.0)*sparks*blueVein*.35;
  vec3 noxusHue=mix(vec3(.18,.42,1.0),vec3(1.0,.22,.06),.5+.5*sin(sa*2.0-time*1.3+diskNoise));
  col+=spectrum(sa*.12+time*.18+diskNoise*.21)*noxusLens*.46;
  col+=vec3(.85,.96,1.0)*noxusShock*.34;
  col+=vec3(.85,.16,1.0)*noxusDust*.85;
  col+=vec3(.12,.62,1.0)*noxusTidal*.40;
  col+=noxusHue*noxusLens*.18;
  col=mix(col,spectrum(glyphSeed*.43+n1*.18+time*.05),glyphInk*.72);
  col+=vec3(.02,.50,.72)*glyphScan*.18;
  col=mix(col,spectrum(marble*.42+n2*.28+time*.025),textureLace*.52);
  col=mix(col,spectrum(polarRadius*1.8+polarAngle*.19-time*.035),mandala*.58);
  col=mix(col,1.0-col,(chaosShard*.10+thoughtWeb*.06)*chaos);
  col=mix(col,col*.54+spectrum(time*.10+n1*.25+fieldA*.30)*.74,lensBody*.18*chaos);
  col+=spectrum(time*.19+n2*.35+sa*.12)*chaosShard*.36;
  col+=spectrum(fieldA*.31+n2*.22+time*.16)*liquidSurge*.46;
  col+=spectrum(fieldB*.27+marble*.18-time*.11)*meltFold*.24;
  col+=mix(vec3(.08,.34,1.0),vec3(1.0,.12,.62),.5+.5*sin(time*1.2+fieldA*5.0))*shearWarp*.32;
  col+=vec3(.05,.95,.72)*chaosPulse*.22;
  col+=vec3(.08,.58,1.0)*chromaSlip*.20 + vec3(1.0,.07,.55)*lensRim*.16;
  col+=mix(vec3(.10,.32,1.0),vec3(1.0,.18,.70),.5+.5*sin(time+fieldB*4.0))*thoughtWeb*.16;
  col+=spectrum(time*.07+meltEcho+n1*.2)*meltEcho*.18;

  float paletteClock=time*(.155+chaos*.070);
  float palettePhase=fract(paletteClock);
  float paletteId=floor(paletteClock);
  float paletteCut=smoothstep(.76,.86,palettePhase);
  float paletteNoise=fbm(q2*.85+vec2(paletteId*.21,time*.025));
  vec3 paletteA=paletteGrade(col,paletteId,paletteNoise);
  vec3 paletteB=paletteGrade(col,paletteId+1.0,paletteNoise);
  col=mix(paletteA,paletteB,paletteCut);
  float colorField=fbm(q2*1.35+vec2(time*.018+paletteId*.19,-time*.022-paletteId*.13));
  vec3 lsdA=spectrum(colorField*.55+plasma*.21+marble*.17+paletteId*.09+time*.030);
  vec3 lsdB=spectrum(colorField*.21-flow*.09+paletteId*.17+time*.018);
  float colorMask=smoothstep(.16,.92,plasma+n2*.18)*(.22+.18*rainbowField+.12*kaleido+chaos*(.10+.14*liquidSurge+.10*thoughtWeb+.12*lensBody+.08*meltFold));
  col=mix(col,mix(lsdA,lsdB,.36+.24*sin(colorField*6.2831853)),colorMask*power);
  col=mix(col,spectrum(colorField*.42+n3*.33+time*.11),chaos*(.14+.10*chaosShard+.14*chaosPulse+.16*liquidSurge+.10*thoughtWeb+.13*shearWarp+.08*meltFold)*power);
  col+=spectrum(paletteId*.21+paletteNoise+time*.035)*ridge(palettePhase,.82,.050)*.12*power;

  float l=dot(col,vec3(.299,.587,.114));
  col=sat(col,1.54+.18*magenta+.14*emerald+.12*blueVein+.11*kaleido+.09*aurora+.12*rainbowField+.10*prism+chaos*.20);
  col=(col-.070)*(1.22+chaos*.10)+.070;
  col=pow(max(col,0.0),vec3(.88-chaos*.03));

  float signal=clamp(violet*.34+magenta*.58+emerald*.38+pale*.28+sparks*.78+blueVein*.48+silverEdge*.38+
                      aurora*.38+kaleido*.42+cellular*.30+ribbon*.26+amber*.34+crimson*.36+
                      rainbowField*.42+prism*.34+noxusLens*.42+noxusShock*.36+noxusDust*.70+noxusTidal*.38+
                      chaosShard*.24+chaosPulse*.46+liquidSurge*.44+meltFold*.30+lensBody*.34+lensRim*.30+shearWarp*.40+thoughtWeb*.30+meltEcho*.26+
                      textureLace*.36+mandala*.42+glyphInk*.56+glyphScan*.20,0.0,1.0);
  float veil=smoothstep(.18,.95,plasma+n1*.22)*.09 + smoothstep(.25,.90,marble+n2*.25)*.070;
  float vign=1.0-smoothstep(.58,1.20,length(p));
  float alpha=(veil+signal*.60)*power*(.70+.30*vign)*(1.0-.12*flowMode);
  alpha=clamp(alpha+(noxusLens*.16+noxusShock*.15+noxusDust*.22+
              chaosShard*.05+chaosPulse*.12+liquidSurge*.12+meltFold*.08+lensBody*.12+lensRim*.07+shearWarp*.11+thoughtWeb*.08+meltEcho*.07+
              textureLace*.10+mandala*.12+glyphInk*.20+glyphScan*.06)*power,0.0,.88);
  gl_FragColor=vec4(col,alpha);
}
`;

  function shader(type, src){
    const s=gl.createShader(type); gl.shaderSource(s,src); gl.compileShader(s);
    if(!gl.getShaderParameter(s,gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(s));
    return s;
  }
  const program=gl.createProgram();
  gl.attachShader(program,shader(gl.VERTEX_SHADER,vert));
  gl.attachShader(program,shader(gl.FRAGMENT_SHADER,frag));
  gl.linkProgram(program);
  if(!gl.getProgramParameter(program,gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program));
  gl.useProgram(program);
  const buffer=gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER,buffer);
  gl.bufferData(gl.ARRAY_BUFFER,new Float32Array([-1,-1,3,-1,-1,3]),gl.STATIC_DRAW);
  const attr=gl.getAttribLocation(program,'a');
  gl.enableVertexAttribArray(attr);
  gl.vertexAttribPointer(attr,2,gl.FLOAT,false,0,0);
  const ur=gl.getUniformLocation(program,'r'), ut=gl.getUniformLocation(program,'t'), ui=gl.getUniformLocation(program,'intensity'), um=gl.getUniformLocation(program,'mode');
  const uNilkClock=gl.getUniformLocation(program,'nilkGlobalTime');
  const uNilkIntensity=gl.getUniformLocation(program,'nilkIntensity');
  const us=gl.getUniformLocation(program,'screenFrame'), up=gl.getUniformLocation(program,'previousScreenFrame');
  const uNilkNoise=gl.getUniformLocation(program,'nilkNoiseTexture');
  const uNilkPalette=gl.getUniformLocation(program,'nilkPaletteTexture');
  const uNilkOverlay=gl.getUniformLocation(program,'nilkOverlayTexture');
  const uc=gl.getUniformLocation(program,'captureActive'), uh=gl.getUniformLocation(program,'historyValid');
  const uCursor=gl.getUniformLocation(program,'cursorUv'), uFrameSize=gl.getUniformLocation(program,'frameSize');
  const uCursorVisible=gl.getUniformLocation(program,'cursorVisible');
  const frameTextureSizes=new WeakMap();
  function createFrameTexture(){
    const texture=gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D,texture);
    gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);
    gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,1,1,0,gl.RGBA,gl.UNSIGNED_BYTE,new Uint8Array([0,0,0,255]));
    frameTextureSizes.set(texture,[1,1]);
    return texture;
  }
  gl.activeTexture(gl.TEXTURE0);
  let screenTexture=createFrameTexture();
  gl.activeTexture(gl.TEXTURE1);
  let previousScreenTexture=createFrameTexture();
  gl.uniform1i(us,0); gl.uniform1i(up,1);
  gl.activeTexture(gl.TEXTURE2);
  const nilkNoiseTexture=gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D,nilkNoiseTexture);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.REPEAT);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.REPEAT);
  gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,1,1,0,gl.RGBA,gl.UNSIGNED_BYTE,new Uint8Array([128,128,128,255]));
  gl.uniform1i(uNilkNoise,2);
  const nilkNoiseImage=new Image();
  nilkNoiseImage.onload=()=>{
    gl.activeTexture(gl.TEXTURE2);
    gl.bindTexture(gl.TEXTURE_2D,nilkNoiseTexture);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,true);
    gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,nilkNoiseImage);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,false);
    gl.activeTexture(gl.TEXTURE0);
  };
  nilkNoiseImage.src='nilk-noise.png';
  gl.activeTexture(gl.TEXTURE0);
  gl.activeTexture(gl.TEXTURE3);
  const nilkPaletteTexture=gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D,nilkPaletteTexture);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.NEAREST);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.NEAREST);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);
  gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,8,1,0,gl.RGBA,gl.UNSIGNED_BYTE,new Uint8Array(32));
  gl.uniform1i(uNilkPalette,3);
  gl.activeTexture(gl.TEXTURE4);
  const nilkOverlayTexture=gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D,nilkOverlayTexture);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);
  gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,1,1,0,gl.RGBA,gl.UNSIGNED_BYTE,new Uint8Array([0,0,0,0]));
  gl.uniform1i(uNilkOverlay,4);
  gl.activeTexture(gl.TEXTURE0);
  let currentNilkPaletteKey='';
  function setNilkPalette(colors){
    const key=JSON.stringify(colors);
    if(key===currentNilkPaletteKey)return;
    currentNilkPaletteKey=key;
    const bytes=new Uint8Array(32);
    for(let i=0;i<8;i++){
      const color=colors[i];
      bytes[i*4]=Math.round(color[0]*255);
      bytes[i*4+1]=Math.round(color[1]*255);
      bytes[i*4+2]=Math.round(color[2]*255);
      bytes[i*4+3]=255;
    }
    gl.activeTexture(gl.TEXTURE3);
    gl.bindTexture(gl.TEXTURE_2D,nilkPaletteTexture);
    gl.texSubImage2D(gl.TEXTURE_2D,0,0,0,8,1,gl.RGBA,gl.UNSIGNED_BYTE,bytes);
    gl.activeTexture(gl.TEXTURE0);
  }
  let activeStream=null, screenCaptureActive=false, hasHistory=false, frameCount=0, lastVideoTime=-1;
  let nilkGlobalTime=0, nilkIntensity=0, nilkStateRunning=false, nilkStateReceivedAt=0;
  let cursorU=.5, cursorV=.5, cursorIsVisible=0, nativeCursorMode='never', nativeCursorMotionUntil=0;
  function reportCaptureSize(){
    if(!activeStream)return;
    const track=activeStream.getVideoTracks()[0];
    const surface=track&&track.getSettings().displaySurface||'unknown';
    notifyHost(`capture-size:${screenVideo.videoWidth||0},${screenVideo.videoHeight||0},${surface}`);
  }
  screenVideo.addEventListener('resize',reportCaptureSize);
  window.chrome.webview.addEventListener('message',event=>{
    const message=event.data;
    if(message&&message.type==='cursor'){
      cursorU=Number(message.u)||0;
      cursorV=Number(message.v)||0;
      cursorIsVisible=message.visible?1:0;
      if(nativeCursorMode==='motion')nativeCursorMotionUntil=performance.now()+140;
    }else if(message&&message.type==='nilk-state'){
      nilkGlobalTime=Number(message.globalTime)||0;
      nilkIntensity=Number(message.intensity)||0;
      nilkStateRunning=!!message.running;
      nilkStateReceivedAt=performance.now();
      if(message.palette)setNilkPalette(message.palette);
    }
  });
  function notifyHost(message){if(window.chrome&&window.chrome.webview)window.chrome.webview.postMessage(message);}
function showCapturePanel(message){capturePanel.style.display='block';captureStatus.textContent=message;hud.style.display='none';}
  function stopCapture(message="Screen capture stopped."){
    const oldStream=activeStream;
    activeStream=null;
    screenCaptureActive=false;
    nativeCursorMode='never';
    nativeCursorMotionUntil=0;
    hasHistory=false;
    frameCount=0;
    lastVideoTime=-1;
    cursorIsVisible=0;
    document.body.classList.remove('capture-active');
    screenVideo.pause();
    screenVideo.srcObject=null;
    if(oldStream)oldStream.getTracks().forEach(track=>track.stop());
    showCapturePanel(message);
    notifyHost('capture-stopped');
  }
  async function startCapture(){
    if(!navigator.mediaDevices||!navigator.mediaDevices.getDisplayMedia){showCapturePanel("This WebView does not support screen capture.");notifyHost("capture-error");return;}
    startCaptureButton.disabled=true;
    captureStatus.textContent="Choose an entire monitor. Window capture is not supported for the full-screen Nilk effect.";
    try{
      const pending=navigator.mediaDevices.getDisplayMedia({video:{displaySurface:'monitor',cursor:'never',frameRate:{ideal:60,max:60}},audio:false});
      const stream=await pending;
      const track=stream.getVideoTracks()[0];
      if(!track)throw new Error("No screen video track was returned.");
      const selectedSurface=track.getSettings().displaySurface||'unknown';
      if(selectedSurface!=='monitor'&&selectedSurface!=='unknown'){
        stream.getTracks().forEach(item=>item.stop());
        throw new Error("Choose an entire monitor in the Windows sharing picker, not a window or browser tab.");
      }
      activeStream=stream;
      track.addEventListener("ended",()=>{if(activeStream===stream)stopCapture("Screen sharing ended.");},{once:true});
      try{await track.applyConstraints({cursor:{exact:'never'}});}catch(_){}
      const cursorSetting=track.getSettings().cursor;
      nativeCursorMode=cursorSetting==='always'||cursorSetting==='motion'?cursorSetting:'never';
      screenVideo.srcObject=stream;
      await screenVideo.play();
      screenCaptureActive=true;
      hasHistory=false;
      frameCount=0;
      lastVideoTime=-1;
      document.body.classList.add('capture-active');
      capturePanel.style.display='none';
      hud.style.display='none';
      notifyHost('capture-started');
      reportCaptureSize();
    }catch(error){
      const canceled=error&&error.name==="NotAllowedError";
      if(activeStream){const failed=activeStream;activeStream=null;failed.getTracks().forEach(track=>track.stop());}
      showCapturePanel(canceled?"Screen capture canceled. Press the button to try again.":(error&&error.message)||"Could not capture the screen. Choose a source again.");
      notifyHost(canceled?'capture-cancelled':'capture-error');
    }finally{startCaptureButton.disabled=false;}
  }
  startCaptureButton.addEventListener('click',startCapture);
  window.rejectNonMonitorCapture=()=>stopCapture("Choose an entire monitor in the Windows sharing picker. Window capture is disabled for this full-screen effect.");
  window.toggleScreenCapture=()=>{if(activeStream)stopCapture();else showCapturePanel("Choose an entire monitor for full-screen distortion.");};
  window.addEventListener('pagehide',()=>{if(activeStream)activeStream.getTracks().forEach(track=>track.stop());});

  let targetIntensity=.52, shownIntensity=.52, currentMode=4, paused=false, shaderTime=0, last=performance.now();
  const modeNames=['FLOW','TEXTURE','GLITCH','FULL TRIP','CHAOS','NILK CYCLE','NILK SEEK'];
  window.setOverlayState=(i,p,m)=>{
    targetIntensity=Math.max(0,Math.min(1,Number(i)||0));
    paused=!!p;
    currentMode=Math.max(1,Math.min(7,Number(m)||4));
    const label=modeNames[currentMode-1];
    hud.textContent=`Ctrl+Alt+1-7 | Ctrl+Alt+PageUp/PageDown: Nilk phases | Space | Esc | ${currentMode} ${label} | INT ${Math.round(targetIntensity*100)}%${paused?" PAUSED":""}`;
  };
  function resize(){
    let dpr=Math.min(devicePixelRatio||1,1.35);
    if(screenCaptureActive&&screenVideo.videoWidth&&screenVideo.videoHeight){
      dpr=Math.min(dpr,screenVideo.videoWidth/Math.max(1,innerWidth),screenVideo.videoHeight/Math.max(1,innerHeight));
    }
    const pixelCount=innerWidth*innerHeight*dpr*dpr;
    dpr*=Math.min(1,Math.sqrt(2500000/Math.max(1,pixelCount)));
    const w=Math.max(1,Math.floor(innerWidth*dpr)), h=Math.max(1,Math.floor(innerHeight*dpr));
    if(canvas.width!==w||canvas.height!==h){canvas.width=w;canvas.height=h;gl.viewport(0,0,w,h);}
  }
  addEventListener('resize',resize,{passive:true});
  function frame(now){
    resize();
    const dt=Math.min((now-last)*.001,.033); last=now;
    if(!paused) shaderTime+=dt;
    shownIntensity += (targetIntensity-shownIntensity)*(1.0-Math.pow(.001,dt));
    gl.clearColor(0,0,0,0); gl.clear(gl.COLOR_BUFFER_BIT);
    gl.uniform2f(ur,canvas.width,canvas.height);
    gl.uniform1f(ut,shaderTime);
    gl.uniform1f(ui,shownIntensity);
    gl.uniform1f(um,currentMode);
    const nilkRenderTime=nilkGlobalTime+(nilkStateRunning?(performance.now()-nilkStateReceivedAt)*.001:0);
    gl.uniform1f(uNilkClock,nilkRenderTime);
    gl.uniform1f(uNilkIntensity,nilkIntensity);
    gl.uniform1f(uc,screenCaptureActive?1:0);
    gl.uniform1f(uh,hasHistory?1:0);
    gl.uniform2f(uCursor,cursorU,cursorV);
    gl.uniform1f(uCursorVisible,cursorIsVisible&&!(nativeCursorMode==='always'||(nativeCursorMode==='motion'&&performance.now()<nativeCursorMotionUntil))?1:0);
    if(screenCaptureActive&&screenVideo.readyState>=HTMLMediaElement.HAVE_CURRENT_DATA){
      gl.activeTexture(gl.TEXTURE0);
      gl.bindTexture(gl.TEXTURE_2D,screenTexture);
      if(screenVideo.currentTime!==lastVideoTime){
        const oldScreenTexture=screenTexture;
        screenTexture=previousScreenTexture;
        previousScreenTexture=oldScreenTexture;
        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D,screenTexture);
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,true);
        try{
          const width=screenVideo.videoWidth,height=screenVideo.videoHeight;
          const allocated=frameTextureSizes.get(screenTexture);
          if(!allocated||allocated[0]!==width||allocated[1]!==height){
            gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,screenVideo);
            frameTextureSizes.set(screenTexture,[width,height]);
          }else gl.texSubImage2D(gl.TEXTURE_2D,0,0,0,gl.RGBA,gl.UNSIGNED_BYTE,screenVideo);
        }
        catch(_){stopCapture("Could not upload the screen frame to WebGL.");}
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,false);
        hasHistory=frameCount>0;
        frameCount++;
        lastVideoTime=screenVideo.currentTime;
      }
      gl.activeTexture(gl.TEXTURE0);
      gl.bindTexture(gl.TEXTURE_2D,screenTexture);
      gl.activeTexture(gl.TEXTURE1);
      gl.bindTexture(gl.TEXTURE_2D,previousScreenTexture);
      gl.activeTexture(gl.TEXTURE0);
      gl.uniform2f(uFrameSize,screenVideo.videoWidth||1,screenVideo.videoHeight||1);
    }
    gl.drawArrays(gl.TRIANGLES,0,3);
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
})();
</script>
</body>
</html>
""";
    }

    private void RenderFullTripLowRes(Graphics target, int w, int h, float p)
    {
        int lowW = Math.Clamp(w / 3, 640, 920);
        int lowH = Math.Max(315, (int)MathF.Round(lowW * h / Math.Max(w, 1f)));
        EnsureTripSurface(lowW, lowH);

        RenderNilkShaderPixels(lowW, lowH, p);

        InterpolationMode oldInterpolation = target.InterpolationMode;
        PixelOffsetMode oldPixelOffset = target.PixelOffsetMode;
        target.InterpolationMode = InterpolationMode.Bilinear;
        target.PixelOffsetMode = PixelOffsetMode.Half;
        target.DrawImage(tripBitmap!, new Rectangle(0, 0, w, h), 0, 0, lowW, lowH, GraphicsUnit.Pixel);
        target.InterpolationMode = oldInterpolation;
        target.PixelOffsetMode = oldPixelOffset;
    }

    private void EnsureTripSurface(int width, int height)
    {
        int requiredBytes = width * height * 4;
        if (tripBitmap is not null && tripGraphics is not null && tripWidth == width && tripHeight == height &&
            tripPixelBuffer?.Length == requiredBytes && previousTripPixelBuffer?.Length == requiredBytes)
            return;

        tripGraphics?.Dispose();
        tripBitmap?.Dispose();
        previousTripGraphics?.Dispose();
        previousTripBitmap?.Dispose();

        tripWidth = width;
        tripHeight = height;
        tripBitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        tripGraphics = Graphics.FromImage(tripBitmap);
        ConfigureGraphics(tripGraphics);
        previousTripBitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        previousTripGraphics = Graphics.FromImage(previousTripBitmap);
        ConfigureGraphics(previousTripGraphics);
        tripPixelBuffer = new byte[requiredBytes];
        previousTripPixelBuffer = new byte[requiredBytes];
    }

    private void RenderNilkShaderPixels(int w, int h, float p)
    {
        if (tripBitmap is null || tripPixelBuffer is null || previousTripPixelBuffer is null)
            return;

        BitmapData targetData = tripBitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try
        {
            int targetBytes = Math.Abs(targetData.Stride) * h;
            if (tripPixelBuffer.Length != targetBytes || previousTripPixelBuffer.Length != targetBytes)
                return;

            byte[] target = tripPixelBuffer;
            byte[] previous = previousTripPixelBuffer;
            int stride = targetData.Stride;
            float opacity = Math.Clamp(p, 0f, 1f);
            float smoothOpacity = opacity * opacity * (3f - opacity * 2f);
            float effective = Math.Clamp(opacity * (0.35f + smoothOpacity * 0.95f), 0f, 1f);
            float overlayPower = Math.Clamp(opacity * 1.45f, 0f, 1f);
            float textureMode = mode == EffectMode.TextureTrip ? 1f : 0f;
            float glyphMode = mode == EffectMode.GlyphGlitch ? 1f : 0f;
            float fullMode = mode == EffectMode.FullTrip ? 1f : 0f;
            float chaosMode = mode == EffectMode.Chaos ? 1f : 0f;
            float progress = Frac(0.985f + time / ModePaletteCycleSeconds);
            float minute = progress * 60f;
            float cyanPhase = Saturate(
                Bell(minute, 8.7f, 2.35f) +
                Bell(minute, 15.4f, 2.85f) * 0.95f +
                Bell(minute, 56.8f, 2.80f) * 0.90f);
            float palePhase = Saturate(
                Bell(minute, 16.4f, 2.20f) * 0.18f +
                Bell(minute, 57.6f, 1.70f) * 0.14f);
            float voidPhase = Saturate(TimeWindow(minute, 20.0f, 43.5f, 3.2f));
            float redPhase = Saturate(
                BellWrapped(minute, 59.25f, 60f, 1.85f) +
                TimeWindow(minute, 58.2f, 60.0f, 1.0f) * 0.55f);

            float centerX = 0.53f + MathF.Sin(time * 0.051f) * 0.024f * overlayPower;
            float centerY = 0.50f + MathF.Cos(time * 0.047f) * 0.020f * overlayPower;
            float invW = 1f / Math.Max(w - 1, 1);
            float invH = 1f / Math.Max(h - 1, 1);
            float aspect = h / (float)Math.Max(w, 1);
            float baseAngle = -0.18f + MathF.Sin(time * 0.055f) * 0.11f + cyanPhase * 0.10f - redPhase * 0.08f;
            float baseCos = MathF.Cos(baseAngle);
            float baseSin = MathF.Sin(baseAngle);
            float lowTime = time * 0.18f;
            float midTime = time * 0.34f;
            float fastTime = time * 0.58f;
            byte[]? noise = noisePixels;
            int nw = noiseWidth;
            int nh = noiseHeight;
            int ns = noiseStride;

            for (int y = 0; y < h; y++)
            {
                float v = y * invH;
                int row = stride > 0 ? y * stride : (h - 1 - y) * -stride;
                for (int x = 0; x < w; x++)
                {
                    float u = x * invW;

                    float px = u - centerX;
                    float py = (v - centerY) * aspect;
                    float dist = MathF.Sqrt(px * px + py * py);
                    float twist = (0.12f + cyanPhase * 0.07f + redPhase * 0.09f) * effective / MathF.Max(0.17f, dist + 0.11f);
                    twist += MathF.Sin(dist * 13.5f - fastTime) * 0.050f * effective;
                    float twistCos = MathF.Cos(twist);
                    float twistSin = MathF.Sin(twist);
                    float rx = px * twistCos - py * twistSin;
                    float ry = px * twistSin + py * twistCos;
                    float sx0 = rx * baseCos - ry * baseSin;
                    float sy0 = rx * baseSin + ry * baseCos;

                    float ribbon = MathF.Sin((sx0 * 1.75f + sy0 * 2.25f) * MathF.Tau + lowTime) * 0.052f * effective;
                    ribbon += MathF.Sin((sx0 * 4.4f - sy0 * 5.8f) * MathF.Tau - midTime) * 0.026f * effective;
                    float swirlU = 0.5f + sx0 + ribbon + MathF.Sin(sy0 * 9.5f + lowTime) * 0.018f * effective;
                    float swirlV = 0.5f + sy0 / Math.Max(aspect, 0.35f) - ribbon + MathF.Cos(sx0 * 8.5f - midTime) * 0.018f * effective;

                    float texA = SampleNoise(noise, nw, nh, ns, swirlU * 0.72f + time * 0.004f, swirlV * 0.72f - time * 0.003f);
                    float texB = SampleNoise(noise, nw, nh, ns, swirlU * 1.46f - time * 0.008f + texA * 0.18f, swirlV * 1.25f + time * 0.006f);
                    float texC = SampleNoise(noise, nw, nh, ns, swirlU * 3.25f + texB * 0.30f, swirlV * 2.75f - time * 0.015f);
                    float wave = MathF.Sin((swirlU * 1.18f + swirlV * 0.92f + texB * 1.7f) * MathF.Tau + lowTime);
                    float ripple = MathF.Sin(dist * 24f - time * 0.72f + texC * 3.1f);
                    float flowBand = MathF.Sin((swirlV * 7.2f + swirlU * 1.65f) * MathF.Tau + texB * 5.4f - time * 0.38f);
                    float tearBand = MathF.Sin((swirlV * 18.5f - swirlU * 4.8f) + texC * 4.0f + time * 0.72f);
                    float field = Math.Clamp(texA * 0.40f + texB * 0.31f + texC * 0.16f + wave * 0.080f + ripple * 0.040f +
                                             flowBand * 0.070f + tearBand * 0.030f, 0f, 1f);
                    float edgeCenter = 0.47f + MathF.Sin(time * 0.19f + texB * 2.0f) * 0.085f;
                    float contour = 1f - SmoothStep(0.020f, 0.130f, MathF.Abs(field - edgeCenter));
                    float textureLace = MathF.Max(
                        1f - SmoothStep(0.025f, 0.090f, MathF.Abs(Frac(field * 7f + texB * 0.55f - time * 0.026f) - 0.5f)),
                        1f - SmoothStep(0.025f, 0.085f, MathF.Abs(Frac(texA * 6f + texC * 0.30f + time * 0.018f) - 0.5f))) * textureMode;

                    float polarAngle = MathF.Atan2(py, px);
                    float sector = MathF.Abs(Frac(polarAngle * 8f / MathF.Tau + 0.5f) - 0.5f);
                    float radialFold = MathF.Abs(Frac(dist * 7f - time * 0.045f) - 0.5f);
                    float mandala = MathF.Max(
                        1f - SmoothStep(0.012f, 0.040f, sector),
                        (1f - SmoothStep(0.012f, 0.040f, 0.5f - sector)) * 0.72f) *
                        (0.28f + 0.72f * (1f - SmoothStep(0.012f, 0.075f, MathF.Abs(radialFold - 0.44f)))) * fullMode;

                    float glyphU = Frac(u * 54f + MathF.Sin(v * 19f + time * 0.58f) * 0.035f) - 0.5f;
                    float glyphV = Frac(v * 29f) - 0.5f;
                    float glyphSeed = ValueNoise(MathF.Floor(u * 54f), MathF.Floor(v * 29f));
                    float glyphVertical = (1f - SmoothStep(0.025f, 0.075f, MathF.Abs(glyphU - 0.13f))) *
                                          (1f - SmoothStep(0.22f, 0.48f, MathF.Abs(glyphV))) *
                                          (1f - SmoothStep(0.34f, 0.58f, glyphSeed));
                    float glyphHorizontal = (1f - SmoothStep(0.025f, 0.070f, MathF.Abs(glyphV + 0.16f))) *
                                            (1f - SmoothStep(0.19f, 0.45f, MathF.Abs(glyphU))) *
                                            SmoothStep(0.48f, 0.70f, glyphSeed);
                    float glyphDiagonal = (1f - SmoothStep(0.026f, 0.070f, MathF.Abs(glyphV - glyphU * (glyphSeed > 0.5f ? 1f : -1f)))) *
                                          SmoothStep(0.70f, 0.88f, glyphSeed);
                    float glyphInk = MathF.Max(MathF.Max(glyphVertical, glyphHorizontal), glyphDiagonal) * glyphMode;
                    float glyphScan = (1f - SmoothStep(0.02f, 0.06f, MathF.Abs(Frac(v * 110f + time * 0.12f) - 0.5f))) * glyphMode;

                    float greenBand = SmoothStep(0.28f, 0.48f, field) * (1f - SmoothStep(0.62f, 0.86f, field));
                    float violetBand = SmoothStep(0.46f, 0.69f, field);
                    float deepBand = 1f - SmoothStep(0.20f, 0.50f, field);
                    float hotBand = SmoothStep(0.72f, 0.96f, field);

                    float r = 10f + deepBand * 8f + greenBand * 18f + violetBand * 132f + hotBand * 54f + contour * 56f;
                    float g = 12f + deepBand * 10f + greenBand * 122f + violetBand * 18f + hotBand * 32f + contour * 34f;
                    float b = 28f + deepBand * 72f + greenBand * 42f + violetBand * 122f + hotBand * 50f + contour * 54f;

                    float cyanMix = cyanPhase * (0.55f + field * 0.42f);
                    r = Lerp(r, 115f + field * 120f, cyanMix);
                    g = Lerp(g, 186f + field * 58f, cyanMix);
                    b = Lerp(b, 202f + field * 50f, cyanMix);

                    float paleMix = palePhase * SmoothStep(0.74f, 0.98f, field) * (0.22f + contour * 0.18f);
                    r = Lerp(r, 232f, paleMix);
                    g = Lerp(g, 240f, paleMix);
                    b = Lerp(b, 246f, paleMix);

                    float redMix = redPhase * (0.86f + contour * 0.34f);
                    float toxicMix = SmoothStep(0.30f, 0.76f, texC + contour * 0.26f);
                    float rr = Lerp(7f + field * 190f, 245f, contour * 0.42f);
                    float rg = Lerp(3f + field * 86f, 245f, toxicMix * contour);
                    float rb = Lerp(0f + field * 22f, 218f, contour * 0.88f);
                    r = Lerp(r, rr, redMix);
                    g = Lerp(g, rg, redMix);
                    b = Lerp(b, rb, redMix);

                    float broadBand = redPhase * SmoothStep(-0.35f, 0.76f, flowBand + field * 0.58f + texA * 0.22f);
                    float molten = SmoothStep(0.22f, 0.84f, field + texB * 0.24f);
                    r = Lerp(r, 102f + molten * 128f, broadBand * 0.42f);
                    g = Lerp(g, 35f + molten * 64f, broadBand * 0.32f);
                    b = Lerp(b, 12f + violetBand * 172f + contour * 45f, broadBand * 0.38f);

                    float violetCore = SmoothStep(0.42f, 0.86f, violetBand + texC * 0.34f + MathF.Max(flowBand, 0f) * 0.18f) *
                                       (0.48f + redPhase * 0.42f + cyanPhase * 0.16f);
                    float emeraldSheen = SmoothStep(0.46f, 0.90f, greenBand + texB * 0.36f + MathF.Max(-flowBand, 0f) * 0.16f) *
                                         (0.34f + voidPhase * 0.22f + redPhase * 0.18f);
                    r = Lerp(r, 92f + contour * 145f, violetCore * 0.46f);
                    g = Lerp(g, 16f + contour * 28f, violetCore * 0.34f);
                    b = Lerp(b, 205f + contour * 44f, violetCore * 0.58f);
                    r = Lerp(r, 24f + contour * 72f, emeraldSheen * 0.38f);
                    g = Lerp(g, 118f + contour * 118f, emeraldSheen * 0.62f);
                    b = Lerp(b, 58f + contour * 92f, emeraldSheen * 0.36f);

                    float cellSeed = SampleNoise(noise, nw, nh, ns, swirlU * 5.2f + texA * 0.35f, swirlV * 4.1f - texB * 0.25f);
                    float cell = SmoothStep(0.58f, 0.76f, cellSeed + field * 0.26f);
                    float cellRim = (1f - SmoothStep(0.76f, 0.96f, cellSeed + field * 0.20f)) * cell * redPhase;
                    float greenCore = SmoothStep(0.78f, 0.98f, cellSeed + texC * 0.24f) * redPhase;
                    r = Lerp(r, 230f, cellRim * 0.72f);
                    g = Lerp(g, 12f, cellRim * 0.42f);
                    b = Lerp(b, 218f, cellRim * 0.86f);
                    r = Lerp(r, 20f, greenCore * 0.86f);
                    g = Lerp(g, 238f, greenCore);
                    b = Lerp(b, 42f, greenCore * 0.88f);

                    float voidMix = voidPhase * (0.22f + deepBand * 0.22f);
                    r = Lerp(r, 8f + violetBand * 30f, voidMix);
                    g = Lerp(g, 6f + greenBand * 30f, voidMix);
                    b = Lerp(b, 24f + violetBand * 44f, voidMix);

                    float darken = Math.Clamp(dist * (0.26f + redPhase * 0.24f + voidPhase * 0.10f), 0f, 0.48f);
                    r *= 1f - darken;
                    g *= 1f - darken;
                    b *= 1f - darken;

                    float luma = r * 0.299f + g * 0.587f + b * 0.114f;
                    float saturation = 1.58f + contour * 0.35f + violetCore * 0.18f + emeraldSheen * 0.12f;
                    r = luma + (r - luma) * saturation;
                    g = luma + (g - luma) * saturation;
                    b = luma + (b - luma) * saturation;
                    r = (r - 96f) * 1.20f + 96f;
                    g = (g - 96f) * 1.20f + 96f;
                    b = (b - 96f) * 1.20f + 96f;

                    int index = row + x * 4;
                    int sx = Math.Clamp((int)((u + (texB - 0.5f) * 0.072f * effective + sx0 * redPhase * 0.030f) * (w - 1)), 0, w - 1);
                    int sy = Math.Clamp((int)((v + (texA - 0.5f) * 0.056f * effective + sy0 * cyanPhase * 0.030f) * (h - 1)), 0, h - 1);
                    int previousIndex = (stride > 0 ? sy * stride : (h - 1 - sy) * -stride) + sx * 4;
                    float previousR = Math.Min(previous[previousIndex + 2] * 2.1f, 255f);
                    float previousG = Math.Min(previous[previousIndex + 1] * 2.1f, 255f);
                    float previousB = Math.Min(previous[previousIndex + 0] * 2.1f, 255f);
                    float blendNoise = SampleNoise(noise, nw, nh, ns, u * 1.4f + previousR * 0.0027f, v * 0.9f + previousB * 0.0027f);
                    float datamosh = SmoothStep(1f - NilkDatamoshIntensity, 1f, blendNoise * 0.5f) *
                                     MathF.Pow(overlayPower, 1.65f) *
                                     (0.10f + cyanPhase * 0.07f + voidPhase * 0.08f + redPhase * 0.14f);

                    if (datamosh > 0.001f)
                    {
                        r = Lerp(r, previousR, datamosh);
                        g = Lerp(g, previousG, datamosh);
                        b = Lerp(b, previousB, datamosh);
                    }

                    float fragment = SmoothStep(0.974f, 0.998f, SampleNoise(noise, nw, nh, ns, swirlU * 7.6f + time * 0.030f, swirlV * 6.2f - time * 0.020f));
                    if (fragment > 0.001f)
                    {
                        r = Lerp(r, 245f, fragment * 0.72f);
                        g = Lerp(g, 45f + greenBand * 210f, fragment);
                        b = Lerp(b, 220f, fragment * 0.65f);
                    }

                    float laceMix = textureLace * 0.56f;
                    r = Lerp(r, 62f + 156f * textureLace, laceMix);
                    g = Lerp(g, 28f + 178f * textureLace, laceMix);
                    b = Lerp(b, 172f + 78f * textureLace, laceMix);

                    float mandalaMix = mandala * 0.62f;
                    r = Lerp(r, 112f + 120f * mandala, mandalaMix);
                    g = Lerp(g, 52f + 142f * mandala, mandalaMix);
                    b = Lerp(b, 214f + 40f * mandala, mandalaMix);

                    float glyphHue = glyphSeed * MathF.Tau + time * 0.08f;
                    float glyphMix = glyphInk * 0.78f;
                    r = Lerp(r, 130f + 112f * MathF.Sin(glyphHue), glyphMix);
                    g = Lerp(g, 128f + 112f * MathF.Sin(glyphHue + 2.1f), glyphMix);
                    b = Lerp(b, 132f + 112f * MathF.Sin(glyphHue + 4.2f), glyphMix);
                    g += glyphScan * 26f;

                    float chaosShard = SmoothStep(0.54f, 0.72f, texC + field * 0.32f) * chaosMode;
                    r = Lerp(r, 252f, chaosShard * 0.82f);
                    g = Lerp(g, 44f + greenBand * 190f, chaosShard);
                    b = Lerp(b, 242f, chaosShard * 0.86f);

                    float vignette = Math.Clamp(0.76f + (1f - dist * 0.80f) * 0.24f, 0.50f, 1f);
                    float blotch = 0.70f + 0.30f * SmoothStep(0.20f, 0.85f, field + texB * 0.08f);
                    float phaseAlpha = 1f + cyanPhase * 0.26f + voidPhase * 0.10f + redPhase * 0.32f;
                    float colorSignal = Math.Clamp(violetCore * 0.70f + emeraldSheen * 0.45f + broadBand * 0.52f + contour * 0.46f, 0f, 1f);
                    float alphaF = ((0.008f + 0.155f * overlayPower) * phaseAlpha * blotch + contour * 0.150f * overlayPower) * vignette;
                    alphaF += broadBand * 0.105f * overlayPower + datamosh * 0.018f + palePhase * 0.010f * overlayPower +
                              fragment * 0.18f * overlayPower + (cellRim + greenCore) * 0.18f * overlayPower + colorSignal * 0.115f * overlayPower +
                              textureLace * 0.12f * overlayPower + mandala * 0.14f * overlayPower + (glyphInk * 0.22f + glyphScan * 0.05f + chaosShard * 0.10f) * overlayPower;
                    alphaF = Math.Clamp(alphaF, 0f, 0.66f);

                    r = Math.Clamp(r, 0f, 255f);
                    g = Math.Clamp(g, 0f, 255f);
                    b = Math.Clamp(b, 0f, 255f);
                    target[index + 0] = (byte)(b * alphaF);
                    target[index + 1] = (byte)(g * alphaF);
                    target[index + 2] = (byte)(r * alphaF);
                    target[index + 3] = (byte)(alphaF * 255f);
                }
            }

            Marshal.Copy(target, 0, targetData.Scan0, target.Length);
            Buffer.BlockCopy(target, 0, previous, 0, targetBytes);
        }
        finally
        {
            tripBitmap.UnlockBits(targetData);
        }
    }

    private void DrawNauseaOverlay(int w, int h, float p)
    {
        if (tripGraphics is null)
            return;

        float cx = w * 0.5f + MathF.Sin(time * 0.73f) * w * 0.035f * p;
        float cy = h * 0.5f + MathF.Cos(time * 0.61f) * h * 0.030f * p;
        float minSide = Math.Min(w, h);

        for (int i = 0; i < 5; i++)
        {
            float phase = i * 1.17f;
            float rx = minSide * (0.20f + i * 0.115f + 0.035f * MathF.Sin(time * 0.8f + phase));
            float ry = rx * (0.70f + 0.10f * MathF.Cos(time * 0.9f + phase));
            RectangleF rect = new(cx - rx, cy - ry, rx * 2f, ry * 2f);
            Color color = PaletteColor(time * 0.06f + i * 0.15f);
            using Pen pen = new(WithAlpha(color, (0.030f + i * 0.004f) * p), 3f + 3f * p)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            tripGraphics.DrawArc(pen, rect, time * 22f + i * 51f, 250f + 55f * MathF.Sin(time * 0.7f + phase));
        }

        using GraphicsPath path = new();
        float r = minSide * (0.24f + 0.06f * MathF.Sin(time * 1.1f));
        path.AddEllipse(cx - r, cy - r * 0.75f, r * 2f, r * 1.5f);
        using PathGradientBrush brush = new(path);
        brush.CenterColor = WithAlpha(PaletteColor(time * 0.08f + 0.25f), 0.075f * p);
        brush.SurroundColors = [Color.Transparent];
        tripGraphics.FillPath(brush, path);
    }

    private void DrawSmoothNilkWaves(Graphics g, int w, int h, float p)
    {
        int bands = 7;
        for (int i = 0; i < bands; i++)
        {
            float phase = i * 0.82f;
            float y = h * ((i + 0.5f) / bands) + MathF.Sin(time * 0.55f + phase) * h * 0.055f * p;
            float thickness = h * (0.10f + 0.035f * MathF.Sin(time * 0.7f + phase));
            RectangleF rect = new(-w * 0.08f, y - thickness * 0.5f, w * 1.16f, thickness);
            Color a = PaletteColor(time * 0.045f + phase);
            Color b = PaletteColor(time * 0.050f + phase + 0.28f);
            using LinearGradientBrush brush = new(rect, WithAlpha(a, 0.038f * p), WithAlpha(b, 0.018f * p), 18f + 14f * MathF.Sin(time * 0.3f + phase));
            g.FillEllipse(brush, rect);
        }
    }

    private void DrawPreviousFrameDatamosh(Graphics g, int w, int h, float p)
    {
        if (previousTripBitmap is null || p <= 0.02f)
            return;

        int slices = 4;
        float alpha = Math.Clamp(0.045f * p, 0f, 0.055f);
        for (int i = 0; i < slices; i++)
        {
            int seed = Hash(i * 733 + (int)(time * 9f));
            int y = Math.Abs(seed % Math.Max(h, 1));
            int height = Math.Clamp(18 + Math.Abs(Hash(seed + 11) % 90), 18, Math.Max(18, h - y));
            int shift = (int)((Hash(seed + 17) % 28) * p);
            Rectangle src = new(0, y, w, Math.Min(height, h - y));
            Rectangle dst = new(shift - 14, y, w + 28, src.Height);
            DrawImageAlpha(g, previousTripBitmap, dst, alpha);
        }
    }

    private void DrawDizzyNilkVortex(Graphics g, int w, int h, float p)
    {
        if (psychedelicTexture is null || p <= 0.03f)
            return;

        float cx = w * 0.5f;
        float cy = h * 0.5f;
        int rings = 7;
        float maxR = MathF.Sqrt(w * w + h * h) * 0.56f;
        float drunk = 1f + p * 0.35f;

        for (int i = rings - 1; i >= 0; i--)
        {
            float t = i / (float)(rings - 1);
            float pulse = 1f + (0.075f + 0.055f * p) * MathF.Sin(time * 1.55f + t * 9.0f);
            float radius = maxR * (0.10f + t * 0.98f) * pulse;
            float angle = time * (11f + 13f * p) + t * 116f;
            float scale = 0.25f + t * 1.02f + 0.095f * MathF.Sin(time * 1.1f + i);
            float rw = w * scale * drunk;
            float rh = h * scale * (0.94f + 0.12f * MathF.Sin(time * 0.82f + t * 5f));
            float x = cx - rw * 0.5f + MathF.Cos(angle * MathF.PI / 180f) * radius * 0.16f * p;
            float y = cy - rh * 0.5f + MathF.Sin(angle * MathF.PI / 180f) * radius * 0.13f * p;
            RectangleF dest = new(x, y, rw, rh);

            GraphicsState state = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(angle + MathF.Sin(time * 0.95f + t * 8f) * 9f * p);
            g.TranslateTransform(-cx, -cy);
            DrawImageAlpha(g, psychedelicTexture, dest, (0.024f + 0.028f * (1f - t)) * p);
            g.Restore(state);
        }
    }

    private void DrawFullScreenIridescence(Graphics g, int w, int h, float p)
    {
        RectangleF rect = new(-w * 0.35f, -h * 0.35f, w * 1.7f, h * 1.7f);
        float angle = 28f + MathF.Sin(time * 0.16f) * (22f + 28f * p);

        using LinearGradientBrush brush = new(rect, Color.Transparent, Color.Transparent, angle);
        brush.InterpolationColors = new ColorBlend
        {
            Positions = [0f, 0.14f, 0.31f, 0.48f, 0.64f, 0.83f, 1f],
            Colors =
            [
                WithAlpha(PaletteColor(time * 0.035f + 0.00f), 0.000f),
                WithAlpha(PaletteColor(time * 0.035f + 0.13f), 0.025f * p),
                WithAlpha(PaletteColor(time * 0.035f + 0.29f), 0.055f * p),
                WithAlpha(PaletteColor(time * 0.035f + 0.47f), 0.035f * p),
                WithAlpha(PaletteColor(time * 0.035f + 0.63f), 0.070f * p),
                WithAlpha(PaletteColor(time * 0.035f + 0.82f), 0.030f * p),
                WithAlpha(PaletteColor(time * 0.035f + 1.00f), 0.000f)
            ]
        };

        g.FillRectangle(brush, 0, 0, w, h);
    }

    private void DrawLsdTextureSheets(Graphics g, int w, int h, float p)
    {
        DrawTextureLayer(g, cosmosTexture, w, h, 0.016f, 1.22f, 0.030f * p, 0.00f);
        DrawTextureLayer(g, psychedelicTexture, w, h, -0.022f, 1.05f, 0.052f * p, 0.27f);
        DrawTextureLayer(g, voidTexture, w, h, 0.013f, 1.38f, 0.034f * p, 0.52f);

        if (mode == EffectMode.FullTrip)
            DrawTextureLayer(g, noiseTexture, w, h, -0.035f, 0.82f, 0.045f * p, 0.78f);
    }

    private void DrawTextureLayer(Graphics g, Bitmap? texture, int w, int h, float speed, float scale, float alpha, float phase)
    {
        if (texture is null || alpha <= 0.002f)
            return;

        float destW = w * scale;
        float destH = h * scale;
        float span = destW * 0.42f;
        float scroll = (Frac(time * speed + phase) - 0.5f) * span;
        float wobble = MathF.Sin(time * (0.13f + MathF.Abs(speed)) + phase * 8f) * h * 0.055f;
        RectangleF dest = new((w - destW) * 0.5f + scroll, (h - destH) * 0.5f + wobble, destW, destH);

        GraphicsState state = g.Save();
        g.TranslateTransform(w * 0.5f, h * 0.5f);
        g.RotateTransform(MathF.Sin(time * (0.09f + MathF.Abs(speed)) + phase * 5f) * (2.5f + 4.5f * alpha));
        g.TranslateTransform(-w * 0.5f, -h * 0.5f);
        DrawImageAlpha(g, texture, dest, alpha);
        g.Restore(state);
    }

    private void DrawPaletteRemapVeil(Graphics g, int w, int h, float p)
    {
        int bands = 9;
        for (int i = 0; i < bands; i++)
        {
            float t0 = i / (float)bands;
            float t1 = (i + 1f) / bands;
            float y0 = h * t0;
            float y1 = h * t1;
            float phase = time * 0.11f + i * 0.173f;
            Color a = PaletteColor(phase);
            Color b = PaletteColor(phase + 0.18f + 0.06f * MathF.Sin(time * 0.21f + i));
            RectangleF rect = new(-w * 0.08f, y0 - h * 0.03f, w * 1.16f, y1 - y0 + h * 0.08f);

            using LinearGradientBrush brush = new(rect, WithAlpha(a, 0.045f * p), WithAlpha(b, 0.026f * p), 12f + 20f * MathF.Sin(phase));
            g.FillRectangle(brush, rect);
        }
    }

    private void DrawDatamoshSmears(Graphics g, int w, int h, float p)
    {
        int smears = 18;
        for (int i = 0; i < smears; i++)
        {
            int seed = Hash(i * 1543 + (int)(time * 7f));
            float y = Math.Abs(seed % Math.Max(h, 1));
            float x = Math.Abs(Hash(seed + 3) % Math.Max(w, 1)) - w * 0.12f;
            float width = w * (0.12f + 0.28f * Wave01(time * 0.19f + i));
            float height = 6f + Math.Abs(Hash(seed + 7) % 34) * (0.45f + p * 0.65f);
            Color color = PaletteColor(time * 0.063f + i * 0.071f);

            using LinearGradientBrush brush = new(
                new RectangleF(x, y, width, height),
                WithAlpha(color, 0.048f * p),
                Color.Transparent,
                0f);
            g.FillRectangle(brush, x, y, width, height);
        }
    }

    private void DrawNilkPulseBloom(Graphics g, int w, int h, float p)
    {
        int blooms = 5;
        float minSide = Math.Min(w, h);
        for (int i = 0; i < blooms; i++)
        {
            float phase = i * 1.337f;
            float x = w * (0.13f + 0.74f * Wave01(time * 0.039f + phase));
            float y = h * (0.11f + 0.78f * Wave01(time * 0.047f + phase * 1.27f));
            float r = minSide * (0.18f + 0.11f * Wave01(time * 0.16f + phase));

            using GraphicsPath path = new();
            path.AddEllipse(x - r, y - r * 0.72f, r * 2f, r * 1.44f);
            using PathGradientBrush brush = new(path);
            brush.CenterColor = WithAlpha(PaletteColor(time * 0.066f + phase), 0.070f * p);
            brush.SurroundColors = [Color.Transparent];
            g.FillPath(brush, path);
        }
    }

    private void DrawAuroraBlobs(Graphics g, int w, int h, float p)
    {
        int count = mode == EffectMode.FullTrip ? 6 : 5 + (int)MathF.Round(4f * Math.Clamp(p, 0f, 1f));
        for (int i = 0; i < count; i++)
        {
            float phase = i * 0.91f;
            float x = w * (0.10f + 0.82f * Wave01(time * (0.055f + p * 0.035f) + phase));
            float y = h * (0.08f + 0.84f * Wave01(time * (0.043f + p * 0.028f) + phase * 1.37f));
            float radius = Math.Min(w, h) * (0.14f + 0.09f * p + 0.035f * MathF.Sin(time * 0.25f + i));
            RectangleF ellipse = new(x - radius, y - radius * 0.72f, radius * 2.25f, radius * 1.44f);

            using GraphicsPath path = new();
            path.AddEllipse(ellipse);
            using PathGradientBrush brush = new(path);
            brush.CenterColor = WithAlpha(PaletteColor(time * 0.047f + i * 0.137f), 0.060f * p);
            brush.SurroundColors = [Color.Transparent];
            g.FillPath(brush, path);
        }
    }

    private void DrawNilkWarpField(Graphics g, int w, int h, float p)
    {
        int layers = mode switch
        {
            EffectMode.Flow => 5,
            EffectMode.TextureTrip => 7,
            EffectMode.GlyphGlitch => 8,
            EffectMode.FullTrip => 9,
            _ => 6
        };

        for (int layer = 0; layer < layers; layer++)
        {
            float phase = layer * 0.73f;
            int points = 18;
            PointF[] cyanPoints = new PointF[points];
            PointF[] pinkPoints = new PointF[points];
            float baseY = h * ((layer + 0.5f) / layers);
            float amp = h * (0.018f + 0.030f * p);
            float speed = time * (0.42f + layer * 0.025f);

            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                float x = -w * 0.08f + t * w * 1.16f;
                float warp = MathF.Sin(t * MathF.Tau * (1.2f + p) + speed + phase) * amp;
                warp += MathF.Sin(t * MathF.Tau * 3.1f - speed * 0.7f + phase) * amp * 0.45f;
                float y = baseY + warp;
                cyanPoints[i] = new PointF(x, y - 2f - 8f * p);
                pinkPoints[i] = new PointF(x + 4f + 12f * p, y + 2f + 7f * p);
            }

            using Pen cyan = new(WithAlpha(PaletteColor(time * 0.070f + phase), 0.055f * p), 1.6f + 4.2f * p)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            using Pen pink = new(WithAlpha(PaletteColor(time * 0.075f + phase + 0.42f), 0.048f * p), 1.2f + 3.4f * p)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            g.DrawCurve(cyan, cyanPoints, 0.62f);
            if ((layer & 1) == 0)
                g.DrawCurve(pink, pinkPoints, 0.62f);
        }
    }

    private void DrawLiquidCells(Graphics g, int w, int h, float p)
    {
        int count = mode switch
        {
            EffectMode.Flow => 8,
            EffectMode.TextureTrip => 11,
            EffectMode.GlyphGlitch => 12,
            EffectMode.FullTrip => 10,
            _ => 10
        };

        float minSide = Math.Min(w, h);
        for (int i = 0; i < count; i++)
        {
            float phase = i * 0.618f;
            float x = w * (0.08f + 0.84f * Wave01(time * (0.071f + i * 0.002f) + phase * 2.1f));
            float y = h * (0.08f + 0.84f * Wave01(time * (0.057f + i * 0.003f) + phase * 1.6f));
            float radius = minSide * (0.055f + 0.075f * p + 0.020f * Wave01(time * 0.41f + phase));

            using GraphicsPath path = new();
            int points = 9;
            for (int n = 0; n < points; n++)
            {
                float a = n * MathF.Tau / points;
                float wobble = 0.78f + 0.34f * Wave01(time * (0.82f + i * 0.015f) + n * 1.7f + phase);
                float px = x + MathF.Cos(a) * radius * wobble * (1.18f + 0.20f * MathF.Sin(phase));
                float py = y + MathF.Sin(a) * radius * wobble * (0.76f + 0.18f * MathF.Cos(phase));

                if (n == 0)
                    path.StartFigure();
                path.AddLine(px, py, px, py);
            }
            path.CloseFigure();

            using PathGradientBrush brush = new(path);
            brush.CenterColor = WithAlpha(PaletteColor(time * 0.052f + phase), 0.060f * p);
            brush.SurroundColors = [WithAlpha(PaletteColor(time * 0.047f + phase + 0.42f), 0.010f * p)];
            g.FillPath(brush, path);

            using Pen rim = new(WithAlpha(PaletteColor(time * 0.060f + phase + 0.18f), 0.055f * p), 1.5f + 3.0f * p);
            g.DrawPath(rim, path);
        }
    }

    private void DrawPsychedelicRings(Graphics g, int w, int h, float p)
    {
        int centers = mode == EffectMode.FullTrip ? 3 : 3;
        float minSide = Math.Min(w, h);
        for (int c = 0; c < centers; c++)
        {
            float phase = c * 1.91f;
            float cx = w * (0.18f + 0.64f * Wave01(time * 0.052f + phase));
            float cy = h * (0.18f + 0.64f * Wave01(time * 0.046f + phase * 1.31f));
            int rings = mode == EffectMode.Flow ? 4 : 5 + (mode == EffectMode.FullTrip ? 1 : 0);

            for (int r = 0; r < rings; r++)
            {
                float t = (r + Frac(time * (0.18f + c * 0.025f))) / rings;
                float radius = minSide * (0.045f + t * (0.30f + 0.12f * p));
                float rx = radius * (1.0f + 0.22f * MathF.Sin(time * 0.44f + phase + r));
                float ry = radius * (0.72f + 0.18f * MathF.Cos(time * 0.37f + phase - r));
                RectangleF rect = new(cx - rx, cy - ry, rx * 2f, ry * 2f);
                Color color = PaletteColor(time * 0.080f + c * 0.19f + r * 0.11f);
                using Pen pen = new(WithAlpha(color, (0.030f + 0.040f * (1f - t)) * p), 2.0f + p * 5.0f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                g.DrawArc(pen, rect, time * 28f + r * 37f, 250f + 80f * Wave01(time * 0.32f + r));
            }
        }
    }

    private void DrawFlowRibbons(Graphics g, int w, int h, float p)
    {
        int layers = mode == EffectMode.Flow ? 4 : 6 + (mode == EffectMode.FullTrip ? 2 : 0);
        for (int layer = 0; layer < layers; layer++)
        {
            using GraphicsPath path = new();
            float y = h * (0.08f + layer * (0.84f / Math.Max(layers - 1, 1))) + MathF.Sin(time * 0.24f + layer * 1.9f) * h * (0.030f + 0.035f * p);
            float amp = h * (0.040f + 0.045f * p + layer * 0.003f);
            float speed = time * (0.36f + p * 0.16f + layer * 0.045f);

            PointF p0 = new(-w * 0.12f, y + MathF.Sin(speed + layer) * amp);
            PointF p1 = new(w * 0.22f, y - amp * 1.35f + MathF.Cos(speed * 1.21f) * amp);
            PointF p2 = new(w * 0.54f, y + amp * 1.55f + MathF.Sin(speed * 0.83f + 1f) * amp);
            PointF p3 = new(w * 1.12f, y + MathF.Cos(speed + layer) * amp);
            path.AddBezier(p0, p1, p2, p3);

            float baseWidth = 24f + p * 92f + layer * 3f;
            Color main = PaletteColor(time * 0.055f + layer * 0.12f);
            Color second = PaletteColor(time * 0.055f + layer * 0.12f + 0.33f);
            Color third = PaletteColor(time * 0.055f + layer * 0.12f + 0.62f);

            using Pen glow = new(WithAlpha(main, 0.045f * p), baseWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using Pen core = new(WithAlpha(second, 0.090f * p), baseWidth * 0.42f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using Pen edge = new(WithAlpha(third, 0.075f * p), Math.Max(2f, baseWidth * 0.10f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawPath(glow, path);
            g.DrawPath(core, path);
            g.DrawPath(edge, path);
        }
    }

    private void DrawSpiralVortex(Graphics g, int w, int h, float p)
    {
        if (mode == EffectMode.Flow && p < 0.18f)
            return;

        int arms = mode == EffectMode.FullTrip ? 6 : 4;
        float cx = w * (0.5f + 0.07f * MathF.Sin(time * 0.17f));
        float cy = h * (0.5f + 0.06f * MathF.Cos(time * 0.13f));
        float maxR = Math.Min(w, h) * (0.24f + 0.22f * p);

        for (int arm = 0; arm < arms; arm++)
        {
            using GraphicsPath path = new();
            bool started = false;
            float phase = arm * MathF.Tau / arms + time * (0.18f + 0.06f * p);
            for (int step = 0; step < 34; step++)
            {
                float t = step / 33f;
                float angle = phase + t * (MathF.Tau * (1.15f + p * 0.85f));
                float wobble = 1f + 0.14f * MathF.Sin(time * 1.1f + step * 0.65f + arm);
                float r = maxR * t * wobble;
                float x = cx + MathF.Cos(angle) * r * (1.18f + 0.08f * MathF.Sin(phase));
                float y = cy + MathF.Sin(angle) * r * (0.82f + 0.08f * MathF.Cos(phase));

                if (!started)
                {
                    path.StartFigure();
                    started = true;
                }
                path.AddLine(x, y, x, y);
            }

            Color color = PaletteColor(time * 0.075f + arm * 0.14f);
            using Pen glow = new(WithAlpha(color, 0.055f * p), 16f + 34f * p) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen core = new(WithAlpha(PaletteColor(time * 0.080f + arm * 0.14f + 0.33f), 0.075f * p), 3f + 8f * p) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawPath(glow, path);
            g.DrawPath(core, path);
        }
    }

    private void DrawChromaticThreading(Graphics g, int w, int h, float p)
    {
        int count = 10 + (int)(12f * Math.Clamp(p, 0f, 1f));
        for (int i = 0; i < count; i++)
        {
            float y = h * ((i + 0.5f) / count) + MathF.Sin(time * (0.62f + p * 0.35f) + i * 0.73f) * (18f + 30f * p);
            float offset = MathF.Sin(time * 0.53f + i) * (24f + 35f * p);

            using Pen cyan = new(Color.FromArgb((int)(28 * p), 40, 255, 235), 1.2f + p * 2.8f);
            using Pen pink = new(Color.FromArgb((int)(26 * p), 255, 65, 210), 1.0f + p * 2.0f);
            g.DrawBezier(cyan, -60, y + offset, w * 0.25f, y - 58 - offset * 0.15f, w * 0.72f, y + 58 + offset * 0.15f, w + 60, y - offset);
            if ((i & 1) == 0)
                g.DrawBezier(pink, -60, y + offset + 8, w * 0.30f, y - 45, w * 0.66f, y + 45, w + 60, y - offset + 8);
        }
    }

    private void DrawGlitchScanlines(Graphics g, int w, int h, float p)
    {
        int bands = mode == EffectMode.FullTrip ? 12 : 6;
        for (int i = 0; i < bands; i++)
        {
            int seed = Hash((int)(time * 13f) + i * 971);
            float y = Math.Abs(seed % Math.Max(h, 1));
            float bandH = 2f + Math.Abs(Hash(seed + 17) % 18) * (0.30f + p * 0.70f);
            float xShift = (Hash(seed + 23) % 90) * p;
            Color color = (i & 1) == 0 ? Color.FromArgb((int)(34 * p), 255, 255, 255) : PaletteColor(i * 0.13f + time * 0.05f);
            using Brush brush = new SolidBrush(Color.FromArgb(Math.Clamp((int)(color.A == 255 ? 22 * p : color.A), 0, 70), color.R, color.G, color.B));
            g.FillRectangle(brush, -xShift, y, w + xShift * 2f, bandH);
        }
    }

    private void DrawGlyphStorm(Graphics g, int w, int h, float p)
    {
        int rows = mode == EffectMode.FullTrip ? 9 : 5;
        int chars = Math.Clamp(w / 24, 18, 80);

        for (int row = 0; row < rows; row++)
        {
            int seed = Hash(row * 4099 + (int)(time * (5f + row % 5)));
            string run = MakeGlyphRun(seed, chars);
            float y = h * ((row + 0.35f) / rows) + MathF.Sin(time * 0.55f + row) * (12f + 22f * p);
            float x = -120f + Frac(row * 0.173f + time * (0.018f + row * 0.0012f)) * 230f;
            float jitter = (Hash(seed + 88) % 41) * p;
            int backAlpha = (int)(5f * p);
            int textAlpha = (int)(42f * p);

            if (backAlpha > 1 && row % 3 == 0)
            {
                using Brush back = new SolidBrush(Color.FromArgb(backAlpha, 245, 246, 255));
                g.FillRectangle(back, 0, y - 6, w, 24f + 14f * p);
            }

            using Brush shadow = new SolidBrush(Color.FromArgb(Math.Clamp((int)(60f * p), 0, 115), 0, 0, 0));
            using Brush cyan = new SolidBrush(Color.FromArgb(Math.Clamp(textAlpha, 0, 150), 65, 255, 235));
            using Brush pink = new SolidBrush(Color.FromArgb(Math.Clamp(textAlpha, 0, 150), 255, 58, 211));
            g.DrawString(run, glyphFont, cyan, x + jitter + 2f, y - 1f);
            g.DrawString(run, glyphFont, pink, x - jitter - 2f, y + 1f);
            g.DrawString(run, glyphFont, shadow, x, y);
        }

        if (mode == EffectMode.FullTrip)
        {
                            for (int i = 0; i < 3; i++)

            {
                string glyph = GlyphBank[Math.Abs(Hash(i * 191 + (int)(time * 4f))) % GlyphBank.Length].ToString();
                float x = w * Wave01(time * 0.19f + i * 1.37f);
                float y = h * Wave01(time * 0.23f + i * 0.91f);
                                    using Brush brush = new SolidBrush(WithAlpha(PaletteColor(i * 0.17f + time * 0.08f), 0.045f * p));

                g.DrawString(glyph, glyphFontLarge, brush, x, y);
            }
        }
    }

    private void DrawDatamoshBlocks(Graphics g, int w, int h, float p)
    {
        int blocks = 18;
        for (int i = 0; i < blocks; i++)
        {
            int seed = Hash(i * 757 + (int)(time * 8f));
            float bw = 40f + Math.Abs(Hash(seed + 1) % 180) * p;
            float bh = 12f + Math.Abs(Hash(seed + 2) % 60) * p;
            float x = Math.Abs(Hash(seed + 3) % Math.Max(w, 1));
            float y = Math.Abs(Hash(seed + 4) % Math.Max(h, 1));
            Color c = PaletteColor(i * 0.071f + time * 0.09f);
            using Brush brush = new SolidBrush(WithAlpha(c, 0.030f * p));
            g.FillRectangle(brush, x, y, bw, bh);
        }
    }

    private void DrawKaleidoscopeSigils(Graphics g, int w, int h, float p)
    {
        float cx = w * 0.5f + MathF.Sin(time * 0.21f) * w * 0.08f;
        float cy = h * 0.5f + MathF.Cos(time * 0.19f) * h * 0.08f;
        int arms = 8;
        for (int i = 0; i < arms; i++)
        {
            float a = time * 0.21f + i * MathF.PI * 2f / arms;
            float r = Math.Min(w, h) * (0.17f + 0.12f * Wave01(time * 0.31f + i));
            float x = cx + MathF.Cos(a) * r;
            float y = cy + MathF.Sin(a) * r;
            RectangleF rect = new(x - 42f - p * 35f, y - 42f - p * 35f, 84f + p * 70f, 84f + p * 70f);
            using GraphicsPath path = new();
            path.AddEllipse(rect);
            using PathGradientBrush brush = new(path);
            brush.CenterColor = WithAlpha(PaletteColor(i * 0.11f + time * 0.06f), 0.060f * p);
            brush.SurroundColors = [Color.Transparent];
            g.FillPath(brush, path);
        }
    }

    private void DrawMinimalSparkle(Graphics g, int w, int h, float p)
    {
        unchecked
        {
            int seed = 0x4E494C4B;
            int count = 70 + (int)(90f * Math.Clamp(p, 0f, 1f));
            for (int i = 0; i < count; i++)
            {
                seed = seed * 1664525 + 1013904223;
                float x = Math.Abs(seed % Math.Max(w, 1));
                seed = seed * 1664525 + 1013904223;
                float y = Math.Abs(seed % Math.Max(h, 1));
                float twinkle = 0.45f + 0.55f * Wave01(time * 1.5f + i * 0.77f);
                int alpha = (int)(twinkle * 34f * p);
                if (alpha <= 1)
                    continue;

                float drift = time * (9f + (i % 5) * 3f);
                x = (x + drift) % Math.Max(w, 1);
                float size = 1f + (i % 3) + p * 1.4f;
                using Brush brush = new SolidBrush(Color.FromArgb(alpha, 185, 255, 245));
                g.FillEllipse(brush, x, y, size, size);
            }
        }
    }

    private void DrawSoftVignette(Graphics g, int w, int h, float p)
    {
        using GraphicsPath path = new();
        path.AddEllipse(-w * 0.20f, -h * 0.25f, w * 1.40f, h * 1.50f);
        using PathGradientBrush brush = new(path);
        brush.CenterColor = Color.Transparent;
        brush.SurroundColors = [Color.FromArgb((int)(24 * p), 0, 0, 0)];
        g.FillRectangle(brush, 0, 0, w, h);
    }

    private void DrawHint(Graphics g, int w, int h)
    {
        if (hintSeconds <= 0f)
            return;

        int alpha = (int)(Math.Clamp(hintSeconds / 1.2f, 0f, 1f) * 165f);
        string state = paused ? $"PAUSED | {ModeName(mode)}" : $"{ModeName(mode)} | INT {(int)MathF.Round(targetIntensity * 100f)}%";
        string text = hintOverride ?? $"Ctrl+Alt+1-7 modes | Ctrl+Alt+PageUp/Down: Nilk seek | Ctrl+Alt+Left/Right cycle | Up/Down power | Space pause | Esc exit | {state}";

        SizeF textSize = g.MeasureString(text, hintFont);
        RectangleF box = new(16, h - textSize.Height - 28, Math.Min(textSize.Width + 18, w - 32), textSize.Height + 10);
        using GraphicsPath rounded = RoundedRect(box, 8f);
        using Brush bg = new SolidBrush(Color.FromArgb(alpha / 2, 5, 4, 18));
        using Brush fg = new SolidBrush(Color.FromArgb(alpha, 218, 252, 255));
        g.FillPath(bg, rounded);
        g.DrawString(text, hintFont, fg, box.X + 9, box.Y + 5);
    }

    private static string ModeName(EffectMode effectMode)
    {
        return effectMode switch
        {
            EffectMode.Flow => "FLOW",
            EffectMode.TextureTrip => "TEXTURE TRIP",
            EffectMode.GlyphGlitch => "SOFT GLITCH",
            EffectMode.FullTrip => "FULL TRIP",
            EffectMode.Chaos => "CHAOS",
            EffectMode.Nilk => "NILK CYCLE",
            EffectMode.NilkSeek => "NILK SEEK",
            _ => "UNKNOWN"
        };
    }

    private Color PaletteColor(float t)
    {
        t = Frac(t);
        float scaled = t * (nilkPalette.Length - 1);
        int a = (int)MathF.Floor(scaled);
        int b = Math.Min(a + 1, nilkPalette.Length - 1);
        float f = scaled - a;
        return Color.FromArgb(
            255,
            (int)Lerp(nilkPalette[a].R, nilkPalette[b].R, f),
            (int)Lerp(nilkPalette[a].G, nilkPalette[b].G, f),
            (int)Lerp(nilkPalette[a].B, nilkPalette[b].B, f));
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = Saturate(t);
        return Color.FromArgb(
            255,
            (int)Lerp(a.R, b.R, t),
            (int)Lerp(a.G, b.G, t),
            (int)Lerp(a.B, b.B, t));
    }

    private static float Saturate(float value) => Math.Clamp(value, 0f, 1f);

    private static float Bell(float value, float center, float width)
    {
        float x = (value - center) / Math.Max(width, 0.001f);
        return MathF.Exp(-x * x);
    }

    private static float BellWrapped(float value, float center, float period, float width)
    {
        float d = MathF.Abs(value - center);
        d = MathF.Min(d, period - d);
        return MathF.Exp(-(d * d) / Math.Max(width * width, 0.001f));
    }

    private static float TimeWindow(float value, float start, float end, float softness)
    {
        return SmoothStep(start - softness, start + softness, value) *
               (1f - SmoothStep(end - softness, end + softness, value));
    }

    private static float SampleNoise(byte[]? pixels, int width, int height, int stride, float u, float v)
    {
        if (pixels is null || width <= 0 || height <= 0 || stride == 0)
            return ValueNoise(u * 8.0f, v * 8.0f);

        u = Frac(u);
        v = Frac(v);
        int x = Math.Clamp((int)(u * width), 0, width - 1);
        int y = Math.Clamp((int)(v * height), 0, height - 1);
        int row = stride > 0 ? y * stride : (height - 1 - y) * -stride;
        int index = row + x * 4;
        return (pixels[index] + pixels[index + 1] + pixels[index + 2]) / 765f;
    }

    private static string MakeGlyphRun(int seed, int length)
    {
        StringBuilder builder = new(length);
        int state = seed;
        for (int i = 0; i < length; i++)
        {
            state = Hash(state + i * 131);
            builder.Append(GlyphBank[Math.Abs(state) % GlyphBank.Length]);
        }

        return builder.ToString();
    }

    private static void DrawImageAlpha(Graphics g, Image image, RectangleF dest, float alpha)
    {
        alpha = Math.Clamp(alpha, 0f, 1f);
        if (alpha <= 0.001f)
            return;

        using ImageAttributes attributes = new();
        ColorMatrix matrix = new()
        {
            Matrix00 = 1f,
            Matrix11 = 1f,
            Matrix22 = 1f,
            Matrix33 = alpha,
            Matrix44 = 1f
        };
        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        Rectangle rounded = Rectangle.Round(dest);
        g.DrawImage(image, rounded, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return Color.FromArgb((int)Math.Clamp(alpha * 255f, 0f, 255f), color.R, color.G, color.B);
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        GraphicsPath path = new();
        float d = radius * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void UpdateLayeredWindowFast()
    {
        nint screenDc = GetDC(0);
        NativePoint topPos = new(Left, Top);
        NativeSize size = new(surfaceWidth, surfaceHeight);
        NativePoint sourcePos = new(0, 0);
        BlendFunction blend = new()
        {
            BlendOp = AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = AC_SRC_ALPHA
        };

        UpdateLayeredWindow(Handle, screenDc, ref topPos, ref size, memoryDc, ref sourcePos, 0, ref blend, ULW_ALPHA);
        ReleaseDC(0, screenDc);
    }

    private void DisposeAssets()
    {
        hintFont.Dispose();
        glyphFont.Dispose();
        glyphFontLarge.Dispose();
        noiseTexture?.Dispose();
        cosmosTexture?.Dispose();
        psychedelicTexture?.Dispose();
        voidTexture?.Dispose();
    }

    private void DisposeRenderSurface()
    {
        frameGraphics?.Dispose();
        frameGraphics = null;
        frameBitmap?.Dispose();
        frameBitmap = null;
        tripGraphics?.Dispose();
        tripGraphics = null;
        tripBitmap?.Dispose();
        tripBitmap = null;
        previousTripGraphics?.Dispose();
        previousTripGraphics = null;
        previousTripBitmap?.Dispose();
        previousTripBitmap = null;
        tripPixelBuffer = null;
        previousTripPixelBuffer = null;
        tripWidth = 0;
        tripHeight = 0;

        if (memoryDc != 0 && oldBitmap != 0)
        {
            SelectObject(memoryDc, oldBitmap);
            oldBitmap = 0;
        }

        if (dibBitmap != 0)
        {
            DeleteObject(dibBitmap);
            dibBitmap = 0;
        }

        if (memoryDc != 0)
        {
            DeleteDC(memoryDc);
            memoryDc = 0;
        }

        bits = 0;
    }

    private void RegisterOverlayHotkeys()
    {
        uint mods = MOD_CONTROL | MOD_ALT | MOD_NOREPEAT;
        uint directMods = MOD_NOREPEAT;

        RegisterHotKey(Handle, HotkeyFlowMode, mods, (uint)Keys.D1);
        RegisterHotKey(Handle, HotkeyTextureMode, mods, (uint)Keys.D2);
        RegisterHotKey(Handle, HotkeyGlyphMode, mods, (uint)Keys.D3);
        RegisterHotKey(Handle, HotkeyFullMode, mods, (uint)Keys.D4);
        RegisterHotKey(Handle, HotkeyChaosMode, mods, (uint)Keys.D5);
        RegisterHotKey(Handle, HotkeyNilkMode, mods, (uint)Keys.D6);
        RegisterHotKey(Handle, HotkeyNilkSeekMode, mods, (uint)Keys.D7);
        RegisterHotKey(Handle, HotkeyNilkNextPhase, mods, (uint)Keys.PageUp);
        RegisterHotKey(Handle, HotkeyNilkPrevPhase, mods, (uint)Keys.PageDown);
        RegisterHotKey(Handle, HotkeyPrevMode, mods, (uint)Keys.Left);
        RegisterHotKey(Handle, HotkeyNextMode, mods, (uint)Keys.Right);
        RegisterHotKey(Handle, HotkeyIntensityUp, mods, (uint)Keys.Up);
        RegisterHotKey(Handle, HotkeyIntensityDown, mods, (uint)Keys.Down);
        RegisterHotKey(Handle, HotkeyTogglePause, mods, (uint)Keys.Space);
        RegisterHotKey(Handle, HotkeyExit, mods, (uint)Keys.Escape);
        RegisterHotKey(Handle, HotkeyToggleScreenCapture, mods, (uint)Keys.R);

        // Keep the original shortcuts usable for existing installations.
        RegisterHotKey(Handle, HotkeyIntensityUpF, directMods, (uint)Keys.Add);
        RegisterHotKey(Handle, HotkeyIntensityDownF, directMods, (uint)Keys.Subtract);
        RegisterHotKey(Handle, HotkeyPrevModeF, directMods, (uint)Keys.NumPad1);
        RegisterHotKey(Handle, HotkeyNextModeF, directMods, (uint)Keys.NumPad2);
        RegisterHotKey(Handle, HotkeyTogglePauseF, mods, (uint)Keys.P);
        RegisterHotKey(Handle, HotkeyExitF, mods, (uint)Keys.Q);
    }

    private void UnregisterOverlayHotkeys()
    {
        UnregisterHotKey(Handle, HotkeyIntensityUp);
        UnregisterHotKey(Handle, HotkeyIntensityDown);
        UnregisterHotKey(Handle, HotkeyTogglePause);
        UnregisterHotKey(Handle, HotkeyExit);
        UnregisterHotKey(Handle, HotkeyFlowMode);
        UnregisterHotKey(Handle, HotkeyTextureMode);
        UnregisterHotKey(Handle, HotkeyGlyphMode);
        UnregisterHotKey(Handle, HotkeyFullMode);
        UnregisterHotKey(Handle, HotkeyPrevMode);
        UnregisterHotKey(Handle, HotkeyNextMode);
        UnregisterHotKey(Handle, HotkeyFlowModeF);
        UnregisterHotKey(Handle, HotkeyTextureModeF);
        UnregisterHotKey(Handle, HotkeyGlyphModeF);
        UnregisterHotKey(Handle, HotkeyFullModeF);
        UnregisterHotKey(Handle, HotkeyIntensityDownF);
        UnregisterHotKey(Handle, HotkeyIntensityUpF);
        UnregisterHotKey(Handle, HotkeyTogglePauseF);
        UnregisterHotKey(Handle, HotkeyExitF);
        UnregisterHotKey(Handle, HotkeyNextModeF);
        UnregisterHotKey(Handle, HotkeyPrevModeF);
        UnregisterHotKey(Handle, HotkeyChaosMode);
        UnregisterHotKey(Handle, HotkeyNilkMode);
        UnregisterHotKey(Handle, HotkeyNilkSeekMode);
        UnregisterHotKey(Handle, HotkeyNilkNextPhase);
        UnregisterHotKey(Handle, HotkeyNilkPrevPhase);
        UnregisterHotKey(Handle, HotkeyToggleScreenCapture);
    }

    private void MakeClickThrough()
    {
        SetInputPassthrough(true);
    }

    private void MakeInteractive()
    {
        SetInputPassthrough(false);
    }

    private void SetInputPassthrough(bool enabled)
    {
        int style = GetWindowLong(Handle, GWL_EXSTYLE);
        style |= WS_EX_LAYERED | WS_EX_TOOLWINDOW;
        if (enabled)
            style |= WS_EX_TRANSPARENT | WS_EX_NOACTIVATE;
        else
            style &= ~(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);

        SetWindowLong(Handle, GWL_EXSTYLE, style);
        _ = SetWindowPos(
            Handle,
            0,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    private static string FindAssetRoot()
    {
        string baseDir = AppContext.BaseDirectory;
        string current = Directory.GetCurrentDirectory();
        string[] candidates =
        [
            baseDir,
            current,
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..")),
            Path.GetFullPath(Path.Combine(current, ".."))
        ];

        foreach (string candidate in candidates)
        {
            if (Directory.Exists(Path.Combine(candidate, "Assets")))
                return candidate;
        }

        return baseDir;
    }

    private void CacheNoiseTexture()
    {
        if (noiseTexture is null)
            return;

        try
        {
            using Bitmap normalized = new(noiseTexture.Width, noiseTexture.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(normalized))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(noiseTexture, 0, 0, normalized.Width, normalized.Height);
            }

            BitmapData data = normalized.LockBits(
                new Rectangle(0, 0, normalized.Width, normalized.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppPArgb);
            try
            {
                noiseWidth = normalized.Width;
                noiseHeight = normalized.Height;
                noiseStride = data.Stride;
                noisePixels = new byte[Math.Abs(data.Stride) * normalized.Height];
                Marshal.Copy(data.Scan0, noisePixels, 0, noisePixels.Length);
            }
            finally
            {
                normalized.UnlockBits(data);
            }
        }
        catch
        {
            noisePixels = null;
            noiseWidth = 0;
            noiseHeight = 0;
            noiseStride = 0;
        }
    }

    private static Bitmap? LoadAsset(string root, params string[] parts)
    {
        try
        {
            string path = Path.Combine([root, .. parts]);
            if (File.Exists(path))
                return new Bitmap(path);

            string resourceName = "PsychoOverlay." + string.Join(".", parts).Replace('\\', '.').Replace('/', '.');
            Stream? resource = typeof(OverlayForm).Assembly.GetManifestResourceStream(resourceName);
            if (resource is not null)
            {
                using Bitmap embedded = new(resource);
                return new Bitmap(embedded);
            }

            string suffix = string.Join(".", parts).Replace('\\', '.').Replace('/', '.');
            string? fallbackResourceName = typeof(OverlayForm).Assembly
                .GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            if (fallbackResourceName is null)
                return null;

            resource = typeof(OverlayForm).Assembly.GetManifestResourceStream(fallbackResourceName);
            if (resource is null)
                return null;

            using Bitmap fallback = new(resource);
            return new Bitmap(fallback);
        }
        catch
        {
            return null;
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Frac(float v) => v - MathF.Floor(v);

    private static float Wave01(float v) => MathF.Sin(v) * 0.5f + 0.5f;

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float ValueNoise(float x, float y)
    {
        int xi = (int)MathF.Floor(x);
        int yi = (int)MathF.Floor(y);
        float xf = x - xi;
        float yf = y - yi;
        float u = xf * xf * (3f - 2f * xf);
        float v = yf * yf * (3f - 2f * yf);

        float a = Hash01(xi, yi);
        float b = Hash01(xi + 1, yi);
        float c = Hash01(xi, yi + 1);
        float d = Hash01(xi + 1, yi + 1);
        return Lerp(Lerp(a, b, u), Lerp(c, d, u), v);
    }

    private static float Hash01(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0x00FFFFFF) / 16777215f;
        }
    }

    private static int Hash(int value)
    {
        unchecked
        {
            uint x = (uint)value;
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return (int)x;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;

        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo
    {
        public uint Size;
        public uint Flags;
        public nint CursorHandle;
        public NativePoint ScreenPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Cx;
        public int Cy;

        public NativeSize(int cx, int cy)
        {
            Cx = cx;
            Cy = cy;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(nint hwnd, nint hdcDst, ref NativePoint pptDst, ref NativeSize psize, nint hdcSrc, ref NativePoint pptSrc, int crKey, ref BlendFunction pblend, int dwFlags);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hWnd, nint hDC);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint hDC);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint hdc, nint hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateDIBSection(nint hdc, ref BitmapInfo pbmi, uint usage, out nint ppvBits, nint hSection, uint dwOffset);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorInfo(ref CursorInfo cursorInfo);

    [DllImport("user32.dll")]
    private static extern int ShowCursor(bool bShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(nint hWnd, int dwAffinity);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);
}
