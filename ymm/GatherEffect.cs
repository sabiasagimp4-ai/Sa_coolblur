using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
namespace SaCoolBlurYmm;

internal sealed class GatherEffect(IGraphicsDevicesAndContext devices)
    : D2D1CustomShaderEffectBase(Create<GatherEffect.Impl>(devices))
{
    public float CenterX { set => SetValue(0, value); }
    public float CenterY { set => SetValue(1, value); }
    public float Zone { set => SetValue(2, value); }
    public float Feather { set => SetValue(3, value); }
    public float Angle { set => SetValue(4, value); }
    public float Mode { set => SetValue(5, value); }
    public float Invert { set => SetValue(6, value); }
    public float ShowMap { set => SetValue(7, value); }
    public float Radius { set => SetValue(8, value); }
    public float Dispersion { set => SetValue(9, value); }
    public float Edge { set => SetValue(10, value); }
    public float Anamorphic { set => SetValue(11, value); }
    public float Gamma { set => SetValue(12, value); }
    public float Pivot { set => SetValue(13, value); }
    public float Linear { set => SetValue(14, value); }
    public float Repeat { set => SetValue(15, value); }
    public float Distance { set => SetValue(16, value); }
    public float Range { set => SetValue(17, value); }
    public float DepthFeather { set => SetValue(18, value); }
    public float HasDepth { set => SetValue(19, value); }
    public float Samples { set => SetValue(20, value); }
    public float Reserved0 { set => SetValue(21, value); }
    public float Reserved1 { set => SetValue(22, value); }
    public float Reserved2 { set => SetValue(23, value); }
    public float InnerR { set => SetValue(24, value); }
    public float InnerG { set => SetValue(25, value); }
    public float InnerB { set => SetValue(26, value); }
    public float Reserved3 { set => SetValue(27, value); }
    public float MiddleR { set => SetValue(28, value); }
    public float MiddleG { set => SetValue(29, value); }
    public float MiddleB { set => SetValue(30, value); }
    public float Reserved4 { set => SetValue(31, value); }
    public float OuterR { set => SetValue(32, value); }
    public float OuterG { set => SetValue(33, value); }
    public float OuterB { set => SetValue(34, value); }
    public float Reserved5 { set => SetValue(35, value); }
    [CustomEffect(3)]
    private sealed class Impl : D2D1CustomShaderEffectImplBase<Impl>
    {
        private Constants _c;
        public Impl() : base(ShaderResourceLoader.Get("Gather")) { }
        [CustomEffectProperty(PropertyType.Float, 0)] public float CenterX { get => _c.CenterX; set { _c.CenterX = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 1)] public float CenterY { get => _c.CenterY; set { _c.CenterY = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 2)] public float Zone { get => _c.Zone; set { _c.Zone = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 3)] public float Feather { get => _c.Feather; set { _c.Feather = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 4)] public float Angle { get => _c.Angle; set { _c.Angle = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 5)] public float Mode { get => _c.Mode; set { _c.Mode = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 6)] public float Invert { get => _c.Invert; set { _c.Invert = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 7)] public float ShowMap { get => _c.ShowMap; set { _c.ShowMap = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 8)] public float Radius { get => _c.Radius; set { _c.Radius = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 9)] public float Dispersion { get => _c.Dispersion; set { _c.Dispersion = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 10)] public float Edge { get => _c.Edge; set { _c.Edge = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 11)] public float Anamorphic { get => _c.Anamorphic; set { _c.Anamorphic = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 12)] public float Gamma { get => _c.Gamma; set { _c.Gamma = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 13)] public float Pivot { get => _c.Pivot; set { _c.Pivot = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 14)] public float Linear { get => _c.Linear; set { _c.Linear = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 15)] public float Repeat { get => _c.Repeat; set { _c.Repeat = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 16)] public float Distance { get => _c.Distance; set { _c.Distance = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 17)] public float Range { get => _c.Range; set { _c.Range = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 18)] public float DepthFeather { get => _c.DepthFeather; set { _c.DepthFeather = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 19)] public float HasDepth { get => _c.HasDepth; set { _c.HasDepth = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 20)] public float Samples { get => _c.Samples; set { _c.Samples = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 21)] public float Reserved0 { get => _c.Reserved0; set { _c.Reserved0 = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 22)] public float Reserved1 { get => _c.Reserved1; set { _c.Reserved1 = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 23)] public float Reserved2 { get => _c.Reserved2; set { _c.Reserved2 = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 24)] public float InnerR { get => _c.InnerR; set { _c.InnerR = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 25)] public float InnerG { get => _c.InnerG; set { _c.InnerG = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 26)] public float InnerB { get => _c.InnerB; set { _c.InnerB = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 27)] public float Reserved3 { get => _c.Reserved3; set { _c.Reserved3 = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 28)] public float MiddleR { get => _c.MiddleR; set { _c.MiddleR = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 29)] public float MiddleG { get => _c.MiddleG; set { _c.MiddleG = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 30)] public float MiddleB { get => _c.MiddleB; set { _c.MiddleB = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 31)] public float Reserved4 { get => _c.Reserved4; set { _c.Reserved4 = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 32)] public float OuterR { get => _c.OuterR; set { _c.OuterR = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 33)] public float OuterG { get => _c.OuterG; set { _c.OuterG = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 34)] public float OuterB { get => _c.OuterB; set { _c.OuterB = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 35)] public float Reserved5 { get => _c.Reserved5; set { _c.Reserved5 = value; UpdateConstants(); } }
        protected override void UpdateConstants()
        {
            drawInformation?.SetOutputBuffer(BufferPrecision.PerChannel32Float, ChannelDepth.Four);
            drawInformation?.SetInputDescription(0, new InputDescription { Filter = Filter.MinMagMipLinear, LevelOfDetailCount = 1 });
            drawInformation?.SetPixelShaderConstantBuffer(_c);
        }
        public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
        {
            // Request the complete source: clamped sampling at large radii may
            // reach the opposite edge even when rendering a small output tile.
            inputRects[0] = _bounds;
            inputRects[1] = _depthBounds;
            inputRects[2] = outputRect;
        }
        private RawRect _bounds, _depthBounds;
        public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
        {
            _bounds = inputRects[2];
            _depthBounds = inputRects[1];
            outputRect = _bounds;
            outputOpaqueSubRect = default;
            _c.Left = _bounds.Left; _c.Top = _bounds.Top;
            _c.Right = _bounds.Right; _c.Bottom = _bounds.Bottom;
            _c.DepthLeft = _depthBounds.Left; _c.DepthTop = _depthBounds.Top;
            _c.DepthRight = _depthBounds.Right; _c.DepthBottom = _depthBounds.Bottom;
            UpdateConstants();
        }
        public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect) => _bounds;
        [StructLayout(LayoutKind.Sequential)]
        private struct Constants
        {
            public float CenterX, CenterY, Zone, Feather, Angle, Mode, Invert, ShowMap, Radius, Dispersion, Edge, Anamorphic, Gamma, Pivot, Linear, Repeat, Distance, Range, DepthFeather, HasDepth, Samples, Reserved0, Reserved1, Reserved2, InnerR, InnerG, InnerB, Reserved3, MiddleR, MiddleG, MiddleB, Reserved4, OuterR, OuterG, OuterB, Reserved5;
            public float Left, Top, Right, Bottom, DepthLeft, DepthTop, DepthRight, DepthBottom;
        }
    }
}
