using System.IO.Compression;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using SkiaSharp;
using SkiaSharp.Skottie;

namespace Lottie
{
    /// <summary>
    /// A control to display and control Motion (Lottie) animations.
    /// </summary>
    public class LottieView : Control
    {
        public const int LoopForever = -1;

        public static readonly StyledProperty<string?> SourceProperty =
            AvaloniaProperty.Register<LottieView, string?>(nameof(Source));

        public static readonly StyledProperty<Stretch> FillProperty =
            AvaloniaProperty.Register<LottieView, Stretch>(nameof(Fill), Stretch.Uniform);

        public static readonly StyledProperty<StretchDirection> DirectionProperty =
            AvaloniaProperty.Register<LottieView, StretchDirection>(
                nameof(Direction), StretchDirection.Both);

        public static readonly StyledProperty<int> LoopsProperty =
            AvaloniaProperty.Register<LottieView, int>(
                nameof(Loops), LoopForever);

        public static readonly StyledProperty<double> SpeedProperty =
            AvaloniaProperty.Register<LottieView, double>(
                nameof(Speed), 1.0);

        public static readonly StyledProperty<int> FpsProperty =
            AvaloniaProperty.Register<LottieView, int>(nameof(Fps), 30);

        public static readonly StyledProperty<IBrush> BackgroundProperty =
            AvaloniaProperty.Register<LottieView, IBrush>(
                nameof(Background), Brushes.Transparent);

        public static readonly StyledProperty<bool> IsPausedProperty =
            AvaloniaProperty.Register<LottieView, bool>(nameof(IsPaused));

        public static readonly StyledProperty<double> PositionProperty =
            AvaloniaProperty.Register<LottieView, double>(nameof(Position), coerce: CoercePosition);
        
        public static readonly DirectProperty<LottieView, int> CurrentFrameProperty =
            AvaloniaProperty.RegisterDirect<LottieView, int>(
                nameof(CurrentFrame),
                o => o.CurrentFrame,
                (o, v) => o.CurrentFrame = v,
                defaultBindingMode: BindingMode.TwoWay);

        public static readonly DirectProperty<LottieView, TimeSpan> DurationProperty =
            AvaloniaProperty.RegisterDirect<LottieView, TimeSpan>(
                nameof(Duration),
                o => o.Duration);

        public static readonly DirectProperty<LottieView, int> TotalFramesProperty =
            AvaloniaProperty.RegisterDirect<LottieView, int>(
                nameof(TotalFrames),
                o => o.TotalFrames);

        // allow the external control
        public static readonly StyledProperty<bool> AllowExternalControlProperty =
            AvaloniaProperty.Register<LottieView, bool>(nameof(AllowExternalControl), true);

        private readonly Uri _contextBase;
        private CompositionCustomVisual? _childVisual;
        private Animation? _current;
        private int _targetLoops;
        private TimeSpan _duration;
        private int _totalFrames;
        private int _currentFrame;
        private bool _isUpdatingInternally;
        private bool _isPausedSetExternally;
        private DateTime _lastExternalFrameUpdate = DateTime.MinValue;
        private readonly object _updateLock = new object();

