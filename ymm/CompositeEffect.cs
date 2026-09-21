using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace SaCoolBlurYmm;

/// <summary>
/// CUDAの低解像度パス末尾と同じく、低解像度ブラーを線形補間してから
/// フル解像度の焦点量で原画と合成する。
/// </summary>
internal sealed class CompositeEffect(IGraphicsDevicesAndContext devices)
    : D2D1CustomShaderEffectBase(Create<CompositeEffect.Impl>(devices))
{
    public float CenterX { set => SetValue(0, value); }
    public float CenterY { set => SetValue(1, value); }
    public float Zone { set => SetValue(2, value); }
    public float Feather { set => SetValue(3, value); }
    public float Angle { set => SetValue(4, value); }
    public float Mode { set => SetValue(5, value); }
    public float Invert { set => SetValue(6, value); }
    public float Factor { set => SetValue(7, value); }

    [CustomEffect(2)]
    private sealed class Impl : D2D1CustomShaderEffectImplBase<Impl>
    {
        private Constants _c;
        private RawRect _lowBounds, _rawBounds;

        public Impl() : base(ShaderResourceLoader.Get("Composite")) { }

        [CustomEffectProperty(PropertyType.Float, 0)] public float CenterX { get => _c.CenterX; set { _c.CenterX = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 1)] public float CenterY { get => _c.CenterY; set { _c.CenterY = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 2)] public float Zone { get => _c.Zone; set { _c.Zone = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 3)] public float Feather { get => _c.Feather; set { _c.Feather = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 4)] public float Angle { get => _c.Angle; set { _c.Angle = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 5)] public float Mode { get => _c.Mode; set { _c.Mode = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 6)] public float Invert { get => _c.Invert; set { _c.Invert = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 7)] public float Factor { get => _c.Factor; set { _c.Factor = Math.Clamp(float.IsFinite(value) ? value : 1, 1, 4); UpdateConstants(); } }

        protected override void UpdateConstants()
        {
            drawInformation?.SetOutputBuffer(BufferPrecision.PerChannel32Float, ChannelDepth.Four);
            drawInformation?.SetInputDescription(0, new InputDescription { Filter = Filter.MinMagMipLinear, LevelOfDetailCount = 1 });
            drawInformation?.SetInputDescription(1, new InputDescription { Filter = Filter.MinMagMipPoint, LevelOfDetailCount = 1 });
            drawInformation?.SetPixelShaderConstantBuffer(_c);
        }

        public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
        {
            _lowBounds = inputRects[0];
            _rawBounds = inputRects[1];
            outputRect = _rawBounds;
            outputOpaqueSubRect = default;
            _c.LowLeft = _lowBounds.Left; _c.LowTop = _lowBounds.Top;
            _c.LowRight = _lowBounds.Right; _c.LowBottom = _lowBounds.Bottom;
            _c.RawLeft = _rawBounds.Left; _c.RawTop = _rawBounds.Top;
            _c.RawRight = _rawBounds.Right; _c.RawBottom = _rawBounds.Bottom;
            UpdateConstants();
        }

        public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
        {
            inputRects[0] = _lowBounds.Right > _lowBounds.Left && _lowBounds.Bottom > _lowBounds.Top ? _lowBounds : outputRect;
            inputRects[1] = outputRect;
        }

        public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect) => _rawBounds;

        [StructLayout(LayoutKind.Sequential)]
        private struct Constants
        {
            public float CenterX, CenterY, Zone, Feather;
            public float Angle, Mode, Invert, Factor;
            public float LowLeft, LowTop, LowRight, LowBottom;
            public float RawLeft, RawTop, RawRight, RawBottom;
        }
    }
}
