using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace SaCoolBlurYmm;

internal sealed class SaCoolBlurProcessor : IVideoEffectProcessor
{
    private readonly IGraphicsDevicesAndContext _devices;
    private readonly SaCoolBlurEffect _item;
    private PrepareEffect? _prepare;
    private GatherEffect? _gather;
    private DownsampleEffect? _downsample;
    private GatherEffect? _lowGather;
    private CompositeEffect? _composite;
    private ID2D1Image? _output, _input;
    private ID2D1Bitmap1? _depth;
    private string _depthPath = "";
    private bool _disposed;

    public SaCoolBlurProcessor(IGraphicsDevicesAndContext devices, SaCoolBlurEffect item)
    {
        _devices = devices;
        _item = item;
        try
        {
            _prepare = new PrepareEffect(devices);
            _gather = new GatherEffect(devices);
            _downsample = new DownsampleEffect(devices);
            _lowGather = new GatherEffect(devices);
            _composite = new CompositeEffect(devices);
            if (!_prepare.IsEnabled || !_gather.IsEnabled || !_downsample.IsEnabled || !_lowGather.IsEnabled || !_composite.IsEnabled)
                throw new NotSupportedException("Sa_coolblur のGPUシェーダーを初期化できませんでした。");
            using var prepared = _prepare.Output;
            _gather.SetInput(0, prepared, true);
            _downsample.SetInput(0, prepared, true);
            using var downsampled = _downsample.Output;
            _lowGather.SetInput(0, downsampled, true);
            _lowGather.SetInput(1, downsampled, true);
            _lowGather.SetInput(2, downsampled, true);
            // The fourth gather input carries the full-resolution bounds for
            // the CUDA-equivalent low-resolution focus calculation.
            using var normal = _gather.Output;
            _composite.SetInput(0, normal, true);
            _output = _composite.Output;
        }
        catch { Dispose(); throw; }
    }

    public ID2D1Image Output => _output ?? _input ?? throw new InvalidOperationException("入力が未設定です。");
    public void SetInput(ID2D1Image? input)
    {
        _input = input;
        _prepare?.SetInput(0, input, true);
        _gather?.SetInput(2, input, true);
        _gather?.SetInput(1, _depth ?? input, true);
        _gather?.SetInput(3, input, true);
        _lowGather?.SetInput(3, input, true);
        _composite?.SetInput(1, input, true);
    }
    public void ClearInput()
    {
        _input = null;
        _prepare?.SetInput(0, null, true);
        _gather?.SetInput(1, null, true);
        _gather?.SetInput(2, null, true);
        _gather?.SetInput(3, null, true);
        _lowGather?.SetInput(3, null, true);
        _composite?.SetInput(1, null, true);
    }

    public DrawDescription Update(EffectDescription description)
    {
        if (_prepare is null || _gather is null || _downsample is null || _lowGather is null || _composite is null) return description.DrawDescription;
        var frame = description.ItemPosition.Frame;
        var length = description.ItemDuration.Frame;
        var fps = description.FPS;
        float Value(Animation a, float min, float max, float fallback)
        {
            var v = (float)a.GetValue(frame, length, fps);
            return float.IsFinite(v) ? Math.Clamp(v, min, max) : fallback;
        }
        float gamma = 1 + Value(_item.Highlight, 0, 100, 30) * .04f;
        float pivot = Value(_item.Threshold, .05f, 1, .8f);
        float linear = _item.LinearLight ? 1 : 0;
        float centerX = Value(_item.CenterX, -200, 300, 50);
        float centerY = Value(_item.CenterY, -200, 300, 50);
        float zone = Value(_item.Width, 0, 4000, 200);
        float feather = Value(_item.Feather, 0, 2000, 300);
        float angle = Value(_item.Angle, -360, 360, 0) * MathF.PI / 180;
        float mode = Enum.IsDefined(_item.Mode) ? (float)_item.Mode : 1;
        float radius = Value(_item.Radius, 0, 100, 20);
        // AE routes the focus-map diagnostic away from its GPU gather.  Keep
        // the full-resolution shader path here so the map remains a map.
        int downsample = _item.ShowMap
            ? 1
            : GetDownsampleFactor((FocusMode)(int)mode, radius, Enum.IsDefined(_item.Downsample) ? _item.Downsample : DownsampleMode.Auto);
        float dispersion = Value(_item.Dispersion, -1000, 1000, 25) * .003f;
        float edge = Value(_item.BokehEdge, -100, 100, 0) * .01f;
        float bokehStrength = Value(_item.BokehStrength, 0, 200, 0) * .01f;
        float anamorphic = Value(_item.Anamorphic, .5f, 2, 1);
        float distance = Value(_item.FocusDistance, 0, 1, .5f);
        float range = Value(_item.FocusRange, 0, 1, .1f);
        float depthFeather = Value(_item.DepthFeather, 0, 1, .2f);
        float samples = Enum.IsDefined(_item.Quality) ? (int)_item.Quality : 256;
        _prepare.Gamma = gamma; _prepare.Pivot = pivot; _prepare.Linear = linear;
        ConfigureGather(_gather, 1, gamma, pivot, linear, centerX, centerY, zone, feather, angle, mode, radius, dispersion, edge, anamorphic, distance, range, depthFeather, samples, bokehStrength, _item.ShowMap ? 1 : 0, _depth is null ? 0 : 1);
        ConfigureGather(_lowGather, downsample, gamma, pivot, linear, centerX, centerY, zone, feather, angle, mode, radius, dispersion, edge, anamorphic, distance, range, depthFeather, samples, bokehStrength, 0, 0);
        _downsample.Factor = downsample;
        _composite.CenterX = centerX; _composite.CenterY = centerY;
        _composite.Zone = zone; _composite.Feather = feather; _composite.Angle = angle;
        _composite.Mode = mode; _composite.Invert = _item.Invert ? 1 : 0;
        _composite.Factor = downsample; _composite.MaxRadius = radius;
        UpdateDepth(_item.Mode == FocusMode.Depth ? _item.DepthFile : "");
        _gather.HasDepth = _depth is null ? 0 : 1;
        using var blur = downsample > 1 ? _lowGather.Output : _gather.Output;
        _composite.SetInput(0, blur, true);
        return description.DrawDescription;
    }

