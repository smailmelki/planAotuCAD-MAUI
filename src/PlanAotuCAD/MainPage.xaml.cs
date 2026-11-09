using System.Globalization;
using Microsoft.Maui.Devices;
using PlanAotuCAD.Core.Models;
using PlanAotuCAD.Core.Drawing;
using PlanAotuCAD.Core.Geometry;
using PlanAotuCAD.Core.Services;
using PlanAotuCAD.Core.Tools;

namespace PlanAotuCAD;

public partial class MainPage : ContentPage
{
    // ───────────── ثوابت التصميم ─────────────
    // العرض والارتفاع الأدنى لاعتماد تخطيط الكمبيوتر؛ الهاتف في الوضع الأفقي يبقى بتخطيط الهاتف.
    private const double WideMinWidth = 900;
    private const double WideMinHeight = 520;

    private static readonly Color ActiveColor = Color.FromArgb("#147D60");
    private static readonly Color InactiveColor = Color.FromArgb("#24364B");
    private static readonly Color BorderTone = Color.FromArgb("#3C5068");
    private static readonly Color TextLight = Color.FromArgb("#F4F7FB");
    private static readonly Color TextMuted = Color.FromArgb("#9FB0C3");
    private static readonly Color ActiveRowColor = Color.FromArgb("#24485A");

    private sealed record ToolDef(CadTool Tool, string Label, string Icon, string Hint, string[] Aliases);

    private static readonly ToolDef[] ToolDefs =
    {
        new(CadTool.Select, "تحديد", "tool_select.svg", "تحديد: المس عنصراً لتحديده ثم اسحبه لتحريكه", new[] { "V", "SELECT", "تحديد" }),
        new(CadTool.Pan, "تحريك", "tool_pan.svg", "تحريك العرض: اسحب لتحريك مساحة الرسم", new[] { "H", "PAN", "تحريك" }),
        new(CadTool.Line, "خط", "tool_line.svg", "خط: اسحب من نقطة البداية إلى نقطة النهاية", new[] { "L", "LINE", "خط" }),
        new(CadTool.Rectangle, "مستطيل", "tool_rect.svg", "مستطيل: اسحب بين زاويتين متقابلتين", new[] { "R", "REC", "RECT", "RECTANGLE", "مستطيل" }),
        new(CadTool.Circle, "دائرة", "tool_circle.svg", "دائرة: اسحب من المركز إلى المحيط", new[] { "C", "CIRCLE", "دائرة" }),
        new(CadTool.Ellipse, "بيضاوي", "tool_ellipse.svg", "بيضاوي: اسحب بين زاويتين متقابلتين", new[] { "E", "EL", "ELLIPSE", "بيضاوي" }),
        new(CadTool.Triangle, "مثلث", "tool_triangle.svg", "مثلث: اسحب لتحديد حدوده", new[] { "T", "TRI", "TRIANGLE", "مثلث" }),
        new(CadTool.Arc, "قوس", "tool_arc.svg", "قوس: اسحب بين طرفي القطر", new[] { "A", "ARC", "قوس" }),
        new(CadTool.Polyline, "متعدد", "tool_polyline.svg", "متعدد الخطوط: ارسم المسار بالسحب", new[] { "P", "PL", "PLINE", "POLYLINE", "متعدد الخطوط" }),
        new(CadTool.Polygon, "مضلع", "tool_polygon.svg", "مضلع: اسحب لتحديد القطر (سداسي افتراضياً)", new[] { "G", "POL", "POLYGON", "مضلع" })
    };

    private enum SheetTab { Layers, Properties }

    // ───────────── المحرر والكاميرا ─────────────
    private readonly CadCoordinateTransformer _camera = new();
    private readonly CadEditorService _editor = new();
    private readonly CadDocumentStorage _storage = new();
    private readonly CadDxfService _dxfService = new();
    private readonly CadCanvasDrawable _drawable;

    private readonly List<(CadTool Tool, Button Button)> _paletteButtons = new();
    private readonly List<(CadTool Tool, Button Button)> _trayButtons = new();

    private PointF _lastWorldPoint;
    private float _hitRadiusPixels;

    // حالة التخطيط
    private bool _isWide;
    private bool _sheetOpen;
    private bool _trayOpen;
    private SheetTab _sheetTab = SheetTab.Layers;
    private CadTool _lastShapeTool = CadTool.Line;
    private bool _snapEnabled = true;

    // حالة الإيماءات
    private bool _gestureCancelled;
    private bool _isHandPanning;
    private PointF _handStart;
    private float _handInitialPanX;
    private float _handInitialPanY;
    private float _pinchStartZoom;
    private PointF _pinchOrigin;
    private PointF _pinchAnchorWorld;

#if WINDOWS
    private bool _isPanning;
    private PointF _panStartPoint;
    private float _initialPanX;
    private float _initialPanY;
#endif

    public MainPage()
    {
        InitializeComponent();

        // اللمس يحتاج منطقة التقاط أوسع من الماوس.
        _hitRadiusPixels = DeviceInfo.Idiom == DeviceIdiom.Desktop ? 8f : 18f;

        _drawable = new CadCanvasDrawable(_editor.Document, _camera);
        CadView.Drawable = _drawable;

        CadView.StartInteraction += OnCanvasStart;
        CadView.DragInteraction += OnCanvasDrag;
        CadView.EndInteraction += OnCanvasEnd;
        CadView.CancelInteraction += OnCanvasCancel;

        var pinchGesture = new PinchGestureRecognizer();
        pinchGesture.PinchUpdated += OnPinchUpdated;
        CadView.GestureRecognizers.Add(pinchGesture);

#if WINDOWS
        CadView.HandlerChanged += OnCadViewHandlerChanged;
#endif

        BuildToolButtons();
        BuildColorSwatches();
        ConfigureTooltips();
        UpdateToggleButtons();

        SizeChanged += OnPageSizeChanged;

        ApplyLayout(DeviceInfo.Idiom == DeviceIdiom.Desktop);
        SetActiveTool(CadTool.Select);
        RefreshStatus();
        RefreshCanvas();
    }

    // ═════════════════════════════════════════════════════════════
    //  بناء الأدوات
    // ═════════════════════════════════════════════════════════════

