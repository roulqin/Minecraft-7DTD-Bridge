package io.mc7dtd;

import com.google.gson.JsonNull;
import com.google.gson.JsonObject;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicLong;
import net.fabricmc.fabric.api.client.command.v2.ClientCommandRegistrationCallback;
import net.fabricmc.fabric.api.client.command.v2.FabricClientCommandSource;
import net.minecraft.text.Text;
import static net.fabricmc.fabric.api.client.command.v2.ClientCommandManager.literal;

/** Opt-in manual wire acceptance probes, not an entity lifecycle state machine. */
public final class EntityTestCommands {
    private final String stream = UUID.randomUUID().toString();
    private final AtomicLong sequence = new AtomicLong();
    private final String markerId = UUID.randomUUID().toString();
    private final AtomicLong markerSequence = new AtomicLong();
    private final BridgeClient bridge;
    private EntityTestCommands(BridgeClient bridge) { this.bridge = bridge; }
    public static void register(BridgeClient bridge) {
        var commands = new EntityTestCommands(bridge);
        ClientCommandRegistrationCallback.EVENT.register((dispatcher, registryAccess) -> {
            var root = literal("mc7dtd_entity_test");
            for (String event : new String[] { "spawn", "update", "despawn" })
                root.then(literal(event).executes(context -> commands.emit(context.getSource(), event, false)));
            dispatcher.register(root);
            var marker = literal("mc7dtd_marker_test");
            for (String event : new String[] { "spawn", "update", "despawn" })
                marker.then(literal(event).executes(context -> commands.emit(context.getSource(), event, true)));
            dispatcher.register(marker);
        });
    }
    private int emit(FabricClientCommandSource source, String event, boolean marker) {
        var client = source.getClient();
        var player = client.player;
        if (player == null || client.world == null) {
            source.sendError(Text.literal("Entity test requires an active Minecraft world")); return 0;
        }
        var message = new JsonObject();
        message.addProperty("type", "entity_state"); message.addProperty("version", 1);
        message.addProperty("source", "minecraft"); message.addProperty("stream_id", stream);
        message.addProperty("entity_id", marker ? markerId : player.getUuid().toString());
        message.addProperty("entity_type", marker ? "minecraft:marker" : "minecraft:player");
        message.addProperty("world_id", "runtime-acceptance");
        message.addProperty("dimension", client.world.getRegistryKey().getValue().toString());
        message.addProperty("sequence", marker ? markerSequence.incrementAndGet() : sequence.incrementAndGet());
        var lifecycle = new JsonObject(); lifecycle.addProperty("event", event);
        var metadata = new JsonObject();
        if (event.equals("despawn")) {
            lifecycle.addProperty("reason", "out_of_scope");
            message.add("position", JsonNull.INSTANCE); message.add("rotation", JsonNull.INSTANCE);
        } else {
            var position = new JsonObject();
            position.addProperty("x", player.getX()); position.addProperty("y", player.getY()); position.addProperty("z", player.getZ());
            position.addProperty("space", "minecraft"); message.add("position", position);
            var rotation = new JsonObject();
            rotation.addProperty("yaw", ((player.getYaw() % 360) + 360) % 360);
            rotation.addProperty("pitch", player.getPitch()); rotation.addProperty("roll", 0);
            message.add("rotation", rotation);
            if (!marker) {
                metadata.addProperty("health", player.getHealth()); metadata.addProperty("max_health", player.getMaxHealth());
                metadata.addProperty("is_alive", player.isAlive());
            }
        }
        message.add("lifecycle", lifecycle); message.add("metadata", metadata);
        boolean queued = bridge.publishEntityTest(message);
        if (queued) source.sendFeedback(Text.literal("Entity test queued: " + event + " (check Bridge and 7DTD logs)"));
        else source.sendError(Text.literal("Entity test not queued: Bridge disconnected or test queue full"));
        return queued ? 1 : 0;
    }
}
