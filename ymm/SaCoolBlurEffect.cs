using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace SaCoolBlurYmm;

[VideoEffect("Sa_coolblur", ["ぼかし"], ["レンズ", "色収差", "玉ボケ"], IsAviUtlSupported = false)]
public sealed class SaCoolBlurEffect : VideoEffectBase
{
    public override string Label => "Sa_coolblur";
    [Display(Name = "半径", Order = 1)]
    [AnimationSlider("F2", "px", 0, 100)]
    public Animation Radius { get; } = new(20, 0, 100);

    [Display(Name = "分散量", Order = 2)]
    [AnimationSlider("F2", "%", -1000, 1000)]
    public Animation Dispersion { get; } = new(25, -1000, 1000);

    [Display(Name = "ボケの縁", Order = 3)]
    [AnimationSlider("F2", "%", -100, 100)]
    public Animation BokehEdge { get; } = new(0, -100, 100);

    [Display(Name = "ハイライト", Order = 4)]
    [AnimationSlider("F2", "%", 0, 100)]
    public Animation Highlight { get; } = new(30, 0, 100);

    [Display(Name = "しきい値", Order = 5)]
    [AnimationSlider("F2", "", 0.05, 1)]
    public Animation Threshold { get; } = new(0.8, 0.05, 1);

    [Display(Name = "縦横比", Order = 6)]
    [AnimationSlider("F2", "", 0.5, 2)]
    public Animation Anamorphic { get; } = new(1, 0.5, 2);

    [Display(Name = "中心X", Order = 7)]
    [AnimationSlider("F2", "%", -200, 300)]
    public Animation CenterX { get; } = new(50, -200, 300);

    [Display(Name = "中心Y", Order = 8)]
    [AnimationSlider("F2", "%", -200, 300)]
    public Animation CenterY { get; } = new(50, -200, 300);

    [Display(Name = "ピント幅", Order = 9)]
    [AnimationSlider("F2", "px", 0, 4000)]
    public Animation Width { get; } = new(200, 0, 4000);

    [Display(Name = "ピントの境界ぼかし", Order = 10)]
    [AnimationSlider("F2", "px", 0, 2000)]
    public Animation Feather { get; } = new(300, 0, 2000);

    [Display(Name = "角度", Order = 11)]
    [AnimationSlider("F2", "°", -360, 360)]
    public Animation Angle { get; } = new(0, -360, 360);

    [Display(Name = "焦点距離", Order = 12)]
    [AnimationSlider("F2", "", 0, 1)]
    public Animation FocusDistance { get; } = new(0.5, 0, 1);

    [Display(Name = "深度のピント幅", Order = 13)]
    [AnimationSlider("F2", "", 0, 1)]
    public Animation FocusRange { get; } = new(0.1, 0, 1);

    [Display(Name = "深度の境界ぼかし", Order = 14)]
    [AnimationSlider("F2", "", 0, 1)]
    public Animation DepthFeather { get; } = new(0.2, 0, 1);

    [Display(Name = "ピントの形")]
    [EnumComboBox]
    public FocusMode Mode { get => _Mode; set => Set(ref _Mode, value); }
    private FocusMode _Mode = FocusMode.Linear;

    [Display(Name = "画質")]
    [EnumComboBox]
    public BlurQuality Quality { get => _Quality; set => Set(ref _Quality, value); }
    private BlurQuality _Quality = BlurQuality.Balanced;

    [Display(Name = "ピントを反転")]
    [ToggleSlider]
    public bool Invert { get => _Invert; set => Set(ref _Invert, value); }
    private bool _Invert = false;

    [Display(Name = "ピントマップを表示")]
    [ToggleSlider]
    public bool ShowMap { get => _ShowMap; set => Set(ref _ShowMap, value); }
    private bool _ShowMap = false;

    [Display(Name = "端を引き延ばす")]
    [ToggleSlider]
    public bool EdgeRepeat { get => _EdgeRepeat; set => Set(ref _EdgeRepeat, value); }
    private bool _EdgeRepeat = true;

    [Display(Name = "リニアライト")]
    [ToggleSlider]
    public bool LinearLight { get => _LinearLight; set => Set(ref _LinearLight, value); }
    private bool _LinearLight = true;

    [Display(Name = "分散色1（内）")]
    [ColorPicker]
    public Color InnerColor { get => _InnerColor; set => Set(ref _InnerColor, value); }
    private Color _InnerColor = Colors.Red;

    [Display(Name = "分散色2（中）")]
    [ColorPicker]
    public Color MiddleColor { get => _MiddleColor; set => Set(ref _MiddleColor, value); }
    private Color _MiddleColor = Colors.Lime;

    [Display(Name = "分散色3（外）")]
    [ColorPicker]
    public Color OuterColor { get => _OuterColor; set => Set(ref _OuterColor, value); }
    private Color _OuterColor = Colors.Blue;

    [Display(Name = "深度マップ画像")]
    [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.ImageItem)]
    public string DepthFile { get => _DepthFile; set => Set(ref _DepthFile, value); }
    private string _DepthFile = "";

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];
    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices) => new SaCoolBlurProcessor(devices, this);
    protected override IEnumerable<IAnimatable> GetAnimatables() => [Radius, Dispersion, BokehEdge, Highlight, Threshold, Anamorphic, CenterX, CenterY, Width, Feather, Angle, FocusDistance, FocusRange, DepthFeather];
}
public enum FocusMode
{
    [Display(Name = "帯状")] Linear = 1,
    [Display(Name = "円形")] Radial = 2,
    [Display(Name = "深度マップ画像")] Depth = 3,
    [Display(Name = "全体")] Uniform = 4,
}
public enum BlurQuality
{
    [Display(Name = "高速（128）")] Fast = 128,
    [Display(Name = "標準（256）")] Balanced = 256,
    [Display(Name = "高品質（512）")] High = 512,
}