    private void BuildToolButtons()
    {
        for (var i = 0; i < ToolDefs.Length; i++)
        {
            var def = ToolDefs[i];

            // فاصل بين أدوات التنقل (تحديد/تحريك) وأدوات الرسم
            if (i == 2)
                PaletteStack.Children.Add(CreateDivider());

            var paletteButton = CreateActionButton(def.Label, def.Icon, 76, 62);
            paletteButton.Clicked += (_, _) => SetActiveTool(def.Tool);
            ToolTipProperties.SetText(paletteButton, $"{def.Label}  ({def.Aliases[0]})");
            PaletteStack.Children.Add(paletteButton);
            _paletteButtons.Add((def.Tool, paletteButton));

            if (def.Tool is CadTool.Select or CadTool.Pan)
                continue;

            var trayButton = CreateActionButton(def.Label, def.Icon, 68, 58);
            trayButton.Clicked += (_, _) =>
            {
                _trayOpen = false;
                SetActiveTool(def.Tool);
            };
            TrayStack.Children.Add(trayButton);
            _trayButtons.Add((def.Tool, trayButton));
        }

        PaletteStack.Children.Add(CreateDivider());
        var numericPalette = CreateActionButton("إدخال رقمي", "cad_numeric.svg", 76, 62);
        numericPalette.Clicked += (_, _) => OnNumericInputClicked(this, EventArgs.Empty);
        ToolTipProperties.SetText(numericPalette, "إدخال رقمي للأبعاد والإحداثيات");
        PaletteStack.Children.Add(numericPalette);

        var numericTray = CreateActionButton("رقمي", "cad_numeric.svg", 68, 58);
        numericTray.Clicked += (_, _) =>
        {
            _trayOpen = false;
            ApplySheetState();
            OnNumericInputClicked(this, EventArgs.Empty);
        };
        TrayStack.Children.Add(numericTray);
    }

    private static BoxView CreateDivider() => new()
    {
        HeightRequest = 1,
        Color = BorderTone,
        Margin = new Thickness(6, 2)
    };

