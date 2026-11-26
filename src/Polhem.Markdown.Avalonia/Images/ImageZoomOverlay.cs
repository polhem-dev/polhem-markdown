using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Path = Avalonia.Controls.Shapes.Path;

namespace Polhem.Markdown.Avalonia.Images;

/// <summary>
/// An image at full size over the whole window, which a user zooms with the wheel, a pinch or the + and - keys, pans by
/// dragging, and closes with Escape, the close button or a click beside the image. It opens fitted to the window, or at
/// its own size when that is smaller; a double click switches between the two.
/// </summary>
internal sealed class ImageZoomOverlay : Panel
{
    private static readonly Geometry s_closeIcon = Geometry.Parse("M6 6 18 18M18 6 6 18");

    private readonly LoadedImage _image;
    private readonly ImageCanvas _canvas;
    private Point _offset;
    private double _scale = 1;
    private bool _fitted = true;
    private Point? _dragFrom;
    private bool _dragged;
    private double _pinchStartScale;

    public ImageZoomOverlay(LoadedImage image, string alternativeText)
    {
        _image = image;
        Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x10, 0x10, 0x10));
        Focusable = true;
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.SizeAll);

        _canvas = new ImageCanvas(this);
        Children.Add(_canvas);

        var close = new Button
        {
            Content = new Path
            {
                Data = s_closeIcon,
                Stroke = Brushes.White,
                StrokeThickness = 2,
                StrokeLineCap = PenLineCap.Round,
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
            },
            Padding = new Thickness(10),
            Margin = new Thickness(12),
            Background = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = new Cursor(StandardCursorType.Arrow),
        };
        AutomationProperties.SetName(close, CloseLabel);
        ToolTip.SetTip(close, CloseLabel);
        close.Click += (_, _) => Close();
        Children.Add(close);

        AutomationProperties.SetName(this, alternativeText);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Group);
        AutomationProperties.SetIsControlElementOverride(this, true);

        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(PinchEvent, OnPinch);
        AddHandler(PinchEndedEvent, (_, _) => _pinchStartScale = 0);
        AddHandler(PointerTouchPadGestureMagnifyEvent, OnMagnify);
    }

    /// <summary>The accessible name and tooltip of the close button.</summary>
    internal const string CloseLabel = "Close";

    /// <summary>The most an image is magnified, as a multiple of its own size.</summary>
    internal const double MaxScale = 8;

    /// <summary>Occurs when the overlay closes; the viewer then takes it off the window.</summary>
    public event EventHandler? Closed;

    /// <summary>How large the image is drawn, as a multiple of its own size.</summary>
    public double Scale => _scale;

    /// <summary>Where the image is drawn, in the overlay's coordinates.</summary>
    public Rect ImageBounds => new(_offset, _image.Size * _scale);

    /// <summary>The scale at which the image fits the overlay with a margin, but no more than its own size.</summary>
    public double FitScale => FitScaleFor(Bounds.Size);

    private const double FitMargin = 24;

    public void Close() => Closed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Scales the image to <paramref name="scale"/>, keeping the point of it under <paramref name="anchor"/> where it is.
    /// </summary>
    public void ZoomTo(double scale, Point anchor)
    {
        scale = Math.Clamp(scale, Math.Min(FitScale, 1), Math.Max(MaxScale, FitScale));
        var factor = scale / _scale;
        _offset = new Point(anchor.X - (anchor.X - _offset.X) * factor, anchor.Y - (anchor.Y - _offset.Y) * factor);
        _scale = scale;
        _fitted = false;
        KeepInView();
        _canvas.InvalidateVisual();
    }

    /// <summary>Fits the image to the overlay, centered.</summary>
    public void Fit()
    {
        _scale = FitScale;
        _fitted = true;
        KeepInView();
        _canvas.InvalidateVisual();
    }

    /// <summary>
    /// Keeps an image larger than the overlay covering it, and a smaller one centered on the axis it is smaller on, so
    /// panning never loses it.
    /// </summary>
    private void KeepInView()
    {
        var size = _image.Size * _scale;
        var x = size.Width <= Bounds.Width
            ? (Bounds.Width - size.Width) / 2
            : Math.Clamp(_offset.X, Bounds.Width - size.Width, 0);
        var y = size.Height <= Bounds.Height
            ? (Bounds.Height - size.Height) / 2
            : Math.Clamp(_offset.Y, Bounds.Height - size.Height, 0);
        _offset = new Point(x, y);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);

        // The overlay layer is a canvas, which gives a child the size it asks for; this one asks for all of it.
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (_fitted)
        {
            _scale = FitScaleFor(finalSize);
            var shown = _image.Size * _scale;
            _offset = new Point((finalSize.Width - shown.Width) / 2, (finalSize.Height - shown.Height) / 2);
        }
        else
        {
            KeepInView();
        }
        return size;
    }

    private double FitScaleFor(Size size)
    {
        var room = size.Deflate(new Thickness(FitMargin));
        if (_image.Size.Width <= 0 || _image.Size.Height <= 0 || room.Width <= 0 || room.Height <= 0)
        {
            return 1;
        }
        return Math.Min(1, Math.Min(room.Width / _image.Size.Width, room.Height / _image.Size.Height));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                break;
            case Key.OemPlus or Key.Add:
                ZoomTo(_scale * KeyStep, center);
                break;
            case Key.OemMinus or Key.Subtract:
                ZoomTo(_scale / KeyStep, center);
                break;
            case Key.D0 or Key.NumPad0:
                Fit();
                break;
            default:
                base.OnKeyDown(e);
                return;
        }
        e.Handled = true;
    }

    private const double KeyStep = 1.25;

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        ZoomTo(_scale * Math.Pow(WheelStep, e.Delta.Y), e.GetPosition(this));
        e.Handled = true;
    }

    private const double WheelStep = 1.2;

    private void OnMagnify(object? sender, PointerDeltaEventArgs e)
    {
        ZoomTo(_scale * (1 + e.Delta.X), e.GetPosition(this));
        e.Handled = true;
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        if (_pinchStartScale == 0)
        {
            _pinchStartScale = _scale;
        }
        ZoomTo(_pinchStartScale * e.Scale, e.ScaleOrigin);
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var point = e.GetPosition(this);
        if (e.ClickCount % 2 == 0 && ImageBounds.Contains(point))
        {
            // A double click shows the image at its own size around the point, or fits it again.
            if (_fitted && FitScale < 1)
            {
                ZoomTo(1, point);
            }
            else
            {
                Fit();
            }
            _dragFrom = null;
        }
        else
        {
            _dragFrom = point;
            _dragged = false;
            e.Pointer.Capture(this);
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragFrom is not { } from)
        {
            return;
        }
        var point = e.GetPosition(this);
        var delta = point - from;
        if (!_dragged && Math.Abs(delta.X) + Math.Abs(delta.Y) < DragThreshold)
        {
            return;
        }
        _dragged = true;
        _dragFrom = point;
        _offset += delta;
        _fitted = false;
        KeepInView();
        _canvas.InvalidateVisual();
    }

    private const double DragThreshold = 4;

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragFrom is { } from && !_dragged && !ImageBounds.Contains(from))
        {
            // A click beside the image closes it, as a click on a browser's backdrop does.
            Close();
        }
        _dragFrom = null;
        e.Pointer.Capture(null);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);

    /// <summary>Draws the image where the overlay has it; a panel draws nothing of its own but its background.</summary>
    private sealed class ImageCanvas(ImageZoomOverlay overlay) : Control
    {
        public override void Render(DrawingContext context)
        {
            var image = overlay._image.Image;
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
            {
                context.DrawImage(image, new Rect(image.Size), overlay.ImageBounds);
            }
        }
    }
}
