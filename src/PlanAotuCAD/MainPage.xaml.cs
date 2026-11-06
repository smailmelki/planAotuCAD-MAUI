
using System.Linq;
using PlanAotuCAD.Core.Drawing;
using PlanAotuCAD.Core.Models;

namespace PlanAotuCAD;

public partial class MainPage : ContentPage
{
    private readonly CadCanvasDrawable _drawable = new();

    private bool _lineToolActive;
    private bool _isDrawingLine;
    private PointF _startPoint;
	private bool _rectToolActive;
	private bool _isDrawingRectangle;
	private bool _isPanning;
	private PointF _panStartPoint;
	private float _initialPanX;
	private float _initialPanY;
	private PointF _rectangleStartPoint;

    public MainPage()
    {
        InitializeComponent();

		#if WINDOWS
			CadView.HandlerChanged += OnCadViewHandlerChanged;
		#endif

        CadView.Drawable = _drawable;

        CadView.StartInteraction += OnCanvasStart;
        CadView.DragInteraction += OnCanvasDrag;
        CadView.EndInteraction += OnCanvasEnd;
    }

	private void OnLineToolClicked(object? sender, EventArgs e)
{
    _lineToolActive = !_lineToolActive;

    // إلغاء أداة المستطيل
    _rectToolActive = false;
    _isDrawingRectangle = false;
    _drawable.PreviewRectangle = null;

    // إلغاء أي عملية رسم خط سابقة
    _isDrawingLine = false;
    _drawable.PreviewLine = null;

    // تحديث ألوان الأزرار
    LineButton.BackgroundColor = _lineToolActive
        ? Color.FromArgb("#16A34A")
        : Color.FromArgb("#3B82F6");

    RectButton.BackgroundColor = Color.FromArgb("#3B82F6");

    StatusLabel.Text = _lineToolActive
        ? "LINE: Select start point"
        : "Ready";

    CadView.Invalidate();
}
	private void OnRectToolClicked(object? sender, EventArgs e)
{
    _rectToolActive = !_rectToolActive;

    _lineToolActive = false;
    _isDrawingLine = false;
    _drawable.PreviewLine = null;

    _isDrawingRectangle = false;
    _drawable.PreviewRectangle = null;

    RectButton.BackgroundColor = _rectToolActive
        ? Color.FromArgb("#16A34A")
        : Color.FromArgb("#3B82F6");

    LineButton.BackgroundColor = Color.FromArgb("#3B82F6");

    StatusLabel.Text = _rectToolActive
        ? "RECT: Select first corner"
        : "Ready";

    CadView.Invalidate();
}

    private void OnCanvasStart(object? sender, TouchEventArgs e)
    {
        var touches = e.Touches.ToArray();
		if (_rectToolActive)
{
    _rectangleStartPoint = ScreenToWorld(touches[0]);
    _isDrawingRectangle = true;

    _drawable.PreviewRectangle = new CadRectangle(
        _rectangleStartPoint.X,
        _rectangleStartPoint.Y,
        _rectangleStartPoint.X,
        _rectangleStartPoint.Y);

    StatusLabel.Text = "RECT: Select opposite corner";

    CadView.Invalidate();
    return;
}
        if (!_lineToolActive || touches.Length == 0)
            return;

        _startPoint = ScreenToWorld(touches[0]);
        _isDrawingLine = true;

        _drawable.PreviewLine = new CadLine(
            _startPoint.X,
            _startPoint.Y,
            _startPoint.X,
            _startPoint.Y);

        StatusLabel.Text = "LINE: Select end point";

        CadView.Invalidate();
    }

    private void OnCanvasDrag(object? sender, TouchEventArgs e)
    {
        var touches = e.Touches.ToArray();

	if (_isDrawingRectangle)
{
    PointF rectanglePoint = ScreenToWorld(touches[0]);

    _drawable.PreviewRectangle = new CadRectangle(
        _rectangleStartPoint.X,
        _rectangleStartPoint.Y,
        rectanglePoint.X,
        rectanglePoint.Y);

    CadView.Invalidate();
    return;
}

        if (!_isDrawingLine || touches.Length == 0)
            return;

        PointF point = ScreenToWorld(touches[0]);

        _drawable.PreviewLine = new CadLine(
            _startPoint.X,
            _startPoint.Y,
            point.X,
            point.Y);

        CadView.Invalidate();
    }

