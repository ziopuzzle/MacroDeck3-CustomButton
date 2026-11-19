using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Model.References;

namespace Ziopuzzle.CustomButton;

public sealed partial class LayoutRenderer
{
    private sealed partial class ComponentRenderer
    {
        private UiElement BuildDynamicText()
        {
            return new UiDynamicText { Key = key, Fill = fill, MainSize = mainSize,
                Value = UiTimeReference.InZone(Attribute("zone") is "—" ? null : Attribute("zone")), Format = Choice("format", "time"), Seconds = Boolean("seconds", false),
                Size = TextLength(textSize), MinSize = TextLength(minTextSize), Weight = Choice("weight", "regular"), Align = Choice("align", "start"),
                Role = Choice("role", "primary"), Color = attributes.ContainsKey("color") ? UiValue.Of(Color("color", "#ffffff")) : default };
        }

        private UiElement BuildIcon()
        {
            var iconSize = Length("size", .2);
            var icon = new UiIcon { Key = key, Fill = Boolean("fill", false), MainSize = mainSize, Icon = Choice("name", "star"),
                Size = iconSize,
                Role = Choice("role", "primary"), Color = attributes.ContainsKey("color") ? UiValue.Of(Color("color", "#ffffff")) : default };
            // The host centres glyphs in their allocated box. Give a natural-size icon a
            // square box so cross-axis stretching cannot introduce invisible vertical space.
            if (Boolean("fill", false) || attributes.ContainsKey("mainSize") && Attribute("mainSize") != "auto") return icon;
            return new UiModifier { Key = key, Fill = false,
                Frame = new UiFrame { Width = UiLength.OfBasis(iconSize), Height = UiLength.OfBasis(iconSize) },
                Child = icon with { Key = "glyph", Fill = default, MainSize = default } };
        }

        private UiElement BuildGauge()
        {
            var gaugeMin = Number("min", 0, -1e12, 1e12); var gaugeMax = Number("max", 100, -1e12, 1e12);
            if (gaugeMax <= gaugeMin) throw new FormatException("For gauge, max must be greater than min.");
            return new UiGauge { Key = key, Fill = fill, MainSize = mainSize,
                Level = Math.Clamp((Number("value", gaugeMin, -1e12, 1e12) - gaugeMin) / (gaugeMax - gaugeMin), 0, 1),
                StartAngle = Number("startAngle", -135, -3600, 3600), EndAngle = Number("endAngle", 135, -3600, 3600),
                LevelColor = Color("color", "#54dfcc"),
                TrackColor = Attribute("trackColor") is "" or "—" ? default : UiValue.Of(Color("trackColor", "#ffffff")),
                Thickness = Length("thickness", .04, 1) };
        }

