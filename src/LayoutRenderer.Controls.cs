using System.Globalization;
using System.Text.Json;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;

namespace Ziopuzzle.CustomButton;

public sealed partial class LayoutRenderer
{
    private sealed partial class ComponentRenderer
    {
        private UiElement BuildTrackpad()
        {
            var keyX = Attribute("keyX", "x"); var keyY = Attribute("keyY", "y");
            DataHub.ValidateName(keyX); DataHub.ValidateName(keyY);
            if (keyX == keyY) throw new FormatException("Trackpad keyX and keyY must be different.");
            var minX = Number("leftValue", 0, -1e12, 1e12); var maxX = Number("rightValue", 100, -1e12, 1e12);
            var minY = Number("topValue", 0, -1e12, 1e12); var maxY = Number("bottomValue", 100, -1e12, 1e12);
            if (maxX == minX || maxY == minY) throw new FormatException("Trackpad opposite edges must have different values.");
            var stepX = Number("stepX", 0, 0, Math.Abs(maxX - minX)); var stepY = Number("stepY", 0, 0, Math.Abs(maxY - minY));
            double PadSnap(double n, double min, double max, double step) => Math.Clamp(step == 0 ? n : min + Math.Round((n - min) / step, MidpointRounding.AwayFromZero) * step, Math.Min(min, max), Math.Max(min, max));
            double PadLevel(string dataKey, double min, double max, double step)
            {
                var n = values.TryGetValue(dataKey, out var v) && double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed) ? parsed : min;
                return (PadSnap(n, min, max, step) - min) / (max - min);
            }
            var lx = PadLevel(keyX, minX, maxX, stepX); var ly = PadLevel(keyY, minY, maxY, stepY);
            if (!owner.padContacts.TryGetValue(key, out var contact)) owner.padContacts[key] = contact = new();
            var enabled = context.Input != null && Boolean("interactive", false);
            if (!enabled) contact.Id = null;
            void PadPublish(UiPointerSample sample, string eventName)
            {
                contact.LastSeen = Environment.TickCount64;
                var x = PadSnap(minX + Math.Clamp(sample.X / contact.Width, 0, 1) * (maxX - minX), minX, maxX, stepX);
                var y = PadSnap(minY + Math.Clamp(sample.Y / contact.Height, 0, 1) * (maxY - minY), minY, maxY, stepY);
                if (eventName == "position-start")
                    contact.StartX = contact.PreviousX = x;
                if (eventName == "position-start")
                    contact.StartY = contact.PreviousY = y;
                var position = new TrackpadValue(x, y, (x - minX) / (maxX - minX), (y - minY) / (maxY - minY), keyX, keyY,
                    contact.StartX, contact.StartY, contact.PreviousX, contact.PreviousY);
                contact.PreviousX = x; contact.PreviousY = y;
                Emit(new(elementId, eventName, Position: position));
            }
            var padEvents = enabled ? new[]
            {
                UiEventHandler.On(UiComponentEvents.PointerDown, e =>
                {
                    if (!e.TryGetPointerDown(out var down) || down.Width <= 0 || down.Height <= 0) return UiEventOutcome.Rejected("Expected pointer position and positive dimensions.");
                    if (contact.Id != null && Environment.TickCount64 - contact.LastSeen < 2000) return UiEventOutcome.Accepted;
                    owner.InputRevision++; contact.Id = down.Sample.Id; contact.Width = down.Width; contact.Height = down.Height;
                    PadPublish(down.Sample, "position-start"); return UiEventOutcome.Accepted;
                }),
                UiEventHandler.On(UiComponentEvents.PointerMove, e =>
                {
                    if (!e.TryGetPointerSamples(out var samples)) return UiEventOutcome.Rejected("Expected pointer samples.");
                    // Keep only the newest position from this contact; never replay a backlog.
                    var sample = samples.LastOrDefault(s => s.Id == contact.Id);
                    if (contact.Id != null && samples.Any(s => s.Id == contact.Id)) PadPublish(sample, "position-changing");
                    return UiEventOutcome.Accepted;
                }),
                UiEventHandler.On(UiComponentEvents.PointerUp, e =>
                {
                    if (!e.TryGetPointerUp(out var up)) return UiEventOutcome.Rejected("Expected pointer release.");
                    if (contact.Id == up.Sample.Id)
                    {
                        contact.Id = null; owner.InputRevision++;
                        if (!up.Cancelled) PadPublish(up.Sample, "position-end");
                    }
                    return UiEventOutcome.Accepted;
                })
            } : Array.Empty<UiEventHandler>();
            var padColor = ParseColor("color", "#54dfcc");
            var padBackground = ParseColor("background", "#252525");
            return AlphaPaint.Layer(new UiLayer { Key = key, Fill = fill, MainSize = mainSize, Events = padEvents, Children = [
                AlphaPaint.Fade(new UiShape { Key = "surface", Fill = true, Shape = "path", Path = "M0 0 H1 V1 H0 Z", Color = padBackground.Rgb }, padBackground.Opacity),
                AlphaPaint.Fade(new UiShape { Key = "cursor", Fill = true, Shape = "path", Path = FormattableString.Invariant($"M{lx} 0 V1 M0 {ly} H1"), StrokeColor = padColor.Rgb, StrokeWidth = .008 }, Boolean("showCursorWhileTouching", false) && contact.Id == null ? 0 : padColor.Opacity, force: true)
            ] });
        }