    private void OnCanvasEnd(object? sender, TouchEventArgs e)
    {
        var touches = e.Touches.ToArray();

		if (_isDrawingRectangle)
	{
    PointF rectangleEndPoint = ScreenToWorld(touches[0]);

    _drawable.Rectangles.Add(new CadRectangle(
        _rectangleStartPoint.X,
        _rectangleStartPoint.Y,
        rectangleEndPoint.X,
        rectangleEndPoint.Y));

    _drawable.PreviewRectangle = null;
    _isDrawingRectangle = false;

    StatusLabel.Text = $"Rectangles: {_drawable.Rectangles.Count}";

    CadView.Invalidate();
    return;
	}
		
        if (!_isDrawingLine || touches.Length == 0)
            return;

        PointF endPoint = ScreenToWorld(touches[0]);

        _drawable.Lines.Add(new CadLine(
            _startPoint.X,
            _startPoint.Y,
            endPoint.X,
            endPoint.Y));

        _drawable.PreviewLine = null;
        _isDrawingLine = false;

        StatusLabel.Text = $"Lines: {_drawable.Lines.Count}";

        CadView.Invalidate();
    }

	private PointF ScreenToWorld(PointF point)	
{
    float centerX = (float)CadView.Width / 2;
    float centerY = (float)CadView.Height / 2;

    return new PointF(
        (point.X - centerX - _drawable.PanX) / _drawable.Zoom,
        (centerY + _drawable.PanY - point.Y) / _drawable.Zoom);
}

	private void OnZoomChanged(float delta)
{
    float oldZoom = _drawable.Zoom;

    float factor = delta > 0 ? 1.1f : 0.9f;

    _drawable.Zoom = Math.Clamp(
        oldZoom * factor,
        0.1f,
        10f);

    CadView.Invalidate();
}

	
#if WINDOWS
private void OnCadViewHandlerChanged(object? sender, EventArgs e)
{
    if (CadView.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement nativeView)
    {
        nativeView.PointerWheelChanged -= OnPointerWheelChanged;
        nativeView.PointerWheelChanged += OnPointerWheelChanged;

        nativeView.PointerPressed -= OnPointerPressed;
        nativeView.PointerPressed += OnPointerPressed;

        nativeView.PointerMoved -= OnPointerMoved;
        nativeView.PointerMoved += OnPointerMoved;

        nativeView.PointerReleased -= OnPointerReleased;
        nativeView.PointerReleased += OnPointerReleased;

    }
}
#endif

#if WINDOWS
private void OnPointerWheelChanged(
    object sender,
    Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
{
    int delta = e.GetCurrentPoint(
        (Microsoft.UI.Xaml.UIElement)sender)
        .Properties.MouseWheelDelta;

    OnZoomChanged(delta);
    e.Handled = true;
}
	#endif

#if WINDOWS
private void OnPointerPressed(
    object sender,
    Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
{
    var nativeView = (Microsoft.UI.Xaml.UIElement)sender;
    var properties = e.GetCurrentPoint(nativeView).Properties;

    if (!properties.IsMiddleButtonPressed)
        return;

    var position = e.GetCurrentPoint(nativeView).Position;

    _isPanning = true;

    _panStartPoint = new PointF(
        (float)position.X,
        (float)position.Y);

    _initialPanX = _drawable.PanX;
    _initialPanY = _drawable.PanY;

    nativeView.CapturePointer(e.Pointer);
    e.Handled = true;
}

private void OnPointerMoved(
    object sender,
    Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
{
    if (!_isPanning)
        return;

    var nativeView = (Microsoft.UI.Xaml.UIElement)sender;
    var position = e.GetCurrentPoint(nativeView).Position;

    _drawable.PanX = _initialPanX +
        (float)position.X - _panStartPoint.X;

    _drawable.PanY = _initialPanY +
        (float)position.Y - _panStartPoint.Y;

    CadView.Invalidate();
    e.Handled = true;
}

private void OnPointerReleased(
    object sender,
    Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
{
    if (!_isPanning)
        return;

    _isPanning = false;

    var nativeView = (Microsoft.UI.Xaml.UIElement)sender;
    nativeView.ReleasePointerCapture(e.Pointer);

    e.Handled = true;
}
#endif
}
