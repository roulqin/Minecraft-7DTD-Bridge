package io.mc7dtd;

import java.nio.charset.StandardCharsets;
import java.util.UUID;
import net.fabricmc.fabric.api.client.event.lifecycle.v1.ClientTickEvents;
import net.fabricmc.fabric.api.client.event.lifecycle.v1.ClientLifecycleEvents;
import net.minecraft.util.WorldSavePath;

public final class PlayerProxySampler {
    public static void register(BridgeClient bridge) {
        var tracker = new PlayerProxyTracker(bridge::publishPlayerEntity);
        ClientLifecycleEvents.CLIENT_STOPPING.register(client ->
            tracker.tick(null, true, bridge.entityEpoch(), System.nanoTime()));
        ClientTickEvents.END_CLIENT_TICK.register(client -> {
            PlayerProxyTracker.Sample sample = null;
            if (client.player != null && client.world != null && client.player.isAlive()) {
                String worldKey = client.getServer() != null
                    ? client.getServer().getSavePath(WorldSavePath.ROOT).toAbsolutePath().normalize().toString()
                    : client.getCurrentServerEntry() != null ? client.getCurrentServerEntry().address : "remote-session";
                String world = "mc-" + UUID.nameUUIDFromBytes(worldKey.getBytes(StandardCharsets.UTF_8));
                sample = new PlayerProxyTracker.Sample(client.player, world,
                    client.world.getRegistryKey().getValue().toString(), client.player.getX(), client.player.getY(),
                    client.player.getZ(), client.player.getYaw(), client.player.getPitch());
            }
            tracker.tick(sample, client.isPaused(), bridge.entityEpoch(), System.nanoTime());
        });
    }
}
