using System.Collections.Immutable;
using Windows.UI;

namespace DataMan.Brand;

public readonly record struct BrandColor(byte R, byte G, byte B)
{
    public static BrandColor Parse(string hex)
    {
        if (hex is not { Length: 7 } || hex[0] != '#')
        {
            throw new FormatException($"Expected #RRGGBB, got '{hex}'.");
        }

        return new BrandColor(
            Convert.ToByte(hex[1..3], 16),
            Convert.ToByte(hex[3..5], 16),
            Convert.ToByte(hex[5..7], 16));
    }

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public Color ToWinUi() => Color.FromArgb(255, R, G, B);

    public BrandColor Mix(BrandColor other, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return new BrandColor(
            (byte)Math.Round(R + ((other.R - R) * t)),
            (byte)Math.Round(G + ((other.G - G) * t)),
            (byte)Math.Round(B + ((other.B - B) * t)));
    }
}

public sealed record BrandPalette(
    BrandColor Field,
    BrandColor AccentStart,
    BrandColor AccentMid,
    BrandColor AccentEnd,
    BrandColor Accent);

public enum BrandAssetKind
{
    AppIcon,
    InAppMark,
    InAppSvg,
    StoreLogo,
    Square44,
    Square44Unplated16,
    Square44Unplated24,
    Square44Unplated32,
    Square44Unplated48,
    Square44Unplated256,
    Square150,
    Wide310,
    Splash,
    LockScreen,
}

public enum BrandRasterMode
{
    OpaqueField,
    Transparent,
    Letterbox,
    CopySvg,
}

public sealed record BrandAsset(
    BrandAssetKind Kind,
    string RelativePath,
    int Width,
    int Height,
    BrandRasterMode Mode);

public sealed class BrandSpec
{
    public const string AccentBrushKey = "BrandAccentBrush";
    public const string AccentGradientBrushKey = "BrandAccentGradientBrush";
    public const string FieldBrushKey = "BrandFieldBrush";
    public const string MarkImagePath = "ms-appx:///Assets/Brand/Mark.png";

    public required string SvgPath { get; init; }

    public required BrandPalette Palette { get; init; }

    public required ImmutableArray<BrandAsset> Assets { get; init; }

    public static BrandSpec Primary { get; } = CreatePrimary();

    public BrandAsset AppIcon => Assets.Single(asset => asset.Kind == BrandAssetKind.AppIcon);

    public BrandAsset InAppMark => Assets.Single(asset => asset.Kind == BrandAssetKind.InAppMark);

    private static BrandSpec CreatePrimary()
    {
        var field = BrandColor.Parse("#06090E");
        var start = BrandColor.Parse("#3CE66A");
        var mid = BrandColor.Parse("#12E0C8");
        var end = BrandColor.Parse("#00D4FF");
        var spec = new BrandSpec
        {
            SvgPath = "DataMan (Package)/Images/DataMan_Logo.svg",
            Palette = new BrandPalette(field, start, mid, end, mid),
            Assets =
            [
                new BrandAsset(BrandAssetKind.AppIcon, "DataMan/Assets/Brand/DataMan.ico", 256, 256, BrandRasterMode.OpaqueField),
                new BrandAsset(BrandAssetKind.InAppMark, "DataMan/Assets/Brand/Mark.png", 128, 128, BrandRasterMode.OpaqueField),
                new BrandAsset(BrandAssetKind.InAppSvg, "DataMan/Assets/Brand/DataMan_Logo.svg", 107, 107, BrandRasterMode.CopySvg),
                new BrandAsset(BrandAssetKind.StoreLogo, "DataMan (Package)/Images/StoreLogo.png", 50, 50, BrandRasterMode.OpaqueField),
                new BrandAsset(BrandAssetKind.Square44, "DataMan (Package)/Images/Square44x44Logo.scale-200.png", 88, 88, BrandRasterMode.OpaqueField),
                new BrandAsset(BrandAssetKind.Square44Unplated16, "DataMan (Package)/Images/Square44x44Logo.targetsize-16_altform-unplated.png", 16, 16, BrandRasterMode.OpaqueField),
                new BrandAsset(BrandAssetKind.Square44Unplated24, "DataMan (Package)/Images/Square44x44Logo.targetsize-24_altform-unplated.png", 24, 24, BrandRasterMode.OpaqueField),
                new BrandAsset(BrandAssetKind.Square44Unplated32, "DataMan (Package)/Images/Square44x44Logo.targetsize-32_altform-unplated.png", 32, 32, BrandRasterMode.OpaqueField),
                new BrandAsset(BrandAssetKind.Square44Unplated48, "DataMan (Package)/Images/Square44x44Logo.targetsize-48_altform-unplated.png", 48, 48, BrandRasterMode.OpaqueField),
                new BrandAsset(BrandAssetKind.Square44Unplated256, "DataMan (Package)/Images/Square44x44Logo.targetsize-256_altform-unplated.png", 256, 256, BrandRasterMode.OpaqueField),
                new BrandAsset(BrandAssetKind.Square150, "DataMan (Package)/Images/Square150x150Logo.scale-200.png", 300, 300, BrandRasterMode.OpaqueField),
                new BrandAsset(BrandAssetKind.Wide310, "DataMan (Package)/Images/Wide310x150Logo.scale-200.png", 620, 300, BrandRasterMode.Letterbox),
                new BrandAsset(BrandAssetKind.Splash, "DataMan (Package)/Images/SplashScreen.scale-200.png", 1240, 600, BrandRasterMode.Letterbox),
                new BrandAsset(BrandAssetKind.LockScreen, "DataMan (Package)/Images/LockScreenLogo.scale-200.png", 48, 48, BrandRasterMode.OpaqueField),
            ],
        };

        if (spec.Assets.Count(asset => asset.Kind == BrandAssetKind.AppIcon) != 1
            || spec.Assets.Count(asset => asset.Kind == BrandAssetKind.InAppMark) != 1)
        {
            throw new InvalidOperationException("BrandSpec.Primary needs exactly one AppIcon and one InAppMark.");
        }

        return spec;
    }
}
