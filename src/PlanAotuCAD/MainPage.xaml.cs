using System.Globalization;
using PlanAotuCAD.Core.Models;
using PlanAotuCAD.Core.Drawing;
using PlanAotuCAD.Core.Geometry;
using PlanAotuCAD.Core.Services;
using PlanAotuCAD.Core.Tools;

namespace PlanAotuCAD;

public partial class MainPage : ContentPage
{
    private readonly CadCoordinateTransformer _camera = new();
    private readonly CadEditorService _editor = new();
    private readonly CadDocumentStorage _storage = new();
    private readonly CadDxfService _dxfService = new();
    private readonly CadCanvasDrawable _drawable;

    private PointF _lastWorldPoint;
    private bool _isPanning;
    private PointF _panStartPoint;
    private float _initialPanX;
    private float _initialPanY;
    private float _pinchStartZoom;
    private PointF _pinchOrigin;
    private PointF _pinchAnchorWorld;

    public MainPage()
    {
        InitializeComponent();

        _drawable = new CadCanvasDrawable(_editor.Document, _camera);
        CadView.Drawable = _drawable;

        CadView.StartInteraction += OnCanvasStart;
        CadView.DragInteraction += OnCanvasDrag;
        CadView.EndInteraction += OnCanvasEnd;

        var pinchGesture = new PinchGestureRecognizer();
        pinchGesture.PinchUpdated += OnPinchUpdated;
        CadView.GestureRecognizers.Add(pinchGesture);

#if WINDOWS
        CadView.HandlerChanged += OnCadViewHandlerChanged;
#endif

        SetActiveTool(CadTool.Select);
        RefreshStatus();
    }

    private void OnSelectToolClicked(object? sender, EventArgs e) => SetActiveTool(CadTool.Select);
    private void OnLineToolClicked(object? sender, EventArgs e) => SetActiveTool(CadTool.Line);
    private void OnRectToolClicked(object? sender, EventArgs e) => SetActiveTool(CadTool.Rectangle);
    private void OnCircleToolClicked(object? sender, EventArgs e) => SetActiveTool(CadTool.Circle);

    private void SetActiveTool(CadTool tool)
    {
        _editor.SetTool(tool);

        SelectButton.BackgroundColor = tool == CadTool.Select ? ActiveColor : InactiveColor;
        LineButton.BackgroundColor = tool == CadTool.Line ? ActiveColor : InactiveColor;
        RectButton.BackgroundColor = tool == CadTool.Rectangle ? ActiveColor : InactiveColor;
        CircleButton.BackgroundColor = tool == CadTool.Circle ? ActiveColor : InactiveColor;

        StatusLabel.Text = tool switch
        {
            CadTool.Select => "SELECT: اختر عنصراً لتحريكه",
            CadTool.Line => "LINE: حدد نقطة البداية ثم النهاية",
            CadTool.Rectangle => "RECT: حدد زاويتين متقابلتين",
            CadTool.Circle => "CIRCLE: حدد المركز ثم نصف القطر",
            _ => "Ready"
        };

        RefreshCanvas();
    }

    private static Color ActiveColor => Color.FromArgb("#16A34A");
    private static Color InactiveColor => Color.FromArgb("#3B82F6");

    private void OnCanvasStart(object? sender, TouchEventArgs e)
    {
        var touches = e.Touches.ToArray();
        if (touches.Length == 0)
            return;

        _lastWorldPoint = ScreenToWorld(touches[0]);
        _editor.Begin(_lastWorldPoint, _camera.Zoom);
        _drawable.PreviewEntity = _editor.PreviewEntity;
        RefreshStatus();
        RefreshCanvas();
    }

    private void OnCanvasDrag(object? sender, TouchEventArgs e)
    {
        var touches = e.Touches.ToArray();
        if (touches.Length == 0)
            return;

        _lastWorldPoint = ScreenToWorld(touches[0]);
        _editor.Update(_lastWorldPoint);
        _drawable.PreviewEntity = _editor.PreviewEntity;
        RefreshCanvas();
    }

