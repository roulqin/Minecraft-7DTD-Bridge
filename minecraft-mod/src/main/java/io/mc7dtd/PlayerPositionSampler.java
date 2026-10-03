package io.mc7dtd;

import net.fabricmc.fabric.api.client.event.lifecycle.v1.ClientTickEvents;

/** Read coordinates on the Minecraft client thread; network writes run separately. */
public final class PlayerPositionSampler {
    private static final long INTERVAL_NANOS = 500_000_000L;
    private long nextSample;
    private PlayerPositionSampler() { }

    public static void register(BridgeClient bridge) {
        var sampler = new PlayerPositionSampler();
        ClientTickEvents.END_CLIENT_TICK.register(client -> {
            if (client.player == null || client.world == null || client.isPaused()) {
                sampler.nextSample = 0;
                return;
            }
            long now = System.nanoTime();
            if (sampler.nextSample != 0 && now - sampler.nextSample < 0) return;
            sampler.nextSample = now + INTERVAL_NANOS;
            bridge.publishPlayerPosition(client.player.getX(), client.player.getY(), client.player.getZ());
        });
    }
}