    private void ConfigureGather(GatherEffect target, int scale, float gamma, float pivot, float linear,
        float centerX, float centerY, float zone, float feather, float angle, float mode, float radius,
        float dispersion, float edge, float anamorphic, float distance, float range, float depthFeather,
        float samples, float bokehStrength, float showMap, float hasDepth)
    {
        float inverse = 1f / Math.Max(scale, 1);
        target.Gamma = gamma; target.Pivot = pivot; target.Linear = linear;
        target.CenterX = centerX; target.CenterY = centerY;
        target.Zone = zone * inverse; target.Feather = feather * inverse;
        target.Angle = angle; target.Mode = mode; target.Invert = _item.Invert ? 1 : 0;
        target.ShowMap = showMap; target.Radius = radius * inverse;
        target.Dispersion = dispersion; target.Edge = edge; target.Anamorphic = anamorphic;
        target.Repeat = _item.EdgeRepeat ? 1 : 0;
        target.Distance = distance; target.Range = range; target.DepthFeather = depthFeather;
        target.HasDepth = hasDepth; target.Samples = samples; target.DownsampleScale = scale; target.BokehStrength = bokehStrength;
        target.InnerR = _item.InnerColor.R / 255f; target.InnerG = _item.InnerColor.G / 255f; target.InnerB = _item.InnerColor.B / 255f;
        target.MiddleR = _item.MiddleColor.R / 255f; target.MiddleG = _item.MiddleColor.G / 255f; target.MiddleB = _item.MiddleColor.B / 255f;
        target.OuterR = _item.OuterColor.R / 255f; target.OuterG = _item.OuterColor.G / 255f; target.OuterB = _item.OuterColor.B / 255f;
    }

    private static int GetDownsampleFactor(FocusMode mode, float radius, DownsampleMode setting)
    {
        // This is the exact selection policy in CoolBlur_LaunchCUDA.
        if (mode is not (FocusMode.Linear or FocusMode.Radial or FocusMode.Uniform)) return 1;
        int wanted = setting == DownsampleMode.Auto
            ? radius >= 96 ? 4 : radius >= 32 ? 2 : 1
            : Math.Clamp((int)setting, 1, 4);
        if (radius < 8) wanted = 1;
        while (wanted > 1 && radius / wanted < 4) --wanted;
        return Math.Max(wanted, 1);
    }

    private void UpdateDepth(string path)
    {
        if (path == _depthPath) return;
        ID2D1Bitmap1? next = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                // OnLoad releases the source file immediately. No file is held
                // open across frames and no bitmap is shared between processors.
                using var stream = File.OpenRead(path);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var bitmap = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Pbgra32, null, 0);
                int stride = checked(bitmap.PixelWidth * 4);
                var pixels = new byte[checked(stride * bitmap.PixelHeight)];
                bitmap.CopyPixels(pixels, stride, 0);
                var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                try
                {
                    next = _devices.DeviceContext.CreateBitmap(new SizeI(bitmap.PixelWidth, bitmap.PixelHeight),
                        handle.AddrOfPinnedObject(), stride,
                        new BitmapProperties1(new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), 96, 96));
                }
                finally { handle.Free(); }
            }
            _gather!.SetInput(1, next ?? _input, true);
            _depth?.Dispose();
            _depth = next;
            next = null;
            _depthPath = path;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or System.IO.FileFormatException or ArgumentException)
        {
            // Do not quietly substitute another image for a missing depth map.
            throw new InvalidOperationException($"深度マップ画像を読み込めません: {path}", ex);
        }
        finally { next?.Dispose(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClearInput();
        _output?.Dispose();
        _composite?.Dispose();
        _lowGather?.Dispose();
        _downsample?.Dispose();
        _gather?.Dispose();
        _prepare?.Dispose();
        _depth?.Dispose();
        _output = null; _composite = null; _lowGather = null; _downsample = null; _gather = null; _prepare = null; _depth = null;
    }
}
