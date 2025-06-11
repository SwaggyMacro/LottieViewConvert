using System.IO.Compression;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
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
            AvaloniaProperty.Register<LottieView, int>(nameof(Fps));

        public static readonly StyledProperty<IBrush> BackgroundProperty =
            AvaloniaProperty.Register<LottieView, IBrush>(
                nameof(Background), Brushes.Transparent);

        private readonly Uri _contextBase;
        private CompositionCustomVisual? _childVisual;
        private Animation? _current;
        private int _targetLoops;

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

        public LottieView(Uri baseUri) => _contextBase = baseUri;
        public LottieView(IServiceProvider sp) => _contextBase = sp.GetContextBaseUri();

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

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

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

            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                using var str = OpenStream(path);
                if (!Animation.TryCreate(new SKManagedStream(str), out var anim))
                    return;

                _current = anim;
                InvalidateMeasure();
                _childVisual!.Size = new System.Numerics.Vector2(
                    (float)Bounds.Width, (float)Bounds.Height);

                _childVisual.SendHandlerMessage(
                    new Message(
                        AnimationAction.Start,
                        Clip: anim,
                        Fill: Fill,
                        Direction: Direction,
                        RepeatCount: Loops,
                        Speed: Speed,
                        Fps: Fps));
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