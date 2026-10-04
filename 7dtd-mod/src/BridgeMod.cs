using System;
using System.IO;

namespace MC7DTD
{
    public sealed class BridgeMod : IModApi
    {
        private BridgeClient client;
        private MarkerController markers;
        private MarkerController players;
        private AvatarRenderer avatars;
        private bool worldWasReady;
        private NativePlayerPublisher nativePlayer;
        private object nativeIdentity;
        private string nativeWorldId;
        private bool nativeWorldClosing;
        private IdentityLabels identityLabels;
        public void InitMod(Mod mod)
        {
            try
            {
                var root = Environment.GetEnvironmentVariable("MC7DTD_ROOT") ?? @"D:\wenjian\minecraft\7-M";
                Action<string> logger = message => Log.Out("[MC7DTD] " + message);
                var debugConfig = DebugConfig.Load(Path.Combine(root,"config","debug.json"),logger);
                markers = new MarkerController(new DebugProxyScene(new UnityMarkerScene(),debugConfig,logger), logger, debug:new EntityDebugLog(debugConfig,logger));
                avatars = new AvatarRenderer(root, logger);
                AvatarRenderer.Current = avatars;
                players = new MarkerController(new DebugProxyScene(new AvatarProxyScene(avatars),debugConfig,logger), logger, "minecraft:player", "Player proxy", new EntityDebugLog(debugConfig,logger));
                client = new BridgeClient(Path.Combine(root, "config", "network.json"), logger,
                    message => { markers.Enqueue(message); players.Enqueue(message); },
                    () => { markers.Reset(); players.Reset(); });
                ModEvents.GameUpdate.RegisterHandler(OnUpdate);
                nativePlayer = new NativePlayerPublisher(() => client.NativeEpoch, client.PublishNative, client.PublishHealth, client.PublishComponents);
                identityLabels = new IdentityLabels(Path.Combine(root,"config","identity_labels.json"),logger);
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
            var primary = GameManager.Instance?.World?.GetPrimaryPlayer();
            // WorldShuttingDown can precede disposal of the old primary player.
            if (nativeWorldClosing)
            {
                if (primary != null && ReferenceEquals(nativeIdentity, primary)) return;
                nativeWorldClosing = false;
            }
            if (primary != null)
            {
                // Hash the save name: no account identifiers or filesystem paths on the wire.
                if (!ReferenceEquals(nativeIdentity, primary))
                {
                    nativeIdentity = primary;
                    using (var hash = System.Security.Cryptography.SHA256.Create())
                        nativeWorldId = "td-" + BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(
                            GamePrefs.GetString(EnumGamePrefs.GameWorld) + ":" + GamePrefs.GetString(EnumGamePrefs.GameName)))).Replace("-", "").ToLowerInvariant();
                }
                var p = primary.position; var r = primary.rotation;
                // Convert Unity positive clockwise yaw to the protocol's shared yaw convention.
                nativePlayer?.Tick(primary, nativeWorldId, p.x, p.y, p.z, -r.y,
                    r.x > 180 ? r.x - 360 : r.x, r.z, DateTime.UtcNow, primary.Health, primary.GetMaxHealth(),
                    identityLabels.Sample(primary.EntityName,DateTime.UtcNow,primary.PlayerDisplayName),()=>EquipmentSample.Read(primary));
            }
            else { nativeIdentity = null; nativePlayer?.Leave("world_unloaded"); }
        }
        private void OnWorldShutdown(ref ModEvents.SWorldShuttingDownData data)
        { nativeWorldClosing = true; nativePlayer?.Leave("world_unloaded"); worldWasReady = false; markers?.Reset(); markers?.Tick(false); players?.Reset(); players?.Tick(false); }
        private void OnShutdown(ref ModEvents.SGameShutdownData data)
        { client?.Dispose(); markers?.Reset(); markers?.Tick(false); players?.Reset(); players?.Tick(false); avatars?.Dispose(); }
    }
}
