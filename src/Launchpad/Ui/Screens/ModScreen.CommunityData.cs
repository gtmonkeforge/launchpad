using System.Globalization;
using System.Text.Json.Nodes;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

public sealed partial class ModScreen
{
    private const int CommentsPerPage = 20;

    private JsonNode? _ratings;
    private JsonNode? _comments;
    private int _commentPage = 1;
    private string _commentText = "";
    private bool _posting;
    private long _deleteId;
    private bool _deleteOpen;

    private string BasePath => "/api/v1/mods/" + Uri.EscapeDataString(_mod.Str("guid") ?? _guid);

    private int CommentPages => (int)Math.Max(1, (_comments.Long("total") + CommentsPerPage - 1) / CommentsPerPage);

    private void LoadCommunity()
    {
        _ratings = null;
        _comments = null;
        _commentPage = 1;
        _rating = "-";
        LoadRatings();
        LoadComments();
    }

    private void LoadRatings()
    {
        var request = _request;

        _ = Task.Run(async () =>
        {
            try
            {
                JsonNode result = await _shell.App.Forge.GetJsonAsync(BasePath + "/ratings");

                if (request == _request)
                {
                    _ratings = result;
                    _rating = result.Long("count") > 0 ? (result["average"]?.GetValue<double>() ?? 0).ToString("0.0", CultureInfo.InvariantCulture) + " ★" : "-";
                }
            }
            catch (UserFacingException)
            {
            }

            _shell.Invalidate();
        });
    }

    private void LoadComments()
    {
        var request = _request;
        var page = _commentPage;

        _ = Task.Run(async () =>
        {
            try
            {
                JsonNode result = await _shell.App.Forge.GetJsonAsync(BasePath + "/comments?page=" + page);

                if (request == _request && page == _commentPage)
                {
                    if (result["items"] is JsonArray { Count: 0 } && page > 1)
                    {
                        _commentPage--;
                        LoadComments();
                        return;
                    }

                    _comments = result;
                }
            }
            catch (UserFacingException)
            {
            }

            _shell.Invalidate();
        });
    }

    private void Rate(int stars) => Send(HttpMethod.Put, "/ratings", new { stars }, LoadRatings);

    private void RemoveRating() => Send(HttpMethod.Delete, "/ratings", null, LoadRatings);

    private void PostComment()
    {
        _posting = true;
        Send(HttpMethod.Post, "/comments", new { body = _commentText.Trim() }, () =>
        {
            _commentText = "";
            _commentPage = 1;
            LoadComments();
        }, () => _posting = false);
    }

    private void DeleteComment()
    {
        _deleteOpen = false;
        Send(HttpMethod.Delete, "/comments/" + _deleteId, null, LoadComments);
    }

    private void Send(HttpMethod method, string path, object? body, Action done, Action? always = null)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _shell.App.Forge.SendJsonAsync(method, BasePath + path, body);
                done();
            }
            catch (UserFacingException ex)
            {
                _shell.Toast(ex.Message);
            }

            always?.Invoke();
            _shell.Invalidate();
        });
    }
}
