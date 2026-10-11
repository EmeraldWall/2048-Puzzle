using Microsoft.Maui.Controls.Shapes;

namespace Puzzle2048.App.Controls;

/// <summary>
/// A chunky candy-style button: a gradient face on a darker lip that sinks when pressed.
/// Put any content inside, or set <see cref="Text"/> for a plain label. Set <see cref="Interactive"/> to false for a display panel.
/// </summary>
[ContentProperty(nameof(Inner))]
public class CandyButton : ContentView
{
    public static readonly BindableProperty FaceTopProperty = Prop(nameof(FaceTop), Color.FromArgb("#FFB04D"));
    public static readonly BindableProperty FaceBottomProperty = Prop(nameof(FaceBottom), Color.FromArgb("#FF6B6B"));
    public static readonly BindableProperty LipColorProperty = Prop(nameof(LipColor), Color.FromArgb("#C93A4A"));
    public static readonly BindableProperty CornerRadiusProperty = Prop(nameof(CornerRadius), 22.0);
    public static readonly BindableProperty LipHeightProperty = Prop(nameof(LipHeight), 7.0);
    public static readonly BindableProperty TextProperty = Prop(nameof(Text), (string?)null);
    public static readonly BindableProperty FontSizeProperty = Prop(nameof(FontSize), 22.0);
    public static readonly BindableProperty TextColorProperty = Prop(nameof(TextColor), Colors.White);

    private readonly Border _lip = new() { StrokeThickness = 0 };
    private readonly Border _face = new() { StrokeThickness = 0 };
    private readonly Grid _root = new();
    private readonly Label _label = new()
    {
        FontFamily = "FredokaBold",
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
    };
    private bool _pressing;

    public CandyButton()
    {
        _root.Children.Add(_lip);
        _root.Children.Add(_face);
        base.Content = _root;

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnTapped;
        GestureRecognizers.Add(tap);

        Apply();
    }

    public event EventHandler? Clicked;

    public Color FaceTop { get => (Color)GetValue(FaceTopProperty); set => SetValue(FaceTopProperty, value); }
    public Color FaceBottom { get => (Color)GetValue(FaceBottomProperty); set => SetValue(FaceBottomProperty, value); }
    public Color LipColor { get => (Color)GetValue(LipColorProperty); set => SetValue(LipColorProperty, value); }
    public double CornerRadius { get => (double)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public double LipHeight { get => (double)GetValue(LipHeightProperty); set => SetValue(LipHeightProperty, value); }
    public string? Text { get => (string?)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public Color TextColor { get => (Color)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

    /// <summary>False for panels that only display something, such as the score boxes.</summary>
    public bool Interactive { get; set; } = true;

    /// <summary>The content shown on the face of the button.</summary>
    public View? Inner
    {
        get => _face.Content;
        set => _face.Content = value;
    }

    private static BindableProperty Prop<T>(string name, T defaultValue) =>
        BindableProperty.Create(name, typeof(T), typeof(CandyButton), defaultValue,
            propertyChanged: (bindable, _, _) => ((CandyButton)bindable).Apply());

    private void Apply()
    {
        var radius = new RoundRectangle { CornerRadius = new CornerRadius(CornerRadius) };
        _lip.StrokeShape = radius;
        _lip.BackgroundColor = LipColor;
        _lip.Margin = new Thickness(0, LipHeight - 1, 0, 0);

        _face.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(CornerRadius) };
        _face.Background = new LinearGradientBrush(
            [new GradientStop(FaceTop, 0f), new GradientStop(FaceBottom, 1f)],
            new Point(0.5, 0), new Point(0.5, 1));
        _face.Margin = new Thickness(0, 0, 0, LipHeight);

        if (!string.IsNullOrEmpty(Text))
        {
            _label.Text = Text;
            _label.FontSize = FontSize;
            _label.TextColor = TextColor;
            _face.Content = _label;
        }
    }

    private async void OnTapped(object? sender, TappedEventArgs e)
    {
        if (!Interactive || _pressing)
            return;

        _pressing = true;
        Services.AppServices.Sound.Play(Services.Sfx.Tap);
        try
        {
            await _face.TranslateToAsync(0, LipHeight - 1, 50);
            Clicked?.Invoke(this, EventArgs.Empty);
            await _face.TranslateToAsync(0, 0, 90);
        }
        finally
        {
            _pressing = false;
        }
    }
}
