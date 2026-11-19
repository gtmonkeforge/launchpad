using System;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace MonkeForge.Injector;

[BepInPlugin(Guid, "MonkeForge Injector", "2.0.0")]
public sealed class InjectorPlugin : BaseUnityPlugin
{
    public const string Guid = "org.monkeforge.injector";

    private InjectorClient _client;
    private GameObject _host;

    private void Awake()
    {
        var root = Path.GetDirectoryName(Application.dataPath);
        _client = new InjectorClient(root, "bepinex5", message => Logger.LogInfo(message), message => Logger.LogWarning(message));
        _client.Start();
    }

    private void Update()
    {
        foreach (ReceivedMod mod in _client.Drain())
        {
            try
            {
                Activate(mod);
            }
            catch (Exception ex)
            {
                Logger.LogError($"{mod.Name} {mod.Version} couldn't be loaded: {ex}");
            }
        }
    }

    private void Activate(ReceivedMod mod)
    {
        Assembly assembly = InjectorClient.Load(mod);
        var started = 0;

        if (_host == null)
        {
            _host = new GameObject("MonkeForge Injected Mods");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
        }

        foreach (Type type in InjectorClient.SafeTypes(assembly))
        {
            if (type.IsAbstract || !typeof(BaseUnityPlugin).IsAssignableFrom(type) || type.GetCustomAttributes(typeof(BepInPlugin), false).Length == 0)
                continue;

            _host.AddComponent(type);
            started++;
        }

        if (started == 0)
            Logger.LogWarning($"{mod.Name} {mod.Version} has no BepInEx plugin to start.");
        else
            Logger.LogInfo($"Loaded {mod.Name} {mod.Version}.");
    }
}
