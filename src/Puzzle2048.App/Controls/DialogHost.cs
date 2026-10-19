using Microsoft.Maui.Controls.Shapes;
using Puzzle2048.App.Services;

namespace Puzzle2048.App.Controls;

/// <summary>Colour of a dialog's title ribbon.</summary>
public enum DialogTone
{
    Pink,
    Gold,
    Blue,
    Purple,
}

/// <summary>
/// A candy-style popup that replaces the system alert dialog: a glossy title ribbon over a rounded card,
/// a soft dimmed backdrop and a springy pop-in. Put one as the last child of a page's root grid.
/// </summary>
public sealed class DialogHost : Grid
{
    private readonly BoxView _scrim = new() { Color = Color.FromRgba(40, 20, 80, 150) };
    private readonly Grid _wrap = new()
    {
        VerticalOptions = LayoutOptions.Center,
        HorizontalOptions = LayoutOptions.Fill,
        MaximumWidthRequest = 400,
        Margin = new Thickness(22, 0),
    };
    private readonly CandyButton _ribbon = new()
    {
        Interactive = false,
        HeightRequest = 66,
        CornerRadius = 24,
        LipHeight = 7,
        FontSize = 26,
        VerticalOptions = LayoutOptions.Start,
    };
    private readonly Label _message = new()
    {
        FontFamily = "FredokaBold",
        FontSize = 18,
        TextColor = Color.FromArgb("#4B3A7C"),
        HorizontalTextAlignment = TextAlignment.Center,
        LineHeight = 1.15,
    };
    private readonly ScrollView _scroll = new();
    private readonly CandyButton _accept = new()
    {
        HeightRequest = 60,
        CornerRadius = 22,
        LipHeight = 7,
        FontSize = 21,
        FaceTop = Color.FromArgb("#8BE87F"),
        FaceBottom = Color.FromArgb("#36C25A"),
        LipColor = Color.FromArgb("#21934A"),
    };
    private readonly CandyButton _cancel = new()
    {
        HeightRequest = 54,
        CornerRadius = 22,
        LipHeight = 6,
        FontSize = 19,
        FaceTop = Color.FromArgb("#F4F0FF"),
        FaceBottom = Color.FromArgb("#DDD4FF"),
        LipColor = Color.FromArgb("#B6A8EA"),
        TextColor = Color.FromArgb("#6B5AA6"),
    };

    private TaskCompletionSource<bool>? _pending;

    public DialogHost()
    {
        IsVisible = false;
        ZIndex = 100;

        // Swallow taps so nothing underneath reacts while the dialog is open
        _scrim.GestureRecognizers.Add(new TapGestureRecognizer());

        var card = new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(30) },
            Padding = new Thickness(22, 52, 22, 20),
            Margin = new Thickness(14, 33, 14, 0),
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Color.FromRgba(40, 10, 90, 110)),
                Offset = new Point(0, 10),
                Radius = 24,
            },
        };
        card.GestureRecognizers.Add(new TapGestureRecognizer());

        var buttons = new VerticalStackLayout { Spacing = 10 };
        buttons.Children.Add(_accept);
        buttons.Children.Add(_cancel);

        var body = new VerticalStackLayout { Spacing = 20 };
        body.Children.Add(_scroll);
        body.Children.Add(buttons);
        card.Content = body;

        _wrap.Children.Add(card);
        _wrap.Children.Add(_ribbon);

        Children.Add(_scrim);
        Children.Add(_wrap);

        _accept.Clicked += (_, _) => Close(true);
        _cancel.Clicked += (_, _) => Close(false);
    }

    public bool IsOpen => _pending is not null;

    /// <summary>Shows the dialog and returns true for the accept button, false for cancel or back.</summary>
    /// <param name="longText">Smaller, left-aligned and scrollable, for text such as licences.</param>
    public async Task<bool> ShowAsync(string title, string message, string accept = "OK", string? cancel = null,
        DialogTone tone = DialogTone.Pink, bool longText = false)
    {
        _pending?.TrySetResult(false);
        var pending = new TaskCompletionSource<bool>();
        _pending = pending;

        ApplyTone(tone);
        _ribbon.Text = title;
        _accept.Text = accept;
        _cancel.Text = cancel;
        _cancel.IsVisible = cancel is not null;

        _scroll.Content = null;
        _message.Text = message;
        _message.FontFamily = longText ? null : "FredokaBold";
        _message.FontSize = longText ? 14 : 18;
        _message.HorizontalTextAlignment = longText ? TextAlignment.Start : TextAlignment.Center;
        _scroll.Content = _message;
        // The host is not laid out while hidden, so size long text from the page it covers
        double screen = (Parent as VisualElement)?.Height ?? 0;
        _scroll.HeightRequest = longText ? Math.Clamp(screen * 0.5, 220, 460) : -1;
        _ = _scroll.ScrollToAsync(0, 0, false);

        AppServices.Sound.Play(tone == DialogTone.Gold ? Sfx.Win : Sfx.Whoosh);

        _scrim.Opacity = 0;
        _wrap.Opacity = 0;
        _wrap.Scale = 0.7;
        IsVisible = true;
        await Task.WhenAll(
            _scrim.FadeToAsync(1, 180),
            _wrap.FadeToAsync(1, 120),
            _wrap.ScaleToAsync(1, 340, Easing.SpringOut));

        return await pending.Task;
    }

    /// <summary>Closes an open dialog as cancelled. Returns true when there was one, for the back button.</summary>
    public bool HandleBack()
    {
        if (!IsOpen)
            return false;
        Close(false);
        return true;
    }

    private async void Close(bool result)
    {
        var pending = _pending;
        if (pending is null)
            return;
        _pending = null;

        await Task.WhenAll(
            _scrim.FadeToAsync(0, 160),
            _wrap.FadeToAsync(0, 140),
            _wrap.ScaleToAsync(0.85, 140, Easing.CubicIn));
        if (_pending is null)
            IsVisible = false;
        pending.TrySetResult(result);
    }

    private void ApplyTone(DialogTone tone)
    {
        (string top, string bottom, string lip, string text) = tone switch
        {
            DialogTone.Gold => ("#FFEE7A", "#FFC21F", "#D49A00", "#7A4A00"),
            DialogTone.Blue => ("#7FCBFF", "#3E9CFF", "#1F6FCC", "#FFFFFF"),
            DialogTone.Purple => ("#BE94FF", "#8F5CF2", "#6034C2", "#FFFFFF"),
            _ => ("#FF8CC4", "#FF4F9A", "#C42872", "#FFFFFF"),
        };
        _ribbon.FaceTop = Color.FromArgb(top);
        _ribbon.FaceBottom = Color.FromArgb(bottom);
        _ribbon.LipColor = Color.FromArgb(lip);
        _ribbon.TextColor = Color.FromArgb(text);
    }
}
