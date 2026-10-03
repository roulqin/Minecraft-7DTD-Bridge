package io.mc7dtd;

import com.google.gson.JsonNull;
import com.google.gson.JsonObject;
import java.util.UUID;

/** Client-thread lifecycle decisions; injectable sink keeps tests independent of Minecraft. */
public final class PlayerProxyTracker {
    public record Sample(Object identity, String world, String dimension,
        double x, double y, double z, double yaw, double pitch) { }
    public interface Sink { boolean send(JsonObject message, long epoch); }
    private final Sink sink;
    private Sample current;
    private long epoch, sequence, nextUpdate;
    private String id, stream;
    public PlayerProxyTracker(Sink sink) { this.sink = sink; }
    public void tick(Sample sample, boolean paused, long connection, long now) {
        if (connection == 0 || connection != epoch) {
            if (connection != 0 && current != null) {
                epoch = connection;
                if (!emit("despawn", current, "out_of_scope")) return;
            }
            current = null; epoch = connection; sequence = 0;
            stream = UUID.randomUUID().toString();
        }
        if (connection == 0) return;
        if (current != null && (sample == null || sample.identity() != current.identity() ||
                !sample.world().equals(current.world()) || !sample.dimension().equals(current.dimension()))) {
            if (!emit("despawn", current, sample == null ? "world_unloaded" : "dimension_changed")) return;
            current = null;
        }
        if (sample == null) return;
        if (current == null) {
            id = UUID.randomUUID().toString(); sequence = 0;
            if (emit("spawn", sample, null)) { current = sample; nextUpdate = now + 500_000_000L; }
        } else if (!paused && now - nextUpdate >= 0) {
            if (emit("update", sample, null)) { current = sample; nextUpdate = now + 500_000_000L; }
        }
    }
    private boolean emit(String event, Sample sample, String reason) {
        var message = new JsonObject();
        message.addProperty("type", "entity_state"); message.addProperty("version", 1);
        message.addProperty("source", "minecraft"); message.addProperty("entity_type", "minecraft:player");
        message.addProperty("entity_id", id); message.addProperty("stream_id", stream);
        message.addProperty("world_id", sample.world()); message.addProperty("dimension", sample.dimension());
        message.addProperty("sequence", ++sequence);
        var lifecycle = new JsonObject(); lifecycle.addProperty("event", event);
        if (reason != null) lifecycle.addProperty("reason", reason);
        message.add("lifecycle", lifecycle); message.add("metadata", new JsonObject());
        if (event.equals("despawn")) {
            message.add("position", JsonNull.INSTANCE); message.add("rotation", JsonNull.INSTANCE);
        } else {
            var position = new JsonObject();
            position.addProperty("x", sample.x()); position.addProperty("y", sample.y()); position.addProperty("z", sample.z());
            position.addProperty("space", "minecraft"); message.add("position", position);
            var rotation = new JsonObject();
            rotation.addProperty("yaw", ((sample.yaw() % 360) + 360) % 360);
            rotation.addProperty("pitch", sample.pitch()); rotation.addProperty("roll", 0);
            message.add("rotation", rotation);
        }
        return sink.send(message, epoch);
    }
}
