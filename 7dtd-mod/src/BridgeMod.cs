using System;
using System.IO;

namespace MC7DTD
{
    public sealed class BridgeMod : IModApi
    {
        private BridgeClient client;
        private MarkerController markers;
        private MarkerController players;
        private bool worldWasReady;
        public void InitMod(Mod mod)
        {
            try
            {
                var root = Environment.GetEnvironmentVariable("MC7DTD_ROOT") ?? @"D:\wenjian\minecraft\7-M";
                Action<string> logger = message => Log.Out("[MC7DTD] " + message);
                markers = new MarkerController(new UnityMarkerScene(), logger);
                players = new MarkerController(new UnityPlayerProxyScene(), logger, "minecraft:player", "Player proxy");
                client = new BridgeClient(Path.Combine(root, "config", "network.json"), logger,
                    message => { markers.Enqueue(message); players.Enqueue(message); },
                    () => { markers.Reset(); players.Reset(); });
                ModEvents.GameUpdate.RegisterHandler(OnUpdate);
                ModEvents.WorldShuttingDown.RegisterHandler(OnWorldShutdown);
                ModEvents.GameShutdown.RegisterHandler(OnShutdown);
                client.Start();
            }
            catch (Exception ex) { Log.Error("[MC7DTD] Initialization failed: " + ex.Message); }
        }
        private void OnUpdate(ref ModEvents.SGameUpdateData data)
        {
            var ready = GameManager.Instance?.World?.GetPrimaryPlayer() != null;
            if (ready && !worldWasReady) client?.RequestPlayerResync();
            worldWasReady = ready;
            markers?.Tick(ready); players?.Tick(ready);
        }
        private void OnWorldShutdown(ref ModEvents.SWorldShuttingDownData data)
        { worldWasReady = false; markers?.Reset(); markers?.Tick(false); players?.Reset(); players?.Tick(false); }
        private void OnShutdown(ref ModEvents.SGameShutdownData data)
        { client?.Dispose(); markers?.Reset(); markers?.Tick(false); players?.Reset(); players?.Tick(false); }
    }
}
