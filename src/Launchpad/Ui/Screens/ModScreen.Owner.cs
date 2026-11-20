using System.Text.Json.Nodes;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

public sealed partial class ModScreen
{
    private bool _owner;

    private bool CanDonate => _mod.Bool("donations_enabled") && !_owner && Status == "approved";

    private void LoadOwner()
    {
        _owner = false;
        var authorId = _mod["author"].Str("id");
        var userId = _shell.User.Str("id");

        if (authorId == null || userId == null)
            return;

        if (authorId == userId)
        {
            _owner = true;
            return;
        }

        var request = _request;

        _ = Task.Run(async () =>
        {
            try
            {
                var groups = (await _shell.App.Forge.GetJsonAsync("/api/v1/groups"))["items"] as JsonArray ?? [];

                if (request == _request && groups.Any(group => group.Str("id") == authorId))
                {
                    _owner = true;
                    _shell.Invalidate();
                }
            }
            catch (UserFacingException)
            {
            }
        });
    }
}