    private void OnCanvasEnd(object? sender, TouchEventArgs e)
    {
        var touches = e.Touches.ToArray();
        if (touches.Length > 0)
            _lastWorldPoint = ScreenToWorld(touches[0]);

        _editor.End(_lastWorldPoint);
        _drawable.PreviewEntity = null;
        RefreshStatus();
        RefreshCanvas();
    }

    private PointF ScreenToWorld(PointF point) =>
        _camera.ScreenToWorld(point, (float)CadView.Width, (float)CadView.Height);

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Started)
        {
            _pinchStartZoom = _camera.Zoom;
            _pinchOrigin = new PointF(
                (float)(e.ScaleOrigin.X * CadView.Width),
                (float)(e.ScaleOrigin.Y * CadView.Height));
            _pinchAnchorWorld = ScreenToWorld(_pinchOrigin);
            return;
        }

        if (e.Status != GestureStatus.Running)
            return;

        _camera.Zoom = Math.Clamp(_pinchStartZoom * (float)e.Scale, 0.1f, 20f);
        _camera.PanX = _pinchOrigin.X - (float)CadView.Width / 2f -
                       _pinchAnchorWorld.X * _camera.Zoom;
        _camera.PanY = _pinchOrigin.Y - (float)CadView.Height / 2f +
                       _pinchAnchorWorld.Y * _camera.Zoom;
        RefreshCanvas();
    }

    private async void OnNumericInputClicked(object? sender, EventArgs e)
    {
        var format = _editor.ActiveTool switch
        {
            CadTool.Line => "X;Y;Length;Angle (degrees)",
            CadTool.Rectangle => "X;Y;Width;Height",
            CadTool.Circle => "Center X;Center Y;Radius",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(format))
        {
            await DisplayAlert("الإدخال الرقمي", "اختر أداة LINE أو RECT أو CIRCLE أولاً.", "موافق");
            return;
        }

        var input = await DisplayPromptAsync(
            "الإدخال الرقمي",
            $"أدخل القيم بهذا الترتيب: {format}",
            "إضافة",
            "إلغاء",
            placeholder: "مثال: 0;0;100;45",
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(input))
            return;

        var values = input.Split(';', StringSplitOptions.TrimEntries);
        var expectedCount = _editor.ActiveTool == CadTool.Circle ? 3 : 4;

        if (values.Length != expectedCount ||
            values.Any(value =>
                !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
                !float.IsFinite(parsed)))
        {
            await DisplayAlert("قيم غير صحيحة", $"أدخل {expectedCount} أرقام مفصولة بالفاصلة المنقوطة (;). استخدم النقطة للفواصل العشرية.", "موافق");
            return;
        }

        var numbers = values
            .Select(value => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture))
            .ToArray();

        var dimensionsArePositive = _editor.ActiveTool switch
        {
            CadTool.Line => numbers[2] > 0,
            CadTool.Rectangle => numbers[2] > 0 && numbers[3] > 0,
            CadTool.Circle => numbers[2] > 0,
            _ => false
        };

        if (!dimensionsArePositive)
        {
            await DisplayAlert("أبعاد غير صالحة", "يجب أن يكون الطول والعرض والارتفاع ونصف القطر أكبر من صفر.", "موافق");
            return;
        }

        CadEntity entity = _editor.ActiveTool switch
        {
            CadTool.Line => CreateLine(numbers),
            CadTool.Rectangle => new CadRectangle(numbers[0], numbers[1], numbers[0] + numbers[2], numbers[1] + numbers[3]),
            CadTool.Circle => new CadCircle(numbers[0], numbers[1], numbers[2]),
            _ => null!
        };

        if (!_editor.AddEntity(entity))
        {
            await DisplayAlert("قيم غير صالحة", "يجب أن تكون الأبعاد أكبر من صفر.", "موافق");
            return;
        }

        RefreshStatus();
        RefreshCanvas();
        StatusLabel.Text = "تمت إضافة العنصر بالإحداثيات الرقمية";
    }

    private static CadLine CreateLine(float[] values)
    {
        var angle = values[3] * MathF.PI / 180f;
        return new CadLine(
            values[0],
            values[1],
            values[0] + values[2] * MathF.Cos(angle),
            values[1] + values[2] * MathF.Sin(angle));
    }

    private void OnDeleteClicked(object? sender, EventArgs e)
    {
        _editor.DeleteSelection();
        RefreshStatus();
        RefreshCanvas();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            var path = await _storage.SaveAsync(_editor.Document);
            StatusLabel.Text = $"تم حفظ الرسم: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            await DisplayAlert("تعذر الحفظ", ex.Message, "موافق");
        }
    }

    private async void OnOpenClicked(object? sender, EventArgs e)
    {
        try
        {
            var document = await _storage.OpenAsync();
            if (document is null)
                return;

            _editor.ReplaceDocument(document);
            _drawable.Document = _editor.Document;
            RefreshStatus();
            RefreshCanvas();
            StatusLabel.Text = $"تم فتح الرسم: {document.Name}";
        }
        catch (Exception ex)
        {
            await DisplayAlert("تعذر فتح الملف", ex.Message, "موافق");
        }
    }

    private async void OnImportDxfClicked(object? sender, EventArgs e)
    {
        try
        {
            var document = await _dxfService.ImportAsync();
            if (document is null)
                return;

            _editor.ReplaceDocument(document);
            _drawable.Document = _editor.Document;
            RefreshStatus();
            RefreshCanvas();
            StatusLabel.Text = $"تم استيراد DXF: {document.Name}";
        }
        catch (Exception ex)
        {
            await DisplayAlert("تعذر استيراد DXF", ex.Message, "موافق");
        }
    }

    private async void OnExportDxfClicked(object? sender, EventArgs e)
    {
        try
        {
            await _dxfService.ExportAsync(_editor.Document);
            StatusLabel.Text = "تم تجهيز ملف DXF للمشاركة أو الحفظ";
        }
        catch (Exception ex)
        {
            await DisplayAlert("تعذر تصدير DXF", ex.Message, "موافق");
        }
    }

    private void RefreshStatus()
    {
        EntityCountLabel.Text = $"{_editor.Document.EntityCount} entities";
        if (_editor.SelectedEntity is not null)
            StatusLabel.Text = "عنصر محدد — اسحب لتحريكه أو اضغط DELETE لحذفه";
    }

    private void RefreshCanvas() => CadView.Invalidate();

