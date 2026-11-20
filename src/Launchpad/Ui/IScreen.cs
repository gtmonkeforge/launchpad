namespace Launchpad.Ui;

using Ui = GoomeSharp.Ui;

public interface IScreen
{
    void Draw(Ui ui, double width, double height);

    void Reload();

    void Leave()
    {
    }

    void Dispose()
    {
    }
}
