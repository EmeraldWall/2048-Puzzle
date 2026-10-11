using Microsoft.Maui.Graphics;

namespace Puzzle2048.Rendering;

public enum IconKind
{
    Back,
    Restart,
    Undo,
    Trash,
    Home,
    Play,
    Share,
}

/// <summary>Vector icons drawn inside a square, so they stay crisp at any screen density.</summary>
public static class Icons
{
    public static void Draw(ICanvas canvas, IconKind kind, RectF box, Color color)
    {
        float s = MathF.Min(box.Width, box.Height);
        float cx = box.Center.X;
        float cy = box.Center.Y;
        float stroke = s * 0.15f;

        canvas.SaveState();
        canvas.StrokeColor = color;
        canvas.FillColor = color;
        canvas.StrokeSize = stroke;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        switch (kind)
        {
            case IconKind.Back:
            {
                var path = new PathF();
                path.MoveTo(cx + s * 0.14f, cy - s * 0.32f);
                path.LineTo(cx - s * 0.18f, cy);
                path.LineTo(cx + s * 0.14f, cy + s * 0.32f);
                canvas.DrawPath(path);
                break;
            }

            case IconKind.Restart:
                ArcArrow(canvas, cx, cy, s * 0.30f, startDeg: -60, sweepDeg: 290, headAtEnd: true, stroke, color);
                break;

            case IconKind.Undo:
                ArcArrow(canvas, cx + s * 0.04f, cy + s * 0.06f, s * 0.28f, startDeg: 190, sweepDeg: 220, headAtEnd: false, stroke, color);
                break;

            case IconKind.Trash:
            {
                float w = s * 0.46f;
                var body = new RectF(cx - w / 2f, cy - s * 0.12f, w, s * 0.46f);
                canvas.FillRoundedRectangle(body, s * 0.07f);
                canvas.FillRoundedRectangle(new RectF(cx - s * 0.33f, cy - s * 0.27f, s * 0.66f, s * 0.11f), s * 0.05f);
                canvas.FillRoundedRectangle(new RectF(cx - s * 0.11f, cy - s * 0.37f, s * 0.22f, s * 0.12f), s * 0.05f);
                canvas.FillColor = Colors.Black.WithAlpha(0.22f);
                for (int i = -1; i <= 1; i++)
                    canvas.FillRoundedRectangle(new RectF(cx + i * s * 0.12f - s * 0.025f, body.Y + s * 0.08f, s * 0.05f, body.Height - s * 0.16f), s * 0.025f);
                break;
            }

            case IconKind.Home:
            {
                var roof = new PathF();
                roof.MoveTo(cx - s * 0.34f, cy - s * 0.02f);
                roof.LineTo(cx, cy - s * 0.34f);
                roof.LineTo(cx + s * 0.34f, cy - s * 0.02f);
                canvas.DrawPath(roof);
                canvas.FillRoundedRectangle(new RectF(cx - s * 0.22f, cy - s * 0.06f, s * 0.44f, s * 0.36f), s * 0.06f);
                break;
            }

            case IconKind.Play:
            {
                var tri = new PathF();
                tri.MoveTo(cx - s * 0.16f, cy - s * 0.26f);
                tri.LineTo(cx + s * 0.26f, cy);
                tri.LineTo(cx - s * 0.16f, cy + s * 0.26f);
                tri.Close();
                canvas.FillPath(tri);
                canvas.DrawPath(tri);
                break;
            }

            case IconKind.Share:
            {
                PointF a = new(cx - s * 0.22f, cy), b = new(cx + s * 0.2f, cy - s * 0.22f), c = new(cx + s * 0.2f, cy + s * 0.22f);
                canvas.StrokeSize = stroke * 0.75f;
                canvas.DrawLine(a, b);
                canvas.DrawLine(a, c);
                foreach (PointF p in new[] { a, b, c })
                    canvas.FillCircle(p, s * 0.11f);
                break;
            }
        }

        canvas.RestoreState();
    }

    /// <summary>An arc with an arrowhead. Angles in degrees, 0 = 3 o'clock, growing clockwise on screen.</summary>
    private static void ArcArrow(ICanvas canvas, float cx, float cy, float r, float startDeg, float sweepDeg, bool headAtEnd, float stroke, Color color)
    {
        var path = new PathF();
        int steps = 36;
        for (int i = 0; i <= steps; i++)
        {
            float deg = startDeg + sweepDeg * i / steps;
            float rad = deg * MathF.PI / 180f;
            float x = cx + r * MathF.Cos(rad);
            float y = cy + r * MathF.Sin(rad);
            if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
        }
        canvas.DrawPath(path);

        float tDeg = headAtEnd ? startDeg + sweepDeg : startDeg;
        float tRad = tDeg * MathF.PI / 180f;
        var tip = new PointF(cx + r * MathF.Cos(tRad), cy + r * MathF.Sin(tRad));
        // Direction of travel along the arc at that end
        float dx = -MathF.Sin(tRad), dy = MathF.Cos(tRad);
        if (!headAtEnd) { dx = -dx; dy = -dy; }

        float head = stroke * 2.1f;
        var front = new PointF(tip.X + dx * head * 0.75f, tip.Y + dy * head * 0.75f);
        var left = new PointF(tip.X - dy * head * 0.8f, tip.Y + dx * head * 0.8f);
        var right = new PointF(tip.X + dy * head * 0.8f, tip.Y - dx * head * 0.8f);
        var tri = new PathF();
        tri.MoveTo(front);
        tri.LineTo(left);
        tri.LineTo(right);
        tri.Close();
        canvas.FillColor = color;
        canvas.FillPath(tri);
    }
}
