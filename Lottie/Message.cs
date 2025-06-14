using Avalonia.Media;
using SkiaSharp.Skottie;

namespace Lottie;

internal readonly record struct Message(
    AnimationAction Action,
    Animation? Clip = null,
    Stretch? Fill = null,
    StretchDirection? Direction = null,
    int? RepeatCount = null,
    double? Speed = null,
    int? Fps = null,
    TimeSpan? SeekTime = null,
    Action<int, double>? FrameUpdateCallback = null);