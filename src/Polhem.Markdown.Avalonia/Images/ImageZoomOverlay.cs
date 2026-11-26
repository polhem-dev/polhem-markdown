using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
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
    // NOTE: The icons are drawn on a 20 by 20 grid, unscaled, so their strokes keep one width. The Blazor overlay
    // draws the same paths, in MarkdownRenderTree.Images.cs.
    private const string CloseIconPath = "M5.5 5.5 14.5 14.5M14.5 5.5 5.5 14.5";
    private const string SaveIconPath = "M10 3.5v9M6 9l4 4 4-4M3.5 13.5v1.5a1.5 1.5 0 0 0 1.5 1.5h10a1.5 1.5 0 0 0 1.5-1.5v-1.5";
    private const double IconSize = 20;
    private const double ButtonSize = 36;

    // A dark pill with a faint edge shows over a light image as over the dark backdrop.
    private static readonly IBrush s_toolbarBrush = new SolidColorBrush(Color.FromArgb(0xC7, 0x20, 0x20, 0x20));
    private static readonly IBrush s_toolbarEdge = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush s_hoverBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush s_pressedBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0xFF, 0xFF));

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

        var save = OverlayButton(SaveIconPath, SaveLabel);
        save.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);
        var close = OverlayButton(CloseIconPath, CloseLabel);
        close.Click += (_, _) => Close();
        var toolbar = new Border
        {
            Background = s_toolbarBrush,
            BorderBrush = s_toolbarEdge,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(ButtonSize / 2 + 5),
            Padding = new Thickness(4),
            Margin = new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                Children = { save, close },
            },
        };
        // A press between the buttons is the toolbar's, never a drag or a click beside the image.
        toolbar.PointerPressed += (_, e) => e.Handled = true;
        Children.Add(toolbar);

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

    /// <summary>The accessible name and tooltip of the save button.</summary>
    internal const string SaveLabel = "Save image";

    /// <summary>Occurs when the user asks to save the image; the viewer saves it.</summary>
    public event EventHandler? SaveRequested;

    /// <summary>
    /// A round button of the overlay's toolbar, drawn by the overlay instead of the theme so it looks the same in any
    /// application: a white icon, and a light disc under the pointer.
    /// </summary>
    private static Button OverlayButton(string icon, string label)
    {
        var frame = new Border
        {
            Width = ButtonSize,
            Height = ButtonSize,
            CornerRadius = new CornerRadius(ButtonSize / 2),
            Background = Brushes.Transparent,
            Child = new Path
            {
                Data = Geometry.Parse(icon),
                Width = IconSize,
                Height = IconSize,
                Stretch = Stretch.None,
                Stroke = Brushes.White,
                StrokeThickness = 1.75,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        var button = new Button
        {
            Template = new FuncControlTemplate<Button>((_, _) => frame),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        void Paint() => frame.Background = button.IsPressed ? s_pressedBrush
            : button.IsPointerOver ? s_hoverBrush
            : Brushes.Transparent;
        button.PropertyChanged += (_, e) =>
        {
            if (e.Property == InputElement.IsPointerOverProperty || e.Property == Button.IsPressedProperty)
            {
                Paint();
            }
        };
        AutomationProperties.SetName(button, label);
        ToolTip.SetTip(button, label);
        return button;
    }

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