        private UiElement BuildTextField()
        {
            var textKey = Attribute("key");
            if (textKey.Length > 0) DataHub.ValidateName(textKey);
            if (Boolean("interactive", false) && textKey.Length == 0) throw new FormatException("An interactive text field needs a key to store its value.");
            if (textKey.Length > 0 && attributes.ContainsKey("value")) throw new FormatException("Use either key or value, not both.");
            var textValue = textKey.Length > 0 ? values.TryGetValue(textKey, out var tv) ? tv.ToString() : "" : Expand(attributes.GetValueOrDefault("value", ""), values);
            return new UiTextField { Key = key, Fill = fill, MainSize = mainSize, Text = textValue,
                Placeholder = Expand(attributes.GetValueOrDefault("placeholder", ""), values), Size = Length("size", .12, 1),
                Events = context.Input != null && Boolean("interactive", false) ? new[] { UiComponentEvents.Adjust, UiComponentEvents.Change }.Select(name =>
                    UiEventHandler.On(name, e => {
                        if (!e.TryGetString(out var text)) return UiEventOutcome.Rejected("Expected a string.");
                        // Match the channel payload limit before attempting a data update.
                        if (JsonSerializer.Serialize(text).Length > 16000) return UiEventOutcome.Rejected("Text exceeds the display data limit.");
                        try { Emit(new(elementId, name == UiComponentEvents.Adjust ? "text-changing" : "text-changed", DataKey: textKey, TextValue: text, PreviousTextValue: textValue)); }
                        catch (FormatException error) { return UiEventOutcome.Rejected(error.Message); }
                        textValue = text;
                        return UiEventOutcome.Accepted;
                    })).ToArray() : [] };
        }

