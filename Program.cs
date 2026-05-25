using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

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
        FullTrip = 4
    }

    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int GWL_EXSTYLE = -20;
    private const int WM_HOTKEY = 0x0312;
    private const int ULW_ALPHA = 0x02;
    private const int BI_RGB = 0;
    private const uint DIB_RGB_COLORS = 0;
    private const byte AC_SRC_OVER = 0;
    private const byte AC_SRC_ALPHA = 1;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_NOREPEAT = 0x4000;
    private const float NilkCycleSeconds = 3600f;
    private const float NilkDatamoshIntensity = 0.51f;

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

    private static readonly char[] GlyphBank =
        "░▒▓█▓▒░ ᚠᚢᚦᚨᚱᚲ ΨΩΔΛΣΞ ЖЙФЮЯ 目電幻夢零壱弐参 NILK VOID COSMOS LSD 0123456789 @#$%&*<>/\\".ToCharArray();

    private readonly System.Windows.Forms.Timer timer;
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
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
    private float intensity = 0.28f;
    private float targetIntensity = 0.28f;
    private float surge;
    private float hintSeconds = 7f;
    private bool paused;
    private bool needsFrame = true;

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
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

        // 33 ms keeps the transparent layered window close to 30 FPS without choking Terraria.
        timer = new System.Windows.Forms.Timer { Interval = 33 };
        timer.Tick += (_, _) => RenderTick();
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
        RegisterOverlayHotkeys();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        CreateRenderSurface(Math.Max(1, Width), Math.Max(1, Height));
        timer.Start();
        RenderTick();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Stop();
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
                    targetIntensity = Math.Clamp(targetIntensity + 0.06f, 0f, 0.72f);
                    ShowHint();
                    break;
                case HotkeyIntensityDown:
                case HotkeyIntensityDownF:
                    targetIntensity = Math.Clamp(targetIntensity - 0.06f, 0f, 0.72f);
                    ShowHint();
                    break;
                case HotkeyTogglePause:
                case HotkeyTogglePauseF:
                    paused = !paused;
                    ShowHint();
                    break;
                case HotkeyExit:
                case HotkeyExitF:
                    Close();
                    return;
            }
        }

        base.WndProc(ref m);
    }

    private void ShowHint()
    {
        hintSeconds = 2.6f;
        needsFrame = true;
    }

    private void UpdateTimerInterval()
    {
        timer.Interval = 33;
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
        if (intensity > 0.004f)
        {
            float p = LsdPower;
            RenderFullTripLowRes(g, w, h, p);
        }

        DrawHint(g, w, h);
    }

    private void RenderFullTripLowRes(Graphics target, int w, int h, float p)
    {
        int lowW = Math.Clamp(w / 4, 320, 480);
        int lowH = Math.Max(180, (int)MathF.Round(lowW * h / Math.Max(w, 1f)));
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

    private void DrawNilkShaderApproximation(Graphics g, int w, int h, float p)
    {
        DrawFullScreenIridescence(g, w, h, p * 1.05f);
        DrawTextureLayer(g, cosmosTexture, w, h, 0.016f, 1.24f, 0.060f * p, 0.00f);
        DrawTextureLayer(g, psychedelicTexture, w, h, -0.022f, 1.08f, 0.105f * p, 0.27f);
        DrawTextureLayer(g, voidTexture, w, h, 0.013f, 1.42f, 0.070f * p, 0.52f);
        DrawTextureLayer(g, noiseTexture, w, h, -0.035f, 0.86f, 0.028f * p, 0.78f);
        DrawSoftVignette(g, w, h, p * 0.95f);
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
            float effective = Math.Clamp(opacity * smoothOpacity, 0f, 1f);
            float overlayPower = Math.Clamp(opacity * 1.18f, 0f, 1f);
            float offsetTime = time * 0.7f;
            float progress = Frac(time / NilkCycleSeconds);
            float minute = progress * 60f;
            float cyanPhase = Saturate(
                Bell(minute, 8.7f, 2.35f) +
                Bell(minute, 15.4f, 2.85f) * 0.95f +
                Bell(minute, 56.8f, 2.80f) * 0.90f);
            float palePhase = Saturate(
                Bell(minute, 16.4f, 2.20f) * 0.70f +
                Bell(minute, 57.6f, 1.70f) * 0.55f);
            float voidPhase = Saturate(TimeWindow(minute, 20.0f, 43.5f, 3.2f));
            float redPhase = Saturate(
                BellWrapped(minute, 59.25f, 60f, 1.85f) +
                TimeWindow(minute, 58.2f, 60.0f, 1.0f) * 0.55f);

            float centerX = 0.5f + MathF.Sin(time * 0.031f) * 0.045f * overlayPower;
            float centerY = 0.5f + MathF.Cos(time * 0.027f) * 0.040f * overlayPower;
            float invW = 1f / Math.Max(w - 1, 1);
            float invH = 1f / Math.Max(h - 1, 1);
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

                    float du = u + MathF.Cos(offsetTime + v * MathF.Tau) * effective * 0.05f;
                    float dv = v + MathF.Cos(offsetTime + du * MathF.Tau) * effective * 0.05f;
                    dv += (MathF.Sin(du * 300f - dv * 32f + time * 20f) * 0.004f +
                           MathF.Sin(du * 20f + dv * 105f + time * 10f) * 0.003f) * effective;

                    float cx = du - centerX;
                    float cy = dv - centerY;
                    float dist = MathF.Sqrt(cx * cx + cy * cy);
                    float angle = MathF.Atan2(cy, cx);
                    float vortex = overlayPower * (0.26f + cyanPhase * 0.16f + redPhase * 0.28f) / MathF.Max(0.075f, dist + 0.05f);
                    angle += time * (0.18f + 0.34f * overlayPower) + vortex;
                    angle += MathF.Sin(dist * 18f - time * 1.35f) * 0.10f * overlayPower;
                    float stretch = 1f + MathF.Sin(angle * 2.0f + time * 0.42f) * 0.065f * overlayPower;
                    float swirlU = centerX + MathF.Cos(angle) * dist * stretch;
                    float swirlV = centerY + MathF.Sin(angle) * dist / MathF.Max(0.62f, stretch);

                    float tex1 = SampleNoise(noise, nw, nh, ns, swirlU * 0.82f + time * 0.006f, swirlV * 0.82f - time * 0.004f);
                    float tex2 = SampleNoise(noise, nw, nh, ns, swirlU * 1.72f - time * 0.010f + tex1 * 0.15f, swirlV * 1.45f + time * 0.008f);
                    float n1 = ValueNoise(swirlU * 3.3f + tex1 * 1.7f + time * 0.045f, swirlV * 2.7f - time * 0.035f);
                    float n2 = ValueNoise(swirlU * 9.2f - time * 0.115f + n1 * 2.2f, swirlV * 7.1f + time * 0.092f + tex2);
                    float n3 = ValueNoise(swirlU * 22.0f + n2 * 3.1f + time * 0.19f, swirlV * 18.0f - time * 0.16f);
                    float liquidWave = MathF.Sin((swirlU * 1.55f + swirlV * 0.95f + n2 * 1.85f) * MathF.Tau + time * 0.31f);
                    float angularWave = MathF.Sin(angle * 2.7f + dist * 24f - time * 0.62f);
                    float luminosity = Math.Clamp(tex1 * 0.25f + tex2 * 0.19f + n1 * 0.24f + n2 * 0.22f + n3 * 0.10f +
                                                  liquidWave * 0.085f + angularWave * 0.055f, 0f, 1f);

                    float paletteT = MathF.Sin(luminosity * MathF.Tau - time * 1.5f) * 0.5f + 0.5f;
                    Color evil = PaletteColor(paletteT + cyanPhase * 0.045f - redPhase * 0.11f);
                    float contour = 1f - SmoothStep(0.015f, 0.115f, MathF.Abs(luminosity - (0.50f + 0.08f * MathF.Sin(time * 0.22f + tex2))));
                    Color voidColor = LerpColor(Color.FromArgb(7, 5, 21), Color.FromArgb(39, 20, 62), SmoothStep(0.18f, 0.90f, luminosity));
                    Color cyanColor = LerpColor(Color.FromArgb(123, 210, 223), Color.FromArgb(250, 152, 226), SmoothStep(0.18f, 0.82f, luminosity + tex2 * 0.12f));
                    cyanColor = LerpColor(cyanColor, Color.FromArgb(236, 248, 255), (palePhase * 0.52f + SmoothStep(0.78f, 0.98f, luminosity) * 0.24f) * cyanPhase);
                    Color redBase = LerpColor(Color.FromArgb(4, 0, 0), Color.FromArgb(175, 77, 8), SmoothStep(0.18f, 0.64f, luminosity));
                    Color toxic = LerpColor(Color.FromArgb(236, 0, 211), Color.FromArgb(17, 238, 45), SmoothStep(0.36f, 0.78f, n3 + tex2 * 0.18f));
                    Color redColor = LerpColor(redBase, toxic, Math.Clamp(contour * 0.86f + SmoothStep(0.74f, 0.96f, luminosity) * 0.45f, 0f, 1f));

                    evil = LerpColor(evil, voidColor, voidPhase * 0.48f);
                    evil = LerpColor(evil, cyanColor, cyanPhase);
                    evil = LerpColor(evil, LerpColor(cyanColor, Color.White, 0.28f), palePhase * 0.50f);
                    evil = LerpColor(evil, redColor, redPhase);
                    evil = LerpColor(evil, Color.Black, Math.Clamp(dist * (0.28f + redPhase * 0.26f + voidPhase * 0.10f), 0f, 0.42f));

                    int index = row + x * 4;
                    int sx = Math.Clamp((int)((du + (n2 - 0.5f) * 0.052f * overlayPower + MathF.Cos(angle) * redPhase * 0.018f) * (w - 1)), 0, w - 1);
                    int sy = Math.Clamp((int)((dv + (n1 - 0.5f) * 0.044f * overlayPower + MathF.Sin(angle) * cyanPhase * 0.014f) * (h - 1)), 0, h - 1);
                    int previousIndex = (stride > 0 ? sy * stride : (h - 1 - sy) * -stride) + sx * 4;
                    float previousAlpha = previous[previousIndex + 3] / 255f;
                    float previousR = previousAlpha > 0.001f ? Math.Clamp(previous[previousIndex + 2] / previousAlpha, 0f, 255f) : previous[previousIndex + 2];
                    float previousG = previousAlpha > 0.001f ? Math.Clamp(previous[previousIndex + 1] / previousAlpha, 0f, 255f) : previous[previousIndex + 1];
                    float previousB = previousAlpha > 0.001f ? Math.Clamp(previous[previousIndex + 0] / previousAlpha, 0f, 255f) : previous[previousIndex + 0];
                    float blendNoise = SampleNoise(noise, nw, nh, ns, u * 1.4f + previousR * 0.0039f, v * 0.9f + previousB * 0.0039f) +
                                       SampleNoise(noise, nw, nh, ns, u * 0.9f + previousB * 0.0039f, v * 1.4f + previousG * 0.0039f);
                    float datamosh = SmoothStep(1f - NilkDatamoshIntensity, 1f, blendNoise * 0.5f) *
                                     MathF.Pow(overlayPower, 2.1f) *
                                     (0.55f + cyanPhase * 0.24f + voidPhase * 0.18f + redPhase * 0.45f);

                    if (datamosh > 0.001f)
                    {
                        evil = Color.FromArgb(255,
                            (int)Lerp(evil.R, previousR, datamosh),
                            (int)Lerp(evil.G, previousG, datamosh),
                            (int)Lerp(evil.B, previousB, datamosh));
                    }

                    float vignette = Math.Clamp(0.70f + (1f - dist * 0.95f) * 0.30f, 0.48f, 1f);
                    float blotch = 0.62f + 0.38f * SmoothStep(0.22f, 0.86f, luminosity + tex2 * 0.10f);
                    float phaseAlpha = 1f + cyanPhase * 0.42f + palePhase * 0.20f + voidPhase * 0.18f + redPhase * 0.48f;
                    float alphaF = ((0.050f + 0.385f * overlayPower) * phaseAlpha * blotch + contour * 0.105f * overlayPower) * vignette;
                    alphaF += datamosh * 0.070f + palePhase * 0.055f * overlayPower;
                    alphaF = Math.Clamp(alphaF, 0f, 0.78f);

                    target[index + 0] = (byte)(evil.B * alphaF);
                    target[index + 1] = (byte)(evil.G * alphaF);
                    target[index + 2] = (byte)(evil.R * alphaF);
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
        string state = paused ? "PAUSED" : $"NILK | INT {(int)MathF.Round(targetIntensity * 100f)}%";
        string text = $"F5/F6 power | F7 pause | F12 exit | {state}";

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
        uint fmods = MOD_NOREPEAT;

        // Old safe combos still work.
        RegisterHotKey(Handle, HotkeyIntensityUp, mods, (uint)Keys.Up);
        RegisterHotKey(Handle, HotkeyIntensityDown, mods, (uint)Keys.Down);
        RegisterHotKey(Handle, HotkeyTogglePause, mods, (uint)Keys.Space);
        RegisterHotKey(Handle, HotkeyExit, mods, (uint)Keys.Escape);
        // Quick reliable keys, because click-through overlays do not receive normal keyboard focus.
        RegisterHotKey(Handle, HotkeyIntensityDownF, fmods, (uint)Keys.F5);
        RegisterHotKey(Handle, HotkeyIntensityUpF, fmods, (uint)Keys.F6);
        RegisterHotKey(Handle, HotkeyTogglePauseF, fmods, (uint)Keys.F7);
        RegisterHotKey(Handle, HotkeyExitF, fmods, (uint)Keys.F12);
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
    }

    private void MakeClickThrough()
    {
        int style = GetWindowLong(Handle, GWL_EXSTYLE);
        SetWindowLong(Handle, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
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
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);
}
