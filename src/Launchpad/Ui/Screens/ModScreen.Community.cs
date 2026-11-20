using System.Globalization;
using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen
{
    private const double CommentIndent = 42;

    private int _hoverStars;

    private bool CommunityOpen => Status == "approved";

    private static double CommentHeight(JsonNode? comment, double width) => 40 + WrapLines(Fonts.Main, 14, comment.Str("body") ?? "", width - CommentIndent, 40).Count * 21 + 16;

    private double DrawCommunity(Ui ui, double left, double width, double y)
    {
        var inner = width - 44;
        var items = _comments?["items"] as JsonArray ?? [];
        var signedIn = _shell.SignedIn && CommunityOpen;
        var heights = items.Select(item => CommentHeight(item, inner)).ToList();
        var composeHeight = signedIn ? 150.0 : 36.0;
        var listHeight = items.Count == 0 ? 36 : heights.Sum();
        var pagesHeight = CommentPages > 1 ? 48.0 : 0;
        var height = 22 + 70 + (signedIn ? 52 : 0) + 46 + composeHeight + listHeight + pagesHeight + 14;

        Item box = Area(ui, "mod-community", left, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, box.X, box.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        var x = left + 22;
        var top = y + 22;

        DrawRatingSummary(c, box.X + 22, box.Y + 22);
        top += 70;

        if (signedIn)
        {
            DrawRatingControls(ui, x, top);
            top += 52;
        }

        c = ui.Canvas()!;
        Chrome.BoldText(c, Fonts.Main, 17, _ox + x, _oy + top + 14, "Comments", Palette.Text);
        top += 46;
        top = DrawCompose(ui, x, top, inner, signedIn);

        if (items.Count == 0)
        {
            Item empty = Area(ui, "mod-no-comments", x, top, inner, 30);
            Chrome.Text(ui.Canvas()!, Fonts.Main, 14, empty.X, empty.Y + 15, _comments == null ? "Loading…" : "No comments yet.", Palette.Muted);
        }

        var rowY = top;

        for (var i = 0; i < items.Count; i++)
        {
            DrawComment(ui, items[i], i, x, rowY, inner);
            rowY += heights[i];
        }

        top += listHeight;

        if (pagesHeight > 0)
            DrawCommentPages(ui, x, top + 6, inner);

        return y + height;
    }

    private void DrawRatingSummary(Canvas c, double x, double y)
    {
        if (_ratings == null)
            return;

        if (_ratings.Long("count") == 0)
        {
            Chrome.Text(c, Fonts.Main, 14, x, y + 20, "No ratings yet. Be the first.", Palette.Muted);
            return;
        }

        var average = _ratings["average"]?.GetValue<double>() ?? 0;
        var count = _ratings.Long("count");
        var big = average.ToString("0.0", CultureInfo.InvariantCulture);
        var bigWidth = Chrome.BoldText(c, Fonts.Mono, 34, x, y + 22, big, Palette.Text);
        var filled = (int)Math.Round(average);

        for (var n = 1; n <= 5; n++)
            DrawStar(c, x + bigWidth + 18 + 10 + (n - 1) * 24, y + 12, 10, n <= filled ? Palette.Warn : Palette.LineStrong);

        Chrome.Text(c, Fonts.Mono, 12.5, x + bigWidth + 18, y + 40, $"{count} rating{(count == 1 ? "" : "s")}", Palette.Muted);
    }

    private static void DrawStar(Canvas c, double centerX, double centerY, double radius, uint color)
    {
        var points = new double[20];

        for (var i = 0; i < 10; i++)
        {
            var angle = -Math.PI / 2 + i * Math.PI / 5;
            var length = i % 2 == 0 ? radius : radius * 0.42;
            points[i * 2] = centerX + Math.Cos(angle) * length;
            points[i * 2 + 1] = centerY + Math.Sin(angle) * length;
        }

        c.DrawPoly(points, color);
    }

    private void DrawRatingControls(Ui ui, double x, double y)
    {
        var mine = (int)_ratings.Long("mine");
        var hovered = false;
        Item label = Area(ui, "mod-rate-label", x, y, 90, 30);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 14, label.X, label.Y + 15, "Your rating", Palette.Muted);

        for (var n = 1; n <= 5; n++)
        {
            Item star = Chrome.Hit(ui, "mod-star-" + n, x + 100 + (n - 1) * 32, y, 32, 30);
            Canvas c = ui.Canvas()!;

            if (star.Hovered)
            {
                _hoverStars = n;
                hovered = true;
            }

            var lit = n <= (_hoverStars > 0 && hovered ? _hoverStars : mine);
            DrawStar(c, star.X + 16, star.Y + 15, 11, lit ? Palette.Warn : Palette.LineStrong);

            if (star.Clicked)
                Rate(n);
        }

        if (!hovered)
            _hoverStars = 0;

        if (mine > 0 && Button(ui, "mod-rate-remove", "Remove", x + 276, y, 80, 30, ButtonStyle.Plain))
            RemoveRating();
    }

    private double DrawCompose(Ui ui, double x, double y, double width, bool signedIn)
    {
        if (!signedIn)
        {
            Item hint = Chrome.Hit(ui, "mod-compose-hint", x, y, width, 30);
            Canvas c = ui.Canvas()!;

            if (!CommunityOpen)
                Chrome.Text(c, Fonts.Main, 14, hint.X, hint.Y + 15, "Ratings and comments open after approval.", Palette.Muted);
            else
            {
                var link = Chrome.Text(c, Fonts.Main, 14, hint.X, hint.Y + 15, "Sign in", hint.Hovered ? Palette.AccentHot : Palette.Accent);
                Chrome.Text(c, Fonts.Main, 14, hint.X + link, hint.Y + 15, " to rate and comment.", Palette.Muted);

                if (hint.Clicked)
                    _shell.Router.Push(new Route(Page.SignIn));
            }

            return y + 36;
        }

        ui.Pos(x, y);
        ui.Width(width);
        ui.TextEdit("##mod-comment", ref _commentText, 90, EditFlags.Wrap, 3000);

        if (_commentText.Length == 0)
            Chrome.Text(ui.Canvas()!, Fonts.Main, 14, _ox + x + 10, _oy + y + 18, "Share feedback, tips or bug reports…", Palette.Faint);

        var buttonWidth = Widgets.ButtonWidth("Post comment");
        var ready = _commentText.Trim().Length > 0 && !_posting;

        if (Widgets.Button(ui, "mod-comment-post", x + width - buttonWidth, y + 100, "Post comment", ButtonKind.Primary, out _, ready))
            PostComment();

        return y + 150;
    }

    private void DrawComment(Ui ui, JsonNode? comment, int index, double x, double y, double width)
    {
        Item row = Area(ui, "mod-comment-" + index, x, y, width, 1);
        Canvas c = ui.Canvas()!;
        var author = comment?["author"];
        var name = author.Str("name") ?? "";

        if (index > 0)
            c.DrawRect(row.X, row.Y, width, 1, Palette.Line);

        Bitmap? avatar = _shell.Images.Get(author.Str("avatar"));

        if (avatar != null)
            c.DrawBitmap(avatar, row.X, row.Y + 14, 30, 30, 15);
        else
        {
            c.DrawCircle(row.X + 15, row.Y + 29, 15, Palette.Surface2);
            var letters = Format.Initials(name);
            Chrome.Text(c, Fonts.Main, 11, row.X + 15 - Fonts.Main.Measure(11, letters) / 2, row.Y + 29, letters, Palette.Muted);
        }

        var nameWidth = Chrome.BoldText(c, Fonts.Main, 14, row.X + CommentIndent, row.Y + 22, name, Palette.Text);
        Chrome.Text(c, Fonts.Main, 12.5, row.X + CommentIndent + nameWidth + 10, row.Y + 22, Format.Ago(ParseDate(comment.Str("created_at"))), Palette.Faint);
        var lines = WrapLines(Fonts.Main, 14, comment.Str("body") ?? "", width - CommentIndent, 40);

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 14, row.X + CommentIndent, row.Y + 48 + i * 21, lines[i], Palette.Muted);

        if (!comment.Bool("can_delete"))
            return;

        Item trash = Chrome.Hit(ui, "mod-comment-delete-" + index, x + width - 34, y + 8, 30, 28);
        c = ui.Canvas()!;
        Icons.Trash(c, trash.X + 15, trash.Y + 14, trash.Hovered ? Palette.Bad : Palette.Faint);

        if (trash.Clicked)
        {
            _deleteId = comment.Long("id");
            _deleteOpen = true;
        }
    }

    private void DrawCommentPages(Ui ui, double x, double y, double width)
    {
        var label = $"{_commentPage} / {CommentPages}";
        var center = x + width / 2;
        Item mid = Area(ui, "mod-comment-page", center - 40, y, 80, 34);
        Chrome.Text(ui.Canvas()!, Fonts.Mono, 13, mid.X + 40 - Fonts.Mono.Measure(13, label) / 2, mid.Y + 17, label, Palette.Muted);

        if (Button(ui, "mod-comments-newer", "Newer", center - 40 - 8 - 80, y, 80, 34, ButtonStyle.Plain, _commentPage > 1))
        {
            _commentPage--;
            LoadComments();
        }

        if (Button(ui, "mod-comments-older", "Older", center + 40 + 8, y, 80, 34, ButtonStyle.Plain, _commentPage < CommentPages))
        {
            _commentPage++;
            LoadComments();
        }
    }
}
