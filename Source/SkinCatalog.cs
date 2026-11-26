using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ScreenGlass;

internal sealed record JewelSkin(string Id, string Name, string Accent, string Ink, string Muted,
    string Glass, string[] Gem, string[] LightLens, string[] DeepLens, string[] Metal);

internal static class SkinCatalog
{
    public static IReadOnlyList<JewelSkin> All { get; } = new[]
    {
        new JewelSkin("jade", "翡翠", "#328F73", "#183E36", "#607B72", "#D9EDE4",
            new[] { "#C0F8DF", "#328F73", "#0B503F", "#6DBE97" },
            new[] { "#9AE5C1", "#57BBA6", "#388D92" }, new[] { "#259889", "#124E65", "#181F46" },
            new[] { "#FFFADE", "#B89350", "#F2DCA0", "#916A36", "#F5DFAC" }),
        new JewelSkin("sapphire", "蓝宝石", "#427AA8", "#17324F", "#68788E", "#D7E6F4",
            new[] { "#C6E9FF", "#427AA8", "#183957", "#80B6D1" },
            new[] { "#B5DFF5", "#6CA9D5", "#657DAA" }, new[] { "#37668D", "#203F72", "#111F46" },
            new[] { "#F3FAFF", "#85A2B7", "#CFDFEB", "#637889", "#E1EDF7" }),
        new JewelSkin("rose", "玫瑰碧玺", "#B9688B", "#4D2B44", "#8B6E80", "#F0DDE8",
            new[] { "#FFDCE9", "#B9688B", "#662D52", "#D997BA" },
            new[] { "#F1CBDC", "#BE8CB4", "#95699F" }, new[] { "#B46292", "#6E3D76", "#342548" },
            new[] { "#FFF1E1", "#B97D78", "#F6CDBA", "#915F62", "#F2C9BE" }),
        new JewelSkin("amber", "香槟琥珀", "#B88843", "#4A3822", "#89785E", "#F1E7CF",
            new[] { "#FFF0BC", "#C99A4D", "#76542C", "#DEB773" },
            new[] { "#FAE1AC", "#D6AB72", "#AB8769" }, new[] { "#AA7A3F", "#745333", "#3C3042" },
            new[] { "#FFFADE", "#B89350", "#F2DCA0", "#916A36", "#F5DFAC" })
    };
    public static JewelSkin Get(string id)
    {
        foreach (var skin in All) if (skin.Id == id) return skin;
        throw new ArgumentOutOfRangeException(nameof(id));
    }
    public static bool Contains(string id)
    {
        foreach (var skin in All) if (skin.Id == id) return true;
        return false;
    }
    public static void Apply(string id)
    {
        var skin = Get(id);
        var resources = Application.Current.Resources;
        resources["Ink"] = Brush(skin.Ink);
        resources["Muted"] = Brush(skin.Muted);
        resources["Green"] = Brush(skin.Accent);
        resources["SelectedSurface"] = Brush(skin.Accent, 0.24);
        resources["SelectedStroke"] = Brush(skin.Accent, 0.85);
        resources["GlassSurface"] = Gradient(new[] { "#FFFFFF", skin.Glass, skin.Glass }, new[] { 0d, 0.55, 1 }, new[] { 0.66, 0.22, 0.40 });
        resources["GlassCard"] = Brush(skin.Glass, 0.40);
        resources["MenuSurface"] = Brush(skin.Glass);
        resources["AccentColor"] = Parse(skin.Accent);
        var glow = Parse(skin.Accent); glow.A = 90;
        resources["AccentHaloColor"] = glow;
        resources["FacetLight"] = Brush(skin.Gem[0], 0.5);
        resources["FacetSide"] = Brush(skin.Accent, 0.28);
        resources["FacetDepth"] = Brush(skin.DeepLens[2], 0.36);
        resources["FacetLine"] = Brush(skin.Gem[0], 0.48);
        resources["GemRim"] = Brush(skin.Metal[2], 0.9);
        resources["GemFill"] = Gradient(skin.Gem, new[] { 0d, 0.38, 0.72, 1 });
        resources["MetalFill"] = Gradient(skin.Metal, new[] { 0d, 0.28, 0.52, 0.78, 1 });
        resources["ActiveSkinId"] = skin.Id;
    }
    private static SolidColorBrush Brush(string color, double opacity = 1) => new(Parse(color)) { Opacity = opacity };
    private static LinearGradientBrush Gradient(string[] colors, double[] offsets, double[]? opacity = null)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        for (int i = 0; i < colors.Length; i++)
        {
            var color = Parse(colors[i]);
            if (opacity != null) color.A = (byte)(255 * opacity[i]);
            brush.GradientStops.Add(new GradientStop(color, offsets[i]));
        }
        return brush;
    }
    internal static Color Parse(string color) => (Color)ColorConverter.ConvertFromString(color);
}