        public string? Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public Stretch Fill
        {
            get => GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        public StretchDirection Direction
        {
            get => GetValue(DirectionProperty);
            set => SetValue(DirectionProperty, value);
        }

        public int Loops
        {
            get => GetValue(LoopsProperty);
            set => SetValue(LoopsProperty, value);
        }

        public double Speed
        {
            get => GetValue(SpeedProperty);
            set => SetValue(SpeedProperty, value);
        }

        public int Fps
        {
            get => GetValue(FpsProperty);
            set => SetValue(FpsProperty, value);
        }

        public IBrush Background
        {
            get => GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        public bool IsPaused
        {
            get => GetValue(IsPausedProperty);
            set => SetValue(IsPausedProperty, value);
        }

        public double Position
        {
            get => GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }

        public bool AllowExternalControl
        {
            get => GetValue(AllowExternalControlProperty);
            set => SetValue(AllowExternalControlProperty, value);
        }

        public int CurrentFrame
        {
            get => _currentFrame;
            set
            {
                var coercedValue = CoerceCurrentFrameValue(value);
                if (_currentFrame != coercedValue)
                {
                    var oldValue = _currentFrame;
                    
                    lock (_updateLock)
                    {
                        _currentFrame = coercedValue;
                        
                        // 检测外部设置
                        if (!_isUpdatingInternally && AllowExternalControl)
                        {
                            _lastExternalFrameUpdate = DateTime.UtcNow;
                            
                            // 处理外部设置的帧跳转
                            if (_totalFrames > 0)
                            {
                                var position = _totalFrames > 1 ? (double)coercedValue / (_totalFrames - 1) : 0;
                                var seekTime = TimeSpan.FromSeconds(_duration.TotalSeconds * position);
                                _childVisual?.SendHandlerMessage(
                                    new Message(AnimationAction.Seek, SeekTime: seekTime));
                            }
                        }
                    }
                    
                    RaisePropertyChanged(CurrentFrameProperty, oldValue, coercedValue);
                }
            }
        }

        public TimeSpan Duration
        {
            get => _duration;
            private set => SetAndRaise(DurationProperty, ref _duration, value);
        }

        public int TotalFrames
        {
            get => _totalFrames;
            private set => SetAndRaise(TotalFramesProperty, ref _totalFrames, value);
        }

        public LottieView(Uri baseUri) => _contextBase = baseUri;
        public LottieView(IServiceProvider sp) => _contextBase = sp.GetContextBaseUri();

        private static double CoercePosition(AvaloniaObject instance, double value)
        {
            return Math.Clamp(value, 0.0, 1.0);
        }

        private int CoerceCurrentFrameValue(int value)
        {
            if (_totalFrames > 0)
            {
                return Math.Clamp(value, 0, _totalFrames - 1);
            }
            return Math.Max(0, value);
        }

        /// <summary>
        /// 检查是否在外部控制模式下
        /// 如果最近的外部更新是在 500ms 内，则认为仍在外部控制中
        /// </summary>
        private bool IsInExternalControlMode()
        {
            if (!AllowExternalControl) return false;
            
            var timeSinceLastUpdate = DateTime.UtcNow - _lastExternalFrameUpdate;
            return timeSinceLastUpdate.TotalMilliseconds < 500; // 500ms 缓冲时间
        }

        public override void Render(DrawingContext context)
        {
            context.FillRectangle(Background, new Rect(Bounds.Size));
            base.Render(context);
        }

        protected override Size MeasureOverride(Size available)
        {
            if (_current == null)
                return new Size();

            var native = new Size(_current.Size.Width, _current.Size.Height);
            return Fill.CalculateSize(available, native, Direction);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_current == null)
                return new Size();

            var native = new Size(_current.Size.Width, _current.Size.Height);
            return Fill.CalculateSize(finalSize, native);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            var vis = ElementComposition.GetElementVisual(this);
            var comp = vis?.Compositor;
            if (comp == null)
                return;

            _childVisual = comp.CreateCustomVisual(new Renderer());
            _childVisual.SendHandlerMessage(new Message(AnimationAction.SetFrameUpdateCallback, FrameUpdateCallback: OnFrameUpdated));
            ElementComposition.SetElementChildVisual(this, _childVisual);
            LayoutUpdated += HandleLayout;

            // when the visual tree is attached,  apply the source
            if (!string.IsNullOrEmpty(Source))
            {
                ApplySource(Source);
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            LayoutUpdated -= HandleLayout;
            if (_childVisual != null)
            {
                _childVisual.SendHandlerMessage(new Message(AnimationAction.Terminate));
            }
            _childVisual = null;
            _current = null;
        }

        private void OnFrameUpdated(int currentFrame, double position)
        {
            if (_isUpdatingInternally)
                return;

            // 确保在UI线程执行
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnFrameUpdated(currentFrame, position));
                return;
            }

            // 检查是否在外部控制模式
            bool inExternalControl = IsInExternalControlMode();

            _isUpdatingInternally = true;
            try
            {
                // 总是更新 CurrentFrame，除非正在外部控制中
                if (!inExternalControl)
                {
                    CurrentFrame = currentFrame;
                }
                
                // 更新 Position（如果没有被外部控制或者没有暂停控制）
                if (!_isPausedSetExternally && Math.Abs(Position - position) > 0.001)
                {
                    SetCurrentValue(PositionProperty, position);
                }
            }
            finally
            {
                _isUpdatingInternally = false;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (_isUpdatingInternally)
                return;

            if (change.Property == SourceProperty)
            {
                var path = change.GetNewValue<string?>();
                if (_childVisual != null)
                {
                    ApplySource(path);
                }
            }
            else if (change.Property == LoopsProperty)
            {
                _targetLoops = Loops;
                _childVisual?.SendHandlerMessage(
                    new Message(AnimationAction.RefreshLoops, RepeatCount: _targetLoops));
            }
            else if (change.Property == SpeedProperty)
            {
                _childVisual?.SendHandlerMessage(
                    new Message(AnimationAction.Refresh, Speed: Speed));
            }
            else if (change.Property == FpsProperty)
            {
                _childVisual?.SendHandlerMessage(
                    new Message(AnimationAction.Refresh, Fps: Fps));
            }
            else if (change.Property == IsPausedProperty)
            {
                // Detect external setting
                if (!_isUpdatingInternally)
                {
                    _isPausedSetExternally = true;
                }

                var isPaused = change.GetNewValue<bool>();
                _childVisual?.SendHandlerMessage(
                    new Message(isPaused ? AnimationAction.Pause : AnimationAction.Resume));
            }
            else if (change.Property == PositionProperty)
            {
                // Only process if explicitly set externally
                if (!_isUpdatingInternally)
                {
                    var position = change.GetNewValue<double>();
                    var seekTime = TimeSpan.FromSeconds(_duration.TotalSeconds * position);
                    _childVisual?.SendHandlerMessage(
                        new Message(AnimationAction.Seek, SeekTime: seekTime));
                }
            }
        }

        private void HandleLayout(object? sender, EventArgs e)
        {
            if (_childVisual == null || _current == null)
                return;

            _childVisual.Size = new System.Numerics.Vector2(
                (float)Bounds.Width, (float)Bounds.Height);

            _childVisual.SendHandlerMessage(
                new Message(
                    AnimationAction.Refresh,
                    Clip: _current,
                    Fill: Fill,
                    Direction: Direction,
                    Speed: Speed,
                    Fps: Fps));
        }

        private void ApplySource(string? path)
        {
            // terminate the current animation if any, and apply the new source
            _childVisual?.SendHandlerMessage(new Message(AnimationAction.Terminate));
            _current = null;
            Duration = TimeSpan.Zero;
            TotalFrames = 0;
            
            // Reset control states
            _isPausedSetExternally = false;
            _lastExternalFrameUpdate = DateTime.MinValue;
            
            _isUpdatingInternally = true;
            try
            {
                SetCurrentValue(PositionProperty, 0.0);
                CurrentFrame = 0;
            }
            finally
            {
                _isUpdatingInternally = false;
            }

            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                using var str = OpenStream(path);
                if (!Animation.TryCreate(new SKManagedStream(str), out var anim))
                    return;

                _current = anim;
                Duration = anim.Duration;
                
                // Calculate total frames based on duration and fps
                var fps = Fps > 0 ? Fps : 30; // Default to 30 fps if not specified
                TotalFrames = (int)Math.Ceiling(anim.Duration.TotalSeconds * fps);
                
                InvalidateMeasure();
                _childVisual!.Size = new System.Numerics.Vector2(
                    (float)Bounds.Width, (float)Bounds.Height);

                // Start animation - it will auto-play
                _childVisual.SendHandlerMessage(
                    new Message(
                        AnimationAction.Start,
                        Clip: anim,
                        Fill: Fill,
                        Direction: Direction,
                        RepeatCount: Loops,
                        Speed: Speed,
                        Fps: fps));

                // Apply the current pause state if set
                if (_isPausedSetExternally && IsPaused)
                {
                    _childVisual.SendHandlerMessage(new Message(AnimationAction.Pause));
                }
            }
            catch
            {
                // ignore errors
            }
        }

        private Stream OpenStream(string p)
        {
            var uri = new Uri(p, UriKind.RelativeOrAbsolute);
            var stream = uri is { IsAbsoluteUri: true, IsFile: true }
                ? File.OpenRead(uri.LocalPath)
                : AssetLoader.Open(uri, _contextBase);

            if (!stream.CanSeek)
                stream = new BufferedStream(stream);

            var header = new byte[2];
            var read = stream.Read(header, 0, 2);
            stream.Seek(-read, SeekOrigin.Current);

            // gzip magic number
            if (read == 2 && header[0] == 0x1F && header[1] == 0x8B)
            {
                using var gzip = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
                var decompressed = new MemoryStream();
                gzip.CopyTo(decompressed);
                decompressed.Seek(0, SeekOrigin.Begin);
                return decompressed;
            }

            return stream;
        }
    }
}