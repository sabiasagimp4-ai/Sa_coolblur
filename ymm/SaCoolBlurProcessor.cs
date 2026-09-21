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
            if (!_prepare.IsEnabled || !_gather.IsEnabled)
                throw new NotSupportedException("Sa_coolblur のGPUシェーダーを初期化できませんでした。");
            using var prepared = _prepare.Output;
            _gather.SetInput(0, prepared, true);
            _output = _gather.Output;
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
    }
    public void ClearInput()
    {
        _input = null;
        _prepare?.SetInput(0, null, true);
        _gather?.SetInput(1, null, true);
        _gather?.SetInput(2, null, true);
    }

    public DrawDescription Update(EffectDescription description)
    {
        if (_prepare is null || _gather is null) return description.DrawDescription;
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
        _prepare.Gamma = gamma; _prepare.Pivot = pivot; _prepare.Linear = linear;
        _gather.Gamma = gamma; _gather.Pivot = pivot; _gather.Linear = linear;
        _gather.CenterX = Value(_item.CenterX, -200, 300, 50);
        _gather.CenterY = Value(_item.CenterY, -200, 300, 50);
        _gather.Zone = Value(_item.Width, 0, 4000, 200);
        _gather.Feather = Value(_item.Feather, 0, 2000, 300);
        _gather.Angle = Value(_item.Angle, -360, 360, 0) * MathF.PI / 180;
        _gather.Mode = Enum.IsDefined(_item.Mode) ? (float)_item.Mode : 1;
        _gather.Invert = _item.Invert ? 1 : 0;
        _gather.ShowMap = _item.ShowMap ? 1 : 0;
        _gather.Radius = Value(_item.Radius, 0, 100, 20);
        // AE's displayed dispersion uses a 0.003 multiplier, not /100.
        _gather.Dispersion = Value(_item.Dispersion, -1000, 1000, 25) * .003f;
        _gather.Edge = Value(_item.BokehEdge, -100, 100, 0) * .01f;
        _gather.Anamorphic = Value(_item.Anamorphic, .5f, 2, 1);
        _gather.Repeat = _item.EdgeRepeat ? 1 : 0;
        _gather.Distance = Value(_item.FocusDistance, 0, 1, .5f);
        _gather.Range = Value(_item.FocusRange, 0, 1, .1f);
        _gather.DepthFeather = Value(_item.DepthFeather, 0, 1, .2f);
        _gather.Samples = Enum.IsDefined(_item.Quality) ? (int)_item.Quality : 256;
        _gather.InnerR = _item.InnerColor.R / 255f; _gather.InnerG = _item.InnerColor.G / 255f; _gather.InnerB = _item.InnerColor.B / 255f;
        _gather.MiddleR = _item.MiddleColor.R / 255f; _gather.MiddleG = _item.MiddleColor.G / 255f; _gather.MiddleB = _item.MiddleColor.B / 255f;
        _gather.OuterR = _item.OuterColor.R / 255f; _gather.OuterG = _item.OuterColor.G / 255f; _gather.OuterB = _item.OuterColor.B / 255f;
        UpdateDepth(_item.Mode == FocusMode.Depth ? _item.DepthFile : "");
        _gather.HasDepth = _depth is null ? 0 : 1;
        return description.DrawDescription;
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
                        handle.AddrOfPinnedObject(), (uint)stride,
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
        _gather?.SetInput(0, null, true);
        _output?.Dispose();
        _gather?.Dispose();
        _prepare?.Dispose();
        _depth?.Dispose();
        _output = null; _gather = null; _prepare = null; _depth = null;
    }
}
