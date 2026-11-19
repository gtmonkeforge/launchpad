using System;
using MelonLoader;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(MonkeForge.Injector.InjectorPlugin), "MonkeForge Injector", "2.0.0", "MonkeForge")]
[assembly: MelonGame("Another Axiom", "Gorilla Tag")]

namespace MonkeForge.Injector;

public sealed class InjectorPlugin : MelonPlugin
{
    private InjectorClient _client;

    public override void OnPreInitialization()
    {
        _client = new InjectorClient(MelonEnvironment.GameRootDirectory, "melonloader", message => LoggerInstance.Msg(message), message => LoggerInstance.Warning(message));
        _client.Start();
    }

    public override void OnUpdate()
    {
        if (_client == null)
            return;

        foreach (ReceivedMod mod in _client.Drain())
        {
            try
            {
                MelonAssembly melon = MelonAssembly.LoadRawMelonAssembly("MonkeForge/" + mod.Guid + ".dll", mod.Dll, null, true);
                Array.Clear(mod.Dll, 0, mod.Dll.Length);

                if (melon == null || melon.LoadedMelons.Count == 0)
                {
                    LoggerInstance.Warning($"{mod.Name} {mod.Version} has no MelonLoader mod to start.");
                    continue;
                }

                MelonBase.RegisterSorted(melon.LoadedMelons);
                LoggerInstance.Msg($"Loaded {mod.Name} {mod.Version}.");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"{mod.Name} {mod.Version} couldn't be loaded: {ex}");
            }
        }
    }
}
