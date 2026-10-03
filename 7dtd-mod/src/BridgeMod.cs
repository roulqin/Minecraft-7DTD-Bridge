using System;
using System.IO;

namespace MC7DTD
{
    public sealed class BridgeMod : IModApi
    {
        private BridgeClient client;
        public void InitMod(Mod mod)
        {
            try
            {
                var root = Environment.GetEnvironmentVariable("MC7DTD_ROOT") ?? @"D:\wenjian\minecraft\7-M";
                client = new BridgeClient(Path.Combine(root, "config", "network.json"), message => Log.Out("[MC7DTD] " + message));
                ModEvents.GameShutdown.RegisterHandler(OnShutdown);
                client.Start();
            }
            catch (Exception ex) { Log.Error("[MC7DTD] Initialization failed: " + ex.Message); }
        }
        private void OnShutdown(ref ModEvents.SGameShutdownData data) { client?.Dispose(); }
    }
}