    private static Button CreateActionButton(string text, string icon, double width, double height) => new()
    {
        Text = text,
        ImageSource = icon,
        ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Top, 4),
        WidthRequest = width,
        HeightRequest = height,
        MinimumWidthRequest = width,
        MinimumHeightRequest = height,
        Padding = new Thickness(2, 4),
        FontSize = 11,
        CornerRadius = 10,
        BorderWidth = 0,
        BackgroundColor = InactiveColor,
        TextColor = TextLight
    };

    private void BuildColorSwatches()
    {
        var palette = new[] { "#F4F6FA", "#EF4444", "#F59E0B", "#FACC15", "#4ADE80", "#38BDF8", "#818CF8", "#E879F9" };
        foreach (var hex in palette)
            ColorSwatches.Children.Add(CreateSwatch(hex));

        // "تلقائي": يزيل اللون المخصص فيعود العنصر للون طبقته.
        ColorSwatches.Children.Add(CreateSwatch(null));
    }

    private View CreateSwatch(string? hex)
    {
        var swatch = new Border
        {
            WidthRequest = hex is null ? 58 : 36,
            HeightRequest = 36,
            Margin = new Thickness(0, 0, 8, 8),
            BackgroundColor = hex is null ? Color.FromArgb("#223449") : Color.FromArgb(hex),
            Stroke = BorderTone,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(8) },
            Content = hex is null
                ? new Label
                {
                    Text = "تلقائي",
                    FontSize = 11,
                    TextColor = TextMuted,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
                : null
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => PropertyColorEntry.Text = hex ?? string.Empty;
        swatch.GestureRecognizers.Add(tap);
        return swatch;
    }

    private void ConfigureTooltips()
    {
        ToolTipProperties.SetText(UndoButton, "تراجع  (Ctrl+Z)");
        ToolTipProperties.SetText(RedoButton, "إعادة  (Ctrl+Y)");
        ToolTipProperties.SetText(SaveButton, "حفظ  (Ctrl+S)");
        ToolTipProperties.SetText(OpenButton, "فتح  (Ctrl+O)");
        ToolTipProperties.SetText(NewButton, "مشروع جديد  (Ctrl+N)");
        ToolTipProperties.SetText(FitTopButton, "ملاءمة العرض  (Ctrl+F)");
        ToolTipProperties.SetText(SnapButton, "الالتقاط بنقاط الشبكة");
        ToolTipProperties.SetText(GridButton, "إظهار/إخفاء الشبكة");
    }

    // ═════════════════════════════════════════════════════════════
    //  التخطيط المتكيف: كمبيوتر مقابل هاتف
    // ═════════════════════════════════════════════════════════════

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0 || Height <= 0)
            return;

        var wide = Width >= WideMinWidth && Height >= WideMinHeight;
        if (wide != _isWide)
            ApplyLayout(wide);
        else
            UpdateSheetHeight();
    }

    private void ApplyLayout(bool wide)
    {
        _isWide = wide;
        _trayOpen = false;
        _sheetOpen = false;

        // عناصر الكمبيوتر
        ToolPalette.IsVisible = wide;
        BrandLabel.IsVisible = wide;
        DesktopActions.IsVisible = wide;
        DesktopStatusItems.IsVisible = wide;

        // عناصر الهاتف
        PrimaryBar.IsVisible = !wide;
        CompactSaveButton.IsVisible = !wide;
        FitButton.IsVisible = !wide;

        ProjectNameLabel.FontSize = wide ? 13 : 16;
        ProjectNameLabel.TextColor = wide ? Color.FromArgb("#AAB9CA") : Colors.White;
        ProjectNameLabel.FontAttributes = wide ? FontAttributes.None : FontAttributes.Bold;

        if (wide)
        {
            // لوحة جانبية ثابتة على اليمين تعرض الطبقات والخصائص معاً
            Grid.SetColumn(SidePanel, 2);
            SidePanel.WidthRequest = 310;
            SidePanel.HorizontalOptions = LayoutOptions.Fill;
            SidePanel.VerticalOptions = LayoutOptions.Fill;
            SidePanel.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(0) };
        }
        else
        {
            // ورقة سفلية فوق مساحة الرسم تظهر عند الطلب
            Grid.SetColumn(SidePanel, 1);
            SidePanel.WidthRequest = -1;
            SidePanel.HorizontalOptions = LayoutOptions.Fill;
            SidePanel.VerticalOptions = LayoutOptions.End;
            SidePanel.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16, 16, 0, 0) };
        }

        UpdateSheetHeight();
        ApplySheetState(refreshContent: true);
        UpdateToolHighlights();
        RefreshSelectionUi();
    }

    private void UpdateSheetHeight()
    {
        if (_isWide)
        {
            SidePanel.MaximumHeightRequest = double.PositiveInfinity;
            return;
        }

        var available = RootGrid.Height > 0 ? RootGrid.Height : Height;
        SidePanel.MaximumHeightRequest = available > 0 ? Math.Max(260, available * 0.6) : 420;
    }

    private void ApplySheetState(bool refreshContent = false)
    {
        if (_isWide)
        {
            SidePanel.IsVisible = true;
            PanelHeader.IsVisible = false;
            LayersSection.IsVisible = true;
            PropertiesSection.IsVisible = true;
            ShapeTray.IsVisible = false;
        }
        else
        {
            SidePanel.IsVisible = _sheetOpen;
            PanelHeader.IsVisible = true;
            LayersSection.IsVisible = _sheetTab == SheetTab.Layers;
            PropertiesSection.IsVisible = _sheetTab == SheetTab.Properties;
            ShapeTray.IsVisible = _trayOpen && !_sheetOpen;
            TabLayersButton.BackgroundColor = _sheetTab == SheetTab.Layers ? ActiveColor : Colors.Transparent;
            TabPropertiesButton.BackgroundColor = _sheetTab == SheetTab.Properties ? ActiveColor : Colors.Transparent;
        }

        if (refreshContent)
        {
            RefreshLayersPanel();
            RefreshPropertiesPanel();
        }

        RefreshSelectionBarVisibility();
        UpdateToolHighlights();
    }

    private void ShowSheet(SheetTab tab)
    {
        if (_isWide)
            return;

        _sheetTab = tab;
        _sheetOpen = true;
        _trayOpen = false;
        ApplySheetState(refreshContent: true);
    }

    private void HideSheet()
    {
        _sheetOpen = false;
        ApplySheetState();
    }

    private void OnTabLayersClicked(object? sender, EventArgs e)
    {
        _sheetTab = SheetTab.Layers;
        ApplySheetState(refreshContent: true);
    }

    private void OnTabPropertiesClicked(object? sender, EventArgs e)
    {
        _sheetTab = SheetTab.Properties;
        ApplySheetState(refreshContent: true);
    }

    private void OnCloseSheetClicked(object? sender, EventArgs e) => HideSheet();

    private void OnOpenPropertiesClicked(object? sender, EventArgs e) => ShowSheet(SheetTab.Properties);

    // ═════════════════════════════════════════════════════════════
    //  أزرار الهاتف السفلية
    // ═════════════════════════════════════════════════════════════

    private void OnPrimarySelectClicked(object? sender, EventArgs e)
    {
        _trayOpen = false;
        SetActiveTool(CadTool.Select);
    }

    private void OnPrimaryPanClicked(object? sender, EventArgs e)
    {
        _trayOpen = false;
        SetActiveTool(CadTool.Pan);
    }

    private void OnPrimaryDrawClicked(object? sender, EventArgs e)
    {
        _sheetOpen = false;

        if (IsShapeTool(_editor.ActiveTool))
        {
            _trayOpen = !_trayOpen;
            ApplySheetState();
            return;
        }

        // أول ضغطة: فعّل آخر شكل استُخدم وأظهر الصينية لتغييره بسرعة.
        _trayOpen = true;
        SetActiveTool(_lastShapeTool);
    }

    private void OnPrimaryLayersClicked(object? sender, EventArgs e)
    {
        if (_sheetOpen && _sheetTab == SheetTab.Layers)
            HideSheet();
        else
            ShowSheet(SheetTab.Layers);
    }

    private async void OnPrimaryMoreClicked(object? sender, EventArgs e)
    {
        var gridText = _drawable.ShowGrid ? "إخفاء الشبكة" : "إظهار الشبكة";
        var snapText = _snapEnabled ? "إيقاف الالتقاط بالشبكة" : "تشغيل الالتقاط بالشبكة";

        var choice = await DisplayActionSheetAsync("المزيد", "إلغاء", null,
            "مشروع جديد", "فتح", "حفظ باسم", "إدخال رقمي", "ملاءمة العرض",
            gridText, snapText, "استيراد DXF", "تصدير DXF");

        if (string.IsNullOrEmpty(choice) || choice == "إلغاء")
            return;

        if (choice == gridText) OnToggleGridClicked(this, EventArgs.Empty);
        else if (choice == snapText) OnToggleSnapClicked(this, EventArgs.Empty);
        else
        {
            switch (choice)
            {
                case "مشروع جديد": OnNewClicked(this, EventArgs.Empty); break;
                case "فتح": OnOpenClicked(this, EventArgs.Empty); break;
                case "حفظ باسم": OnSaveAsClicked(this, EventArgs.Empty); break;
                case "إدخال رقمي": OnNumericInputClicked(this, EventArgs.Empty); break;
                case "ملاءمة العرض": OnZoomFitClicked(this, EventArgs.Empty); break;
                case "استيراد DXF": OnImportDxfClicked(this, EventArgs.Empty); break;
                case "تصدير DXF": OnExportDxfClicked(this, EventArgs.Empty); break;
            }
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  الأدوات
    // ═════════════════════════════════════════════════════════════

    private static bool IsShapeTool(CadTool tool) => tool != CadTool.Select && tool != CadTool.Pan;

    private static string HintFor(CadTool tool) =>
        ToolDefs.FirstOrDefault(def => def.Tool == tool)?.Hint ?? "جاهز";

    private void OnMenuToolClicked(object? sender, EventArgs e)
    {
        if (sender is MenuFlyoutItem item &&
            item.CommandParameter is string name &&
            Enum.TryParse<CadTool>(name, out var tool))
        {
            SetActiveTool(tool);
        }
    }

    private void SetActiveTool(CadTool tool)
    {
        _editor.SetTool(tool);
        _isHandPanning = false;
        _drawable.PreviewEntity = null;

        if (IsShapeTool(tool))
            _lastShapeTool = tool;

        RefreshStatus();
        SetStatus(HintFor(tool));
        ApplySheetState();
        RefreshCanvas();
    }

    private void UpdateToolHighlights()
    {
        var active = _editor.ActiveTool;

        foreach (var (tool, button) in _paletteButtons)
            button.BackgroundColor = tool == active ? ActiveColor : InactiveColor;

        foreach (var (tool, button) in _trayButtons)
            button.BackgroundColor = tool == active ? ActiveColor : InactiveColor;

        var drawing = IsShapeTool(active);
        PrimarySelectButton.BackgroundColor = active == CadTool.Select ? ActiveColor : Colors.Transparent;
        PrimaryPanButton.BackgroundColor = active == CadTool.Pan ? ActiveColor : Colors.Transparent;
        PrimaryDrawButton.BackgroundColor = drawing ? ActiveColor : Colors.Transparent;
        PrimaryLayersButton.BackgroundColor = _sheetOpen ? ActiveColor : Colors.Transparent;

        // زر "رسم" يعرض الشكل الحالي حتى يعرف المستخدم ما الذي سيرسمه.
        var shown = ToolDefs.First(def => def.Tool == (drawing ? active : _lastShapeTool));
        PrimaryDrawButton.Text = drawing ? shown.Label : "رسم";
        PrimaryDrawButton.ImageSource = drawing ? shown.Icon : "cad_draw.svg";
    }

    // ═════════════════════════════════════════════════════════════
    //  الإيماءات: لمس وماوس
    // ═════════════════════════════════════════════════════════════

    private PointF ScreenToWorld(PointF point) =>
        _camera.ScreenToWorld(point, (float)CadView.Width, (float)CadView.Height);

    private PointF SnapIfNeeded(PointF point)
    {
        if (!_snapEnabled || !IsShapeTool(_editor.ActiveTool))
            return point;

        var step = _drawable.EffectiveGridSpacing;
        if (!float.IsFinite(step) || step <= 0)
            return point;

        return new PointF(MathF.Round(point.X / step) * step, MathF.Round(point.Y / step) * step);
    }

    private void OnCanvasStart(object? sender, TouchEventArgs e)
    {
        var touches = e.Touches.ToArray();
        _gestureCancelled = false;

        if (touches.Length == 0)
            return;

        // أكثر من إصبع يعني تكبيراً/تصغيراً، فلا نبدأ رسماً ولا تحريك عنصر.
        if (touches.Length > 1)
        {
            CancelCanvasGesture();
            return;
        }

        if (_trayOpen)
        {
            _trayOpen = false;
            ApplySheetState();
        }

        var screen = touches[0];
        ShowCoordinates(screen);

        if (_editor.ActiveTool == CadTool.Pan)
        {
            _isHandPanning = true;
            _handStart = screen;
            _handInitialPanX = _camera.PanX;
            _handInitialPanY = _camera.PanY;
            return;
        }

        _lastWorldPoint = SnapIfNeeded(ScreenToWorld(screen));
        _editor.Begin(_lastWorldPoint, _camera.Zoom, _hitRadiusPixels);
        _drawable.PreviewEntity = _editor.PreviewEntity;
        RefreshStatus();
        RefreshCanvas();
    }

    private void OnCanvasDrag(object? sender, TouchEventArgs e)
    {
        if (_gestureCancelled)
            return;

        var touches = e.Touches.ToArray();
        if (touches.Length == 0)
            return;

        if (touches.Length > 1)
        {
            CancelCanvasGesture();
            return;
        }

        var screen = touches[0];
        ShowCoordinates(screen);

        if (_isHandPanning)
        {
            _camera.PanX = _handInitialPanX + screen.X - _handStart.X;
            _camera.PanY = _handInitialPanY + screen.Y - _handStart.Y;
            RefreshCanvas();
            return;
        }

        _lastWorldPoint = SnapIfNeeded(ScreenToWorld(screen));
        _editor.Update(_lastWorldPoint);
        _drawable.PreviewEntity = _editor.PreviewEntity;

        var measure = DescribePreview(_editor.PreviewEntity);
        if (measure is not null)
            SetStatus(measure);

        RefreshCanvas();
    }

    private void OnCanvasEnd(object? sender, TouchEventArgs e)
    {
        if (_gestureCancelled)
        {
            _gestureCancelled = false;
            return;
        }

        if (_isHandPanning)
        {
            _isHandPanning = false;
            return;
        }

        var touches = e.Touches.ToArray();
        if (touches.Length > 0)
            _lastWorldPoint = SnapIfNeeded(ScreenToWorld(touches[0]));

        var countBefore = _editor.Document.EntityCount;
        _editor.End(_lastWorldPoint);
        _drawable.PreviewEntity = null;
        RefreshStatus();

        if (_editor.Document.EntityCount > countBefore && IsShapeTool(_editor.ActiveTool))
            SetStatus(HintFor(_editor.ActiveTool));

        RefreshCanvas();
    }

    private void OnCanvasCancel(object? sender, EventArgs e)
    {
        CancelCanvasGesture();
        _gestureCancelled = false;
    }

    /// <summary>يوقف العملية الجارية بأمان (مثلاً عند لمس إصبع ثانٍ) دون ترك معاينة أو تحريك ناقص.</summary>
    private void CancelCanvasGesture()
    {
        _gestureCancelled = true;
        _isHandPanning = false;
        _editor.AbortGesture();
        _drawable.PreviewEntity = null;
        RefreshCanvas();
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Started)
        {
            CancelCanvasGesture();
            _pinchStartZoom = _camera.Zoom;
            _pinchOrigin = new PointF(
                (float)(e.ScaleOrigin.X * CadView.Width),
                (float)(e.ScaleOrigin.Y * CadView.Height));
            _pinchAnchorWorld = ScreenToWorld(_pinchOrigin);
            return;
        }

        if (e.Status != GestureStatus.Running)
            return;

        _camera.Zoom = Math.Clamp(_pinchStartZoom * (float)e.Scale,
            CadCoordinateTransformer.MinZoom, CadCoordinateTransformer.MaxZoom);
        _camera.PanX = _pinchOrigin.X - (float)CadView.Width / 2f -
                       _pinchAnchorWorld.X * _camera.Zoom;
        _camera.PanY = _pinchOrigin.Y - (float)CadView.Height / 2f +
                       _pinchAnchorWorld.Y * _camera.Zoom;
        RefreshCanvas();
    }

    private void ShowCoordinates(PointF screen)
    {
        if (!_isWide)
            return;

        var world = ScreenToWorld(screen);
        var text = $"X {Fmt(world.X)}   Y {Fmt(world.Y)}";
        if (CoordLabel.Text != text)
            CoordLabel.Text = text;
    }

    private static string? DescribePreview(CadEntity? entity)
    {
        switch (entity)
        {
            case CadLine line:
            {
                var dx = line.EndX - line.StartX;
                var dy = line.EndY - line.StartY;
                var length = MathF.Sqrt(dx * dx + dy * dy);
                var angle = MathF.Atan2(dy, dx) * 180f / MathF.PI;
                if (angle < 0) angle += 360f;
                return $"الطول {Fmt(length)}    الزاوية {Fmt(angle)}°";
            }
            case CadRectangle rectangle:
                return $"العرض {Fmt(rectangle.Width)}    الارتفاع {Fmt(rectangle.Height)}";
            case CadCircle circle:
                return $"نصف القطر {Fmt(circle.Radius)}";
            case CadEllipse ellipse:
                return $"نصف القطر X {Fmt(ellipse.RadiusX)}    Y {Fmt(ellipse.RadiusY)}";
            default:
                return null;
        }
    }

    private static string Fmt(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    // ═════════════════════════════════════════════════════════════
    //  العرض: ملاءمة، شبكة، التقاط
    // ═════════════════════════════════════════════════════════════

    private void OnZoomFitClicked(object? sender, EventArgs e) => FitToDocument();

    private void FitToDocument()
    {
        var bounds = CadBounds.Of(_editor.Document);
        if (bounds is null)
            _camera.Reset();
        else
            _camera.ZoomToRect(bounds.Value, (float)CadView.Width, (float)CadView.Height, 48f);

        RefreshCanvas();
    }

    private void OnToggleGridClicked(object? sender, EventArgs e)
    {
        _drawable.ShowGrid = !_drawable.ShowGrid;
        UpdateToggleButtons();
        SetStatus(_drawable.ShowGrid ? "الشبكة: ظاهرة" : "الشبكة: مخفية");
        RefreshCanvas();
    }

    private void OnToggleSnapClicked(object? sender, EventArgs e)
    {
        _snapEnabled = !_snapEnabled;
        UpdateToggleButtons();
        SetStatus(_snapEnabled ? "الالتقاط بنقاط الشبكة: مفعّل" : "الالتقاط بنقاط الشبكة: متوقف");
    }

    private void UpdateToggleButtons()
    {
        SnapButton.BackgroundColor = _snapEnabled ? ActiveColor : InactiveColor;
        GridButton.BackgroundColor = _drawable.ShowGrid ? ActiveColor : InactiveColor;
    }

    // ═════════════════════════════════════════════════════════════
    //  الإدخال الرقمي وسطر الأوامر
    // ═════════════════════════════════════════════════════════════

    private static string? NumericFormat(CadTool tool) => tool switch
    {
        CadTool.Line => "X  Y  الطول  الزاوية (بالدرجات)",
        CadTool.Rectangle => "X  Y  العرض  الارتفاع",
        CadTool.Circle => "مركز X  مركز Y  نصف القطر",
        CadTool.Ellipse => "X  Y  العرض  الارتفاع",
        CadTool.Triangle => "X  Y  العرض  الارتفاع",
        CadTool.Arc => "مركز X  مركز Y  نصف القطر  زاوية البداية  زاوية المدى",
        CadTool.Polygon => "مركز X  مركز Y  نصف القطر  عدد الأضلاع (3-32)",
        CadTool.Polyline => "X1 Y1  X2 Y2  ...  (نقطتان على الأقل)",
        _ => null
    };

    private async void OnNumericInputClicked(object? sender, EventArgs e)
    {
        var tool = _editor.ActiveTool;
        var format = NumericFormat(tool);

        if (format is null)
        {
            await DisplayAlertAsync("الإدخال الرقمي", "اختر أداة رسم (مثل خط أو مستطيل) أولاً.", "موافق");
            return;
        }

        var input = await DisplayPromptAsync(
            "الإدخال الرقمي",
            $"أدخل القيم بهذا الترتيب، وافصل بينها بمسافة أو فاصلة:\n{format}",
            "إضافة",
            "إلغاء",
            placeholder: "مثال: 0 0 100 45",
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(input))
            return;

        if (!TryAddFromText(tool, input, out var error))
        {
            await DisplayAlertAsync("قيم غير صحيحة", error, "موافق");
            return;
        }

        SetStatus("تمت إضافة العنصر بالإحداثيات الرقمية");
    }

    private bool TryAddFromText(CadTool tool, string input, out string error)
    {
        if (!TryParseNumbers(input, out var numbers))
        {
            error = "استخدم أرقاماً فقط، والنقطة للفواصل العشرية.";
            return false;
        }

        if (!TryBuildEntity(tool, numbers, out var entity, out error) || entity is null)
            return false;

        if (!_editor.AddEntity(entity))
        {
            error = "تعذرت الإضافة: تأكد أن الطبقة النشطة ظاهرة وغير مقفلة.";
            return false;
        }

        RefreshStatus();
        RefreshCanvas();
        return true;
    }

    private static bool TryParseNumbers(string input, out float[] numbers)
    {
        var parts = NormalizeDigits(input).Split(
            new[] { ';', '؛', ',', '،', ' ', '\t' },
            StringSplitOptions.RemoveEmptyEntries);

        var list = new List<float>(parts.Length);
        foreach (var part in parts)
        {
            if (!float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                !float.IsFinite(value))
            {
                numbers = Array.Empty<float>();
                return false;
            }

            list.Add(value);
        }

        numbers = list.ToArray();
        return true;
    }

    /// <summary>يحوّل الأرقام العربية-الهندية إلى لاتينية حتى يعمل الإدخال بلوحة المفاتيح العربية.</summary>
    private static string NormalizeDigits(string text)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c >= '\u0660' && c <= '\u0669') chars[i] = (char)('0' + (c - '\u0660'));
            else if (c >= '\u06F0' && c <= '\u06F9') chars[i] = (char)('0' + (c - '\u06F0'));
            else if (c == '\u066B') chars[i] = '.';
        }

        return new string(chars);
    }

    private static bool TryBuildEntity(CadTool tool, float[] n, out CadEntity? entity, out string error)
    {
        entity = null;
        error = string.Empty;

        var expected = tool switch
        {
            CadTool.Circle => 3,
            CadTool.Arc => 5,
            CadTool.Line or CadTool.Rectangle or CadTool.Ellipse or CadTool.Triangle or CadTool.Polygon => 4,
            _ => -1
        };

        if (tool == CadTool.Polyline)
        {
            if (n.Length < 4 || n.Length % 2 != 0)
            {
                error = "أدخل أزواج إحداثيات (X Y) لنقطتين على الأقل.";
                return false;
            }
        }
        else if (expected < 0)
        {
            error = "اختر أداة رسم تدعم الإدخال الرقمي.";
            return false;
        }
        else if (n.Length != expected)
        {
            error = $"أدخل {expected} أرقام بالترتيب الموضح، وافصل بينها بمسافة أو فاصلة.";
            return false;
        }

        var valid = tool switch
        {
            CadTool.Line => n[2] > 0,
            CadTool.Rectangle => n[2] > 0 && n[3] > 0,
            CadTool.Circle => n[2] > 0,
            CadTool.Ellipse or CadTool.Triangle => n[2] > 0 && n[3] > 0,
            CadTool.Arc => n[2] > 0 && n[4] > 0 && n[4] <= 360,
            CadTool.Polygon => n[2] > 0 && n[3] >= 3 && n[3] <= 32 &&
                               MathF.Abs(n[3] - MathF.Round(n[3])) < 0.001f,
            CadTool.Polyline => Enumerable.Range(1, n.Length / 2 - 1)
                .Any(i => MathF.Abs(n[i * 2] - n[(i - 1) * 2]) > 0.001f ||
                          MathF.Abs(n[i * 2 + 1] - n[(i - 1) * 2 + 1]) > 0.001f),
            _ => false
        };

        if (!valid)
        {
            error = "يجب أن يكون الطول والعرض والارتفاع ونصف القطر أكبر من صفر (وعدد الأضلاع بين 3 و32).";
            return false;
        }

        entity = tool switch
        {
            CadTool.Line => CreateLine(n),
            CadTool.Rectangle => new CadRectangle(n[0], n[1], n[0] + n[2], n[1] + n[3]),
            CadTool.Circle => new CadCircle(n[0], n[1], n[2]),
            CadTool.Ellipse => new CadEllipse(n[0], n[1], n[0] + n[2], n[1] + n[3]),
            CadTool.Triangle => new CadTriangle(n[0], n[1], n[0] + n[2], n[1] + n[3]),
            CadTool.Arc => new CadArc { CenterX = n[0], CenterY = n[1], Radius = n[2], StartAngle = n[3], SweepAngle = n[4] },
            CadTool.Polygon => CadEditorService.CreateRegularPolygon(new PointF(n[0], n[1]), n[2], (int)n[3]),
            CadTool.Polyline => new CadPolyline
            {
                Points = Enumerable.Range(0, n.Length / 2)
                    .Select(i => new CadVertex(n[i * 2], n[i * 2 + 1])).ToList()
            },
            _ => null
        };

        return entity is not null;
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

    /// <summary>سطر الأوامر (كمبيوتر): اكتب L أو R أو C ... لاختيار أداة، أو أرقاماً لإضافة عنصر.</summary>
    private async void OnCommandCompleted(object? sender, EventArgs e)
    {
        var text = CommandEntry.Text?.Trim() ?? string.Empty;
        CommandEntry.Text = string.Empty;
        if (text.Length == 0)
            return;

        try
        {
            await RunCommandAsync(text);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    private async Task RunCommandAsync(string text)
    {
        var normalized = NormalizeDigits(text);
        var first = normalized[0];

        if (char.IsDigit(first) || first == '-' || first == '.')
        {
            if (TryAddFromText(_editor.ActiveTool, normalized, out var error))
                SetStatus("تمت إضافة العنصر بالإحداثيات الرقمية");
            else
                SetStatus(error);
            return;
        }

        var key = normalized.ToUpperInvariant();

        foreach (var def in ToolDefs)
        {
            if (def.Aliases.Contains(key))
            {
                SetActiveTool(def.Tool);
                return;
            }
        }

        switch (key)
        {
            case "U":
            case "UNDO":
            case "تراجع":
                OnUndoClicked(this, EventArgs.Empty);
                break;
            case "REDO":
            case "إعادة":
                OnRedoClicked(this, EventArgs.Empty);
                break;
            case "DEL":
            case "DELETE":
            case "ERASE":
            case "حذف":
                OnDeleteClicked(this, EventArgs.Empty);
                break;
            case "FIT":
            case "ZE":
            case "ZOOM":
            case "ملاءمة":
                FitToDocument();
                break;
            case "GRID":
            case "الشبكة":
                OnToggleGridClicked(this, EventArgs.Empty);
                break;
            case "SNAP":
            case "الالتقاط":
                OnToggleSnapClicked(this, EventArgs.Empty);
                break;
            case "SAVE":
            case "حفظ":
                await SaveDocumentAsync(askName: false);
                break;
            case "OPEN":
            case "فتح":
                OnOpenClicked(this, EventArgs.Empty);
                break;
            case "NEW":
            case "جديد":
                OnNewClicked(this, EventArgs.Empty);
                break;
            default:
                SetStatus($"أمر غير معروف: {text}");
                break;
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  تراجع/إعادة/حذف
    // ═════════════════════════════════════════════════════════════

    private void OnUndoClicked(object? sender, EventArgs e)
    {
        if (!_editor.Undo())
            return;

        AfterDocumentChanged();
        SetStatus("تم التراجع عن آخر عملية");
    }

    private void OnRedoClicked(object? sender, EventArgs e)
    {
        if (!_editor.Redo())
            return;

        AfterDocumentChanged();
        SetStatus("تمت إعادة العملية");
    }

    private void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_editor.SelectedEntity is null)
        {
            SetStatus("لا يوجد عنصر محدد للحذف");
            return;
        }

        _editor.DeleteSelection();
        RefreshStatus();
        RefreshCanvas();
        SetStatus("تم حذف العنصر");
    }

    private void AfterDocumentChanged()
    {
        _drawable.Document = _editor.Document;
        RefreshStatus();
        RefreshLayersPanel();
        RefreshCanvas();
    }

    // ═════════════════════════════════════════════════════════════
    //  الطبقات
    // ═════════════════════════════════════════════════════════════

    private void RefreshLayersPanel()
    {
        LayersList.Children.Clear();
        foreach (var layer in _editor.Document.Layers.ToList())
            LayersList.Children.Add(BuildLayerRow(layer));
    }

    private View BuildLayerRow(CadLayer layer)
    {
        var isActive = layer.Id == _editor.Document.ActiveLayerId;

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(52)),
                new ColumnDefinition(new GridLength(52)),
                new ColumnDefinition(new GridLength(44))
            },
            ColumnSpacing = 4,
            Padding = new Thickness(8, 0),
            MinimumHeightRequest = 46,
            BackgroundColor = isActive ? ActiveRowColor : Colors.Transparent
        };

        var swatch = new BoxView
        {
            Color = ParseColor(layer.ColorHex, Colors.White),
            WidthRequest = 14,
            HeightRequest = 14,
            CornerRadius = 3,
            VerticalOptions = LayoutOptions.Center
        };
        row.Add(swatch, 0, 0);

        var nameLabel = new Label
        {
            Text = isActive ? $"{layer.Name}  (نشطة)" : layer.Name,
            TextColor = TextLight,
            FontSize = 14,
            FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None,
            VerticalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Fill,
            Padding = new Thickness(6, 0),
            LineBreakMode = LineBreakMode.TailTruncation
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => OnLayerTapped(layer.Id);
        nameLabel.GestureRecognizers.Add(tap);
        row.Add(nameLabel, 1, 0);

        var visibleBox = new CheckBox
        {
            IsChecked = layer.IsVisible,
            Color = ActiveColor,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        visibleBox.CheckedChanged += (_, args) =>
        {
            if (args.Value != layer.IsVisible)
                OnLayerFlagChanged(layer.Id, visibilityFlag: true, args.Value);
        };
        row.Add(visibleBox, 2, 0);

        var lockBox = new CheckBox
        {
            IsChecked = layer.IsLocked,
            Color = Color.FromArgb("#F59E0B"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        lockBox.CheckedChanged += (_, args) =>
        {
            if (args.Value != layer.IsLocked)
                OnLayerFlagChanged(layer.Id, visibilityFlag: false, args.Value);
        };
        row.Add(lockBox, 3, 0);

        var moreButton = new Button
        {
            ImageSource = "cad_more.svg",
            BackgroundColor = Colors.Transparent,
            WidthRequest = 44,
            HeightRequest = 44,
            MinimumWidthRequest = 44,
            MinimumHeightRequest = 44,
            Padding = new Thickness(0),
            VerticalOptions = LayoutOptions.Center
        };
        moreButton.Clicked += async (_, _) => await ShowLayerActionsAsync(layer);
        row.Add(moreButton, 4, 0);

        return row;
    }

    private static Color ParseColor(string? hex, Color fallback)
    {
        try
        {
            return string.IsNullOrWhiteSpace(hex) ? fallback : Color.FromArgb(hex);
        }
        catch
        {
            return fallback;
        }
    }

    private void OnLayerTapped(string layerId)
    {
        SetStatus(_editor.SetActiveLayer(layerId)
            ? "تم تعيين الطبقة النشطة"
            : "لا يمكن جعل طبقة مخفية أو مقفلة أو مجمدة هي النشطة");
        RefreshLayersPanel();
    }

    private void OnLayerFlagChanged(string layerId, bool visibilityFlag, bool value)
    {
        var ok = visibilityFlag
            ? _editor.SetLayerVisibility(layerId, value)
            : _editor.SetLayerLocked(layerId, value);

        if (!ok)
            SetStatus("لا يمكن إخفاء أو قفل الطبقة النشطة. اجعل طبقة أخرى نشطة أولاً.");

        // إعادة بناء القائمة بعد انتهاء حدث الصندوق نفسه.
        Dispatcher.Dispatch(() =>
        {
            RefreshLayersPanel();
            RefreshStatus();
            RefreshCanvas();
        });
    }

    private async void OnAddLayerClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("طبقة جديدة", "اسم الطبقة:", "إضافة", "إلغاء");
        if (string.IsNullOrWhiteSpace(name))
            return;

        if (!_editor.AddLayer(name))
        {
            await DisplayAlertAsync("الطبقات", "الاسم مستخدم أو غير صالح.", "موافق");
            return;
        }

        RefreshLayersPanel();
    }

    private async Task ShowLayerActionsAsync(CadLayer layer)
    {
        var freezeText = layer.IsFrozen ? "إلغاء التجميد" : "تجميد";
        var operation = await DisplayActionSheetAsync($"الطبقة: {layer.Name}", "إلغاء", null,
            "تعيين كنشطة", "إعادة تسمية", "تغيير اللون", freezeText,
            "نقل العنصر المحدد إليها", "دمج في طبقة أخرى", "حذف الطبقة");

        if (string.IsNullOrEmpty(operation) || operation == "إلغاء")
            return;

        if (operation == freezeText)
        {
            if (!_editor.SetLayerFrozen(layer.Id, !layer.IsFrozen))
                await DisplayAlertAsync("الطبقات", "لا يمكن تجميد الطبقة النشطة.", "موافق");
        }
        else
        {
            switch (operation)
            {
                case "تعيين كنشطة":
                    OnLayerTapped(layer.Id);
                    return;
                case "إعادة تسمية":
                    var newName = await DisplayPromptAsync("إعادة تسمية", "الاسم الجديد:", initialValue: layer.Name);
                    if (!string.IsNullOrWhiteSpace(newName) && !_editor.RenameLayer(layer.Id, newName))
                        await DisplayAlertAsync("الطبقات", "تعذرت إعادة التسمية (الاسم مستخدم، أو الطبقة الافتراضية).", "موافق");
                    break;
                case "تغيير اللون":
                    var layerColor = await DisplayPromptAsync("لون الطبقة", "أدخل HEX مثل #FFAA00:", initialValue: layer.ColorHex);
                    if (!string.IsNullOrWhiteSpace(layerColor) && !_editor.SetLayerColor(layer.Id, layerColor))
                        await DisplayAlertAsync("الطبقات", "صيغة اللون غير صحيحة.", "موافق");
                    break;
                case "نقل العنصر المحدد إليها":
                    if (!_editor.MoveSelectedToLayer(layer.Id))
                        await DisplayAlertAsync("الطبقات", "حدد عنصراً أولاً، وتأكد أن الطبقة قابلة للتحرير.", "موافق");
                    break;
                case "دمج في طبقة أخرى":
                    var mergeTargets = _editor.Document.Layers.Where(x => x.Id != layer.Id).ToArray();
                    if (mergeTargets.Length == 0)
                    {
                        await DisplayAlertAsync("الطبقات", "لا توجد طبقة أخرى للدمج فيها.", "موافق");
                        break;
                    }

                    var mergeName = await DisplayActionSheetAsync("دمج في الطبقة", "إلغاء", null,
                        mergeTargets.Select(x => x.Name).ToArray());
                    var mergeTarget = mergeTargets.FirstOrDefault(x => x.Name == mergeName);
                    if (mergeTarget is not null && !_editor.MergeLayer(layer.Id, mergeTarget.Id))
                        await DisplayAlertAsync("الطبقات", "تعذر الدمج. تأكد أن الطبقة الهدف ظاهرة وقابلة للتحرير.", "موافق");
                    break;
                case "حذف الطبقة":
                    if (!_editor.DeleteLayer(layer.Id))
                        await DisplayAlertAsync("الطبقات", "لا يمكن حذف الطبقة الافتراضية أو آخر طبقة.", "موافق");
                    break;
            }
        }

        AfterDocumentChanged();
    }

    // ═════════════════════════════════════════════════════════════
    //  خصائص العنصر المحدد
    // ═════════════════════════════════════════════════════════════

    private void RefreshPropertiesPanel()
    {
        var entity = _editor.SelectedEntity;
        NoSelectionLabel.IsVisible = entity is null;
        PropertiesForm.IsVisible = entity is not null;

        if (entity is null)
            return;

        PropertyNameEntry.Text = entity.Name;
        PropertyColorEntry.Text = entity.ColorHex ?? string.Empty;
        PropertyWidthEntry.Text = entity.StrokeWidth.ToString(CultureInfo.InvariantCulture);
        PropertyFillSwitch.IsToggled = entity.IsFilled;
        PropertyLayerPicker.ItemsSource = _editor.Document.Layers.Select(x => x.Name).ToList();
        PropertyLayerPicker.SelectedItem = _editor.Document.GetLayer(entity.LayerId).Name;
    }

    private async void OnApplyPropertiesClicked(object? sender, EventArgs e)
    {
        var entity = _editor.SelectedEntity;
        if (entity is null)
            return;

        if (!float.TryParse(NormalizeDigits(PropertyWidthEntry.Text ?? string.Empty), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var width) || !float.IsFinite(width) || width <= 0)
        {
            await DisplayAlertAsync("خصائص العنصر", "أدخل سمك خط موجباً وصحيحاً.", "موافق");
            return;
        }

        var layerName = PropertyLayerPicker.SelectedItem as string;
        var layer = _editor.Document.Layers.FirstOrDefault(x => x.Name == layerName);
        if (layer is null)
        {
            await DisplayAlertAsync("خصائص العنصر", "اختر طبقة صالحة.", "موافق");
            return;
        }

        if (!_editor.UpdateSelectedProperties(PropertyNameEntry.Text, PropertyColorEntry.Text,
                width, PropertyFillSwitch.IsToggled, layer.Id))
        {
            await DisplayAlertAsync("خصائص العنصر", "تعذر تطبيق الخصائص. تحقق من اللون والطبقة وسمك الخط.", "موافق");
            return;
        }

        _drawable.Document = _editor.Document;
        RefreshStatus();
        RefreshCanvas();
        SetStatus("تم تحديث خصائص العنصر");
    }

    // ═════════════════════════════════════════════════════════════
    //  الملفات
    // ═════════════════════════════════════════════════════════════

    private async void OnNewClicked(object? sender, EventArgs e)
    {
        if (_editor.Document.EntityCount > 0)
        {
            var proceed = await DisplayAlertAsync("مشروع جديد",
                "ستفقد التغييرات غير المحفوظة في الرسم الحالي. هل تريد المتابعة؟", "متابعة", "إلغاء");
            if (!proceed)
                return;
        }

        _editor.ReplaceDocument(new CadDocument());
        _camera.Reset();
        AfterDocumentChanged();
        SetStatus("مشروع جديد");
    }

    private async void OnSaveClicked(object? sender, EventArgs e) => await SaveDocumentAsync(askName: false);

    private async void OnSaveAsClicked(object? sender, EventArgs e) => await SaveDocumentAsync(askName: true);

    private async Task SaveDocumentAsync(bool askName)
    {
        try
        {
            var name = _editor.Document.Name;
            var isDefaultName = string.IsNullOrWhiteSpace(name) || name == "Drawing";

            // أول حفظ يسأل عن الاسم حتى لا تُستبدل الرسومات بعضها ببعض.
            if (askName || isDefaultName)
            {
                var entered = await DisplayPromptAsync("حفظ المشروع", "اسم المشروع:", "حفظ", "إلغاء",
                    placeholder: "مثال: مخطط المنزل", maxLength: 60,
                    initialValue: isDefaultName ? string.Empty : name);

                if (string.IsNullOrWhiteSpace(entered))
                    return;

                _editor.Document.Name = entered.Trim();
            }

            var fileName = await _storage.SaveAsync(_editor.Document);
            RefreshStatus();
            SetStatus($"تم حفظ الرسم: {fileName}");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("تعذر الحفظ", ex.Message, "موافق");
        }
    }

    private async void OnOpenClicked(object? sender, EventArgs e)
    {
        var choice = await DisplayActionSheetAsync(
            "فتح رسم",
            "إلغاء",
            null,
            "الرسومات المحفوظة",
            "فتح ملف JSON خارجي");

        if (choice == "إلغاء" || string.IsNullOrEmpty(choice))
            return;

        try
        {
            CadDocument? document;

            if (choice == "الرسومات المحفوظة")
            {
                var drawings = await _storage.GetSavedDrawingsAsync();

                if (drawings.Count == 0)
                {
                    await DisplayAlertAsync(
                        "الرسومات المحفوظة",
                        "لا توجد رسومات محفوظة داخل التطبيق حتى الآن.",
                        "موافق");
                    return;
                }

                var labels = drawings
                    .Select(drawing =>
                        $"{drawing.Name} — {drawing.LastModifiedUtc.ToLocalTime():dd/MM/yyyy HH:mm}")
                    .ToArray();

                var selectedLabel = await DisplayActionSheetAsync(
                    "اختر الرسم الذي تريد فتحه",
                    "إلغاء",
                    null,
                    labels);

                var selectedIndex = Array.IndexOf(labels, selectedLabel);
                if (selectedIndex < 0)
                    return;

                document = await _storage.OpenSavedAsync(drawings[selectedIndex].FileName);
            }
            else if (choice == "فتح ملف JSON خارجي")
            {
                document = await _storage.OpenExternalAsync();
            }
            else
            {
                return;
            }

            if (document is null)
                return;

            _editor.ReplaceDocument(document);
            AfterDocumentChanged();
            FitToDocument();
            SetStatus($"تم فتح الرسم: {document.Name}");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("تعذر فتح الرسم", ex.Message, "موافق");
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
            AfterDocumentChanged();
            FitToDocument();
            SetStatus($"تم استيراد DXF: {document.Name}");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("تعذر استيراد DXF", ex.Message, "موافق");
        }
    }

    private async void OnExportDxfClicked(object? sender, EventArgs e)
    {
        try
        {
            await _dxfService.ExportAsync(_editor.Document);
            SetStatus("تم تجهيز ملف DXF للمشاركة أو الحفظ");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("تعذر تصدير DXF", ex.Message, "موافق");
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  تحديث الواجهة
    // ═════════════════════════════════════════════════════════════

    private void SetStatus(string text) => StatusLabel.Text = text;

    private void RefreshStatus()
    {
        EntityCountLabel.Text = $"{_editor.Document.EntityCount} عنصر";
        ProjectNameLabel.Text = _editor.Document.Name;
        UndoButton.Opacity = _editor.CanUndo ? 1.0 : 0.4;
        RedoButton.Opacity = _editor.CanRedo ? 1.0 : 0.4;

        if (_editor.SelectedEntity is not null)
            SetStatus("عنصر محدد — اسحبه لتحريكه");

        RefreshSelectionUi();
    }

    private void RefreshSelectionUi()
    {
        RefreshSelectionBarVisibility();
        RefreshPropertiesPanel();
    }

    private void RefreshSelectionBarVisibility()
    {
        SelectionBar.IsVisible = !_isWide &&
                                 !_sheetOpen &&
                                 _editor.SelectedEntity is not null &&
                                 _editor.ActiveTool == CadTool.Select;
    }

    private void RefreshCanvas()
    {
        ZoomLabel.Text = $"{Math.Round(_camera.Zoom * 100f)}%";
        CadView.Invalidate();
    }

#if WINDOWS
    // ═════════════════════════════════════════════════════════════
    //  Windows: عجلة الفأرة، الزر الأوسط، إحداثيات المؤشر، لوحة المفاتيح
    // ═════════════════════════════════════════════════════════════

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
        nativeView.KeyDown -= OnNativeKeyDown;
        nativeView.KeyDown += OnNativeKeyDown;
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

        // محاولة إعطاء الرسم التركيز ليستقبل مفاتيح Delete وEsc (إن كان العنصر يقبل التركيز).
        nativeView.Focus(Microsoft.UI.Xaml.FocusState.Pointer);

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
        var nativeView = (Microsoft.UI.Xaml.UIElement)sender;
        var position = e.GetCurrentPoint(nativeView).Position;

        if (!_isPanning)
        {
            ShowCoordinates(new PointF((float)position.X, (float)position.Y));
            return;
        }

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

    private void OnNativeKeyDown(
        object sender,
        Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case global::Windows.System.VirtualKey.Delete:
                OnDeleteClicked(this, EventArgs.Empty);
                e.Handled = true;
                break;
            case global::Windows.System.VirtualKey.Escape:
                SetActiveTool(CadTool.Select);
                e.Handled = true;
                break;
        }
    }
#endif
}