        private UiElement BuildSelection()
        {
            var controlKey = Attribute("key");
            if (controlKey.Length > 0) DataHub.ValidateName(controlKey);
            if (Boolean("interactive", false) && controlKey.Length == 0) throw new FormatException("An interactive control needs a key to store its value.");
            if (controlKey.Length > 0 && attributes.ContainsKey("value")) throw new FormatException("Use either key or value, not both.");
            var rawValue = controlKey.Length > 0 ? values.TryGetValue(controlKey, out var cv) ? cv.ToString() : "" : Attribute("value");
            if (node.Name == "toggle")
            {
                var on = rawValue == "1" || bool.TryParse(rawValue, out var flag) && flag;
                if (rawValue is not ("" or "—" or "0" or "1") && !bool.TryParse(rawValue, out _)) throw new FormatException("Toggle value must be true, false, 0 or 1.");
                return new UiToggle { Key = key, Fill = fill, MainSize = mainSize, On = on, Size = Length("size", .2, 1), LevelColor = Color("color", "#54dfcc"),
                    Events = context.Input != null && Boolean("interactive", false) ? [UiEventHandler.On(UiComponentEvents.Change, e =>
                    {
                        if (!e.TryGetBoolean(out var next)) return UiEventOutcome.Rejected("Expected a boolean.");
                        Emit(new(elementId, "change", next ? 1 : 0, next ? 1 : 0, controlKey, next, PreviousValue: on ? 1 : 0));
                        return UiEventOutcome.Accepted;
                    })] : [] };
            }
            if (children.Length == 0) throw new FormatException("Segmented needs at least one child for its choices.");
            var selection = -1;
            if (rawValue is not ("" or "—") && !int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out selection)) throw new FormatException("Segmented value must be a zero-based integer index.");
            // Native segments allocate a box but do not center its content. Keep one
            // wrapper per choice so visual alignment never changes selection indices.
            var segments = children.Select(child => (UiElement)new UiStack
            {
                Key = child.Key, Fill = true, Direction = "vertical", Justify = "center", Align = "center",
                Children = [child with { Key = "content" }]
            }).ToArray();
            return new UiSegmented { Key = key, Fill = fill, MainSize = mainSize, Children = segments, Selected = selection, LevelColor = Color("color", "#54dfcc"),
                Events = context.Input != null && Boolean("interactive", false) ? [UiEventHandler.On(UiComponentEvents.Change, e =>
                {
                    if (!e.TryGetDouble(out var index) || !double.IsFinite(index) || index != Math.Truncate(index) || index < 0 || index >= children.Length) return UiEventOutcome.Rejected("Expected a valid segment index.");
                    Emit(new(elementId, "change", index, children.Length == 1 ? 0 : index / (children.Length - 1), controlKey, PreviousValue: selection));
                    return UiEventOutcome.Accepted;
                })] : [] };
        }

        private UiElement BuildSlider()
        {
            var sliderMin = Number("min", 0, -1e12, 1e12); var sliderMax = Number("max", 100, -1e12, 1e12);
            if (sliderMax <= sliderMin) throw new FormatException("For slider, max must be greater than min.");
            var sliderKey = Attribute("key");
            if (sliderKey.Length > 0) DataHub.ValidateName(sliderKey);
            if (Boolean("interactive", false) && sliderKey.Length == 0) throw new FormatException("An interactive slider needs a key to store its value.");
            if (sliderKey.Length > 0 && attributes.ContainsKey("value")) throw new FormatException("Use either key or value on a slider, not both.");
            var sliderFallback = Number("fallback", sliderMin, -1e12, 1e12);
            double sliderValue;
            if (sliderKey.Length > 0)
                sliderValue = values.TryGetValue(sliderKey, out var bound) && DisplayData.TryNumber(bound, out var numeric) ? numeric : sliderFallback;
            else if (attributes.GetValueOrDefault("value", "").Trim() is var binding
                && Slot.Match(binding) is { Success: true, Index: 0 } match && match.Length == binding.Length)
                sliderValue = Animate("value", double.TryParse(Attribute("value"), NumberStyles.Float, CultureInfo.InvariantCulture, out var direct) && double.IsFinite(direct) ? direct : sliderFallback);
            else sliderValue = Number("value", sliderFallback, -1e12, 1e12);
            var step = Number("step", 0, 0, sliderMax - sliderMin);
            double Snap(double value) => step == 0 ? value : Math.Clamp(sliderMin + Math.Round((value - sliderMin) / step, MidpointRounding.AwayFromZero) * step, sliderMin, sliderMax);
            UiEventHandler Handler(string name) => UiEventHandler.On(name, e =>
            {
                if (!e.TryGetDouble(out var level) || !double.IsFinite(level) || level < 0 || level > 1) return UiEventOutcome.Rejected("Expected a level between 0 and 1.");
                var value = Snap(sliderMin + level * (sliderMax - sliderMin));
                Emit(new(elementId, name, value, (value - sliderMin) / (sliderMax - sliderMin), sliderKey, PreviousValue: sliderValue));
                return UiEventOutcome.Accepted;
            });
            if (node.Name == "dial") return new UiDial { Key = key, Fill = fill, MainSize = mainSize,
                Level = Math.Clamp((Snap(sliderValue) - sliderMin) / (sliderMax - sliderMin), 0, 1),
                Step = step > 0 ? UiValue.Of(step / (sliderMax - sliderMin)) : default,
                StartAngle = Number("startAngle", -135, -360, 360), EndAngle = Number("endAngle", 135, -360, 360),
                LevelColor = Color("color", "#54dfcc"), Thickness = Length("thickness", .04, 1),
                Events = context.Input != null && Boolean("interactive", false) ? [Handler(UiComponentEvents.Adjust), Handler(UiComponentEvents.Change)] : [] };
            return new UiSlider { Key = key, Fill = fill, MainSize = mainSize,
                Level = Math.Clamp((Snap(sliderValue) - sliderMin) / (sliderMax - sliderMin), 0, 1),
                Step = step > 0 ? UiValue.Of(step / (sliderMax - sliderMin)) : default,
                Events = context.Input != null && Boolean("interactive", false) ? [Handler(UiComponentEvents.Adjust), Handler(UiComponentEvents.Change)] : [],
                Direction = Choice("direction", "horizontal"),
                LevelColor = Color("color", "#54dfcc"), Thickness = Length("thickness", .04, 1) };
        }
    }
}
