using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using SkiaSharp;
using SkiaSharp.Skottie;

namespace Lottie;

internal class Renderer : CompositionCustomVisualHandler
{
    private Animation? _clip;
    private Stretch _fill = Stretch.Uniform;
    private StretchDirection _dir = StretchDirection.Both;
    private int _loops;
    private bool _playing;
    private bool _paused;
    private TimeSpan _elapsed;
    private TimeSpan? _lastHost;
    private int _doneCount;
    private readonly object _lock = new();
    private double _speed = 1.0;
    private int _fps = 30;
    private TimeSpan? _lastRenderTime;
    private Action<int, double>? _frameUpdateCallback;

    public override void OnMessage(object msg)
    {
        if (msg is not Message m) return;
        lock (_lock)
        {
            switch (m.Action)
            {
                case AnimationAction.Start:
                    _clip = m.Clip;
                    _fill = m.Fill!.Value;
                    _dir = m.Direction!.Value;
                    _loops = m.RepeatCount!.Value;
                    _speed = m.Speed ?? 1.0;
                    _fps = m.Fps ?? 30;
                    _playing = true;
                    _paused = false; // Default to playing when started
                    _elapsed = TimeSpan.Zero;
                    _doneCount = 0;
                    _lastHost = null;
                    _lastRenderTime = null;
                    RegisterForNextAnimationFrameUpdate();
                    break;

                case AnimationAction.Stop:
                    _playing = false;
                    _paused = false;
                    break;

                case AnimationAction.Pause:
                    _paused = true;
                    break;

                case AnimationAction.Resume:
                    _paused = false;
                    _lastHost = null; // Reset timing to avoid jumps
                    if (_playing)
                        RegisterForNextAnimationFrameUpdate();
                    break;

                case AnimationAction.Seek:
                    if (m.SeekTime.HasValue)
                    {
                        _elapsed = m.SeekTime.Value;
                        // Reset loop count when seeking
                        if (_clip != null)
                        {
                            _doneCount = (int)(_elapsed.TotalSeconds / _clip.Duration.TotalSeconds);
                            _elapsed = TimeSpan.FromSeconds(_elapsed.TotalSeconds % _clip.Duration.TotalSeconds);
                        }
                        UpdateFrameCallback();
                        Invalidate();
                    }
                    break;

                case AnimationAction.Terminate:
                    _playing = false;
                    _paused = false;
                    if (_clip != null)
                    {
                        _clip.Dispose();
                        _clip = null;
                    }
                    break;

                case AnimationAction.Refresh:
                    if (m.Fill.HasValue) _fill = m.Fill.Value;
                    if (m.Direction.HasValue) _dir = m.Direction.Value;
                    if (m.Speed.HasValue) _speed = m.Speed.Value;
                    if (m.Fps.HasValue) _fps = m.Fps.Value;
                    RegisterForNextAnimationFrameUpdate();
                    break;

                case AnimationAction.RefreshLoops:
                    if (m.RepeatCount.HasValue)
                        _loops = m.RepeatCount.Value;
                    break;

                case AnimationAction.SetFrameUpdateCallback:
                    _frameUpdateCallback = m.FrameUpdateCallback;
                    break;
            }
        }
    }

    public override void OnAnimationFrameUpdate()
    {
        if (!_playing || _paused) return;

        // fps limiting
        if (_fps > 0 && _lastRenderTime.HasValue)
        {
            var since = CompositionNow - _lastRenderTime.Value;
            var minInterval = TimeSpan.FromSeconds(1.0 / _fps);
            if (since < minInterval)
            {
                RegisterForNextAnimationFrameUpdate();
                return;
            }
        }

        _lastRenderTime = CompositionNow;
        Invalidate();
        RegisterForNextAnimationFrameUpdate();
    }

    public override void OnRender(ImmediateDrawingContext context)
    {
        lock (_lock)
        {
            if (_clip == null) return;

            // Only update elapsed time if not paused and playing
            if (_playing && !_paused)
            {
                if (_lastHost.HasValue)
                {
                    var delta = CompositionNow - _lastHost.Value;
                    _elapsed += TimeSpan.FromTicks((long)(delta.Ticks * _speed));
                }
                _lastHost = CompositionNow;

                // looping logic
                var dur = _clip.Duration;
                if (_elapsed > dur)
                {
                    _elapsed -= dur;
                    _doneCount++;
                    if (_loops >= 0 && _doneCount >= _loops)
                    {
                        _playing = false;
                        _elapsed = dur;
                    }
                }

                UpdateFrameCallback();
            }
            else if (_paused)
            {
                // Reset timing when paused to avoid jumps when resumed
                _lastHost = null;
            }

            if (context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature leaseFeature)
                return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            var viewport = GetRenderBounds();
            var native = new Avalonia.Size(_clip.Size.Width, _clip.Size.Height);
            var scale = _fill.CalculateScaling(viewport.Size, native, _dir);
            var target = new Avalonia.Rect(viewport.Size)
                .CenterRect(new Avalonia.Rect(native * scale));

            canvas.Save();
            canvas.Translate((float)target.X, (float)target.Y);
            canvas.Scale((float)(target.Width / native.Width), (float)(target.Height / native.Height));
            _clip.SeekFrameTime(_elapsed.TotalSeconds);
            _clip.Render(canvas, new SKRect(0, 0, _clip.Size.Width, _clip.Size.Height));
            canvas.Restore();
        }
    }

    private void UpdateFrameCallback()
    {
        if (_frameUpdateCallback == null || _clip == null)
            return;

        var position = _clip.Duration.TotalSeconds > 0 ? _elapsed.TotalSeconds / _clip.Duration.TotalSeconds : 0;
        var totalFrames = (int)Math.Ceiling(_clip.Duration.TotalSeconds * _fps);
        var currentFrame = totalFrames > 1 ? (int)(position * (totalFrames - 1)) : 0;
        
        _frameUpdateCallback.Invoke(currentFrame, position);
    }
}