        private UiElement BuildImage()
        {
            var source = node.Name.LocalName == "svg"
                ? SvgTemplate.Source(string.Concat(node.Nodes().OfType<XText>().Select(t => t.Value)), value => Expand(value, values), Integer("rasterSize", 512, 64, 1024))
                : Attribute("source");
            var colorKey = (string?)node.Attribute("averageColorKey") ?? "";
            if (colorKey.Length > 0 && (source.Trim().StartsWith("icon-pack:", StringComparison.OrdinalIgnoreCase)
                || source.Trim().StartsWith("/api/icons/", StringComparison.Ordinal)))
                throw new FormatException("Average color is not available for Icon Pack resources in SDK beta.17.");
            owner.ConfigureImageColor?.Invoke(key, colorKey, (string?)node.Attribute("averageColorFallback") ?? "transparent");
            var resource = context.Images?.Invoke(key, source);
            var containerFrame = Choice("frame", "square") == "container";
            var imageSize = containerFrame ? 1 : Length("size", 1, 1);
            var imageFill = Boolean("fill", containerFrame);
            var imageOpacity = Number("opacity", 1);
            // Keep the native tint-capable tree stable when a conditional colour is cleared.
            var tintMode = attributes.ContainsKey("color") || node.Elements("style").Any(s => s.Attribute("color") != null)
                || duration > 0 && animated.Contains("color");
            var tint = Attribute("color") is "" or "—" ? null : Color("color", "#ffffff");
            var tintAlpha = tint == null ? 1 : colors["color"].Opacity;
            var image = new UiImage { Key = key, Fill = imageFill, MainSize = mainSize, Size = imageSize,
                Source = resource == null ? default : UiValue.Of(resource), Transition = Choice("transition", "none") == "none" ? default : UiValue.Of("crossfade"),
                Opacity = imageOpacity, Brightness = Number("brightness", 1, 0, 2), Saturation = Number("saturation", 1, 0, 2) };
            var fit = Choice("fit", "contain");
            var zoom = Number("zoom", 1, .1, 4);
            var offsetX = Number("offsetX", 0, -1, 1);
            var offsetY = Number("offsetY", 0, -1, 1);
            // A transparent native artwork surface fits the allocated rectangle, unlike
            // ui.image's intrinsic square. No events means the artwork cannot claim input.
            // The modifier owns clipping/radius so nested button corners do not leak in.
            if (containerFrame)
                return new UiModifier { Key = key, Fill = imageFill, MainSize = mainSize, Clip = "bounds",
                    Child = new UiModifier { Key = "corners", Radius = 0, Child = new UiButton { Key = "art", Source = image.Source, Transition = image.Transition,
                        Background = "transparent", Fit = fit, Zoom = zoom, OffsetX = offsetX, OffsetY = offsetY,
                        Tint = tint == null ? default : UiValue.Of(tint),
                        Opacity = imageOpacity * tintAlpha, Brightness = image.Brightness, Saturation = image.Saturation,
                        Children = [] } } };
            // Explicit framing keeps a stable tree while data-bound values change.
            if (!tintMode && fit == "contain" && !attributes.ContainsKey("zoom") && !attributes.ContainsKey("offsetX") && !attributes.ContainsKey("offsetY")
                && !(duration > 0 && animated.Overlaps(new[] { "zoom", "offsetX", "offsetY" })))
            {
                if (imageFill || attributes.ContainsKey("mainSize") && Attribute("mainSize") != "auto") return image;
                // ui.image centres artwork within the entire allocated cross axis. Give it
                // a natural square footprint so its parent's start/center/end alignment wins.
                return new UiModifier { Key = key, Fill = false,
                    Frame = new UiFrame { Width = UiLength.OfBasis(imageSize), Height = UiLength.OfBasis(imageSize) },
                    Child = image with { Key = "art", Fill = default, MainSize = default } };
            }
            var aspect = context.ImageAspectRatio?.Invoke(key) ?? 1;
            if (double.IsNaN(aspect) || tintMode)
                return new UiModifier { Key = key, Fill = imageFill, MainSize = mainSize, Clip = "bounds", Radius = 0,
                    Frame = new UiFrame { Width = UiLength.OfBasis(imageSize), Height = UiLength.OfBasis(imageSize) },
                    Child = new UiButton { Key = "art", Source = image.Source, Transition = image.Transition,
                        Background = "transparent", Fit = fit, Zoom = zoom, OffsetX = offsetX, OffsetY = offsetY,
                        Tint = tint == null ? default : UiValue.Of(tint),
                        Opacity = imageOpacity * tintAlpha, Brightness = image.Brightness, Saturation = image.Saturation,
                        Children = [] } };
            if (!double.IsFinite(aspect) || aspect <= 0) aspect = 1;
            return new UiModifier { Key = key, Fill = imageFill, MainSize = mainSize, Clip = "bounds",
                Frame = new UiFrame { Width = UiLength.OfBasis(imageSize), Height = UiLength.OfBasis(imageSize) },
                Child = new UiTransform { Key = "crop", Zoom = zoom * (fit == "cover" ? Math.Max(aspect, 1 / aspect) : 1), OffsetX = offsetX, OffsetY = offsetY,
                    Children = [image with { Key = "art", Fill = true, MainSize = default }] } };
        }

        private UiElement BuildClock()
        {
            return new UiClockDial { Key = key, Fill = fill, MainSize = mainSize,
            Value = UiTimeReference.InZone(Attribute("zone") is "—" ? null : Attribute("zone")), Seconds = Boolean("seconds", true),
            Color = attributes.ContainsKey("color") ? UiValue.Of(Color("color", "#ffffff")) : default };
        }

        private UiElement BuildProgressBar()
        {
            return new UiProgressBar { Key = key, Fill = fill, MainSize = mainSize,
            Value = Progress(), StartColor = Color("color", "#54dfcc"), EndColor = EndColor(), Thickness = Length("thickness", .05, 1) };
        }