#if WINDOWS
    private void OnCadViewHandlerChanged(object? sender, EventArgs e)
    {
        if (CadView.Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement nativeView)
            return;

        nativeView.PointerWheelChanged -= OnPointerWheelChanged;
        nativeView.PointerWheelChanged += OnPointerWheelChanged;
        nativeView.PointerPressed -= OnPointerPressed;
        nativeView.PointerPressed += OnPointerPressed;
        nativeView.PointerMoved -= OnPointerMoved;
        nativeView.PointerMoved += OnPointerMoved;
        nativeView.PointerReleased -= OnPointerReleased;
        nativeView.PointerReleased += OnPointerReleased;
    }

    private void OnPointerWheelChanged(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var nativeView = (Microsoft.UI.Xaml.UIElement)sender;
        var point = e.GetCurrentPoint(nativeView);
        var delta = point.Properties.MouseWheelDelta;
        var position = point.Position;

        _camera.ZoomAt(
            new PointF((float)position.X, (float)position.Y),
            (float)CadView.Width,
            (float)CadView.Height,
            delta > 0 ? 1.1f : 0.9f);

        RefreshCanvas();
        e.Handled = true;
    }

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
        _panStartPoint = new PointF((float)position.X, (float)position.Y);
        _initialPanX = _camera.PanX;
        _initialPanY = _camera.PanY;

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

        _camera.PanX = _initialPanX + (float)position.X - _panStartPoint.X;
        _camera.PanY = _initialPanY + (float)position.Y - _panStartPoint.Y;

        RefreshCanvas();
        e.Handled = true;
    }

    private void OnPointerReleased(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_isPanning)
            return;

        _isPanning = false;
        ((Microsoft.UI.Xaml.UIElement)sender).ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }
#endif
}