        private UiElement BuildText()
        {
            return new UiTextRun { Key = key, Text = Expand(string.Concat(node.Nodes().OfType<XText>().Select(t => t.Value)), values), Size = TextLength(textSize), MinSize = TextLength(minTextSize),
            Weight = Choice("weight", "regular"), Color = attributes.ContainsKey("role") && !attributes.ContainsKey("color") ? UiValue.None<string>() : UiValue.Of(Color("color", "#ffffff")), Align = Choice("align", node.Parent?.Name == "segmented" ? "center" : "start"),
            Role = Choice("role", "primary"), FontFace = Attribute("fontFace") is var face && face is not ("" or "—") ? UiValue.Of(face) : UiValue.None<string>(),
            Digits = attributes.ContainsKey("digits") ? UiValue.Of(Number("digits", 0, 0, 32)) : UiValue.None<double>(),
            Shadow = Boolean("shadow", false),
            StrokeColor = Attribute("strokeColor") is "" or "—" ? default : UiValue.Of(Color("strokeColor", "#ffffff")),
            StrokeWidth = Length("strokeWidth", 0, .1),
            Wrap = Boolean("wrap", false), MaxLines = Integer("maxLines", 1, 1, 8), Fill = fill, MainSize = mainSize };
        }

        private UiElement BuildChart()
        {
            var dataKey = Attribute("key"); DataHub.ValidateName(dataKey);
            var low = Number("min", 0, -1e12, 1e12); var high = Number("max", 100, -1e12, 1e12);
            if (high <= low) throw new FormatException("For chart, max must be greater than min.");
            var count = Number("points", 60, 2, DataHub.HistoryLimit);
            if (count != Math.Truncate(count)) throw AttributeError("points", "For chart, points must be an integer.");
            var samples = context.History?.Invoke(dataKey, (int)count) ?? [];
            return new UiChart { Key = key, Fill = fill, MainSize = mainSize,
                Points = UiValue.Of<IReadOnlyList<double>>(samples.Select(v => Math.Clamp((v - low) / (high - low), 0, 1)).ToArray()),
                Color = Color("color", "#54dfcc"), PlotTop = Length("plotTop", .66, 1), Thickness = Length("thickness", .015, 1) };
        }

        private UiElement BuildBar()
        {
            var min = Number("min", 0, -1e12, 1e12); var max = Number("max", 100, -1e12, 1e12);
            if (max <= min) throw new FormatException("For bar, max must be greater than min.");
            return new UiRangeBar { Key = key, Start = Math.Clamp((Number("start", min, -1e12, 1e12) - min) / (max - min), 0, 1), End = Math.Clamp((Number("value", min, -1e12, 1e12) - min) / (max - min), 0, 1),
                Marker = attributes.ContainsKey("marker") ? UiValue.Of(Math.Clamp((Number("marker", min, -1e12, 1e12) - min) / (max - min), 0, 1)) : default,
                StartColor = Color("color", "#54dfcc"), EndColor = EndColor(), Thickness = Length("thickness", .05, 1), Fill = fill, MainSize = mainSize };
        }

        private UiProgressReference Progress()
        {
            long Milliseconds(string name) {
                var value = Number(name, 0, 0, 1e12);
                return value == Math.Truncate(value) ? (long)value : throw AttributeError(name, $"{name} must be an integer number of milliseconds.");
            }
            var rate = Number("rate", 0, -16, 16);
            var anchorText = Attribute("anchor");
            var anchor = DateTimeOffset.UnixEpoch;
            if (anchorText is not ("" or "—"))
            {
                if (!Regex.IsMatch(anchorText, @"^\d{4}-\d{2}-\d{2}T.*(?:Z|[+-]\d{2}:\d{2})$") ||
                    !DateTimeOffset.TryParse(anchorText, CultureInfo.InvariantCulture, DateTimeStyles.None, out anchor))
                    throw AttributeError("anchor", "anchor must be an ISO timestamp with a time zone, e.g. 2026-09-13T12:00:00+09:00.");
            }
            else if (rate != 0) throw new FormatException("When rate is nonzero, set anchor to the time positionMs was measured.");
            // A stable anchor avoids resetting playback on each refresh or unrelated data update.
            return new UiProgressReference { PositionMs = Milliseconds("positionMs"),
                DurationMs = Attribute("durationMs") is "" or "—" ? null : Milliseconds("durationMs"), Anchor = anchor, Rate = rate };
        }
    }
}
