package io.mc7dtd;

import com.google.gson.*;
import java.util.*;
import java.util.function.Consumer;

/** Client-thread projection of Bridge-authorized v2 records; no network sends. */
public final class NativeProxyController {
    public record Snapshot(String id, String stream, String type, String world, String dimension, long sequence,
        String event, double x, double y, double z, double yaw, double pitch, double roll) { }
    public interface Scene {
        Object create(Snapshot state);
        void update(Object handle, Snapshot state);
        void delete(Object handle);
    }
    private static final class Entry { Snapshot state; Object handle; boolean active; }
    private final Scene scene;
    private final ProxyTypeCatalog catalog;
    private final Consumer<String> log;
    private final ArrayDeque<JsonObject> pending = new ArrayDeque<>();
    private final Map<String, Entry> records = new HashMap<>();
    private boolean reset;
    private Object world;
    public NativeProxyController(Scene scene, ProxyTypeCatalog catalog, Consumer<String> log) {
        this.scene = scene; this.catalog = catalog; this.log = log;
    }
    public synchronized void enqueue(JsonObject message) {
        if (pending.size() >= 512) throw new IllegalStateException("Proxy queue full; fresh connection required");
        pending.add(message.deepCopy());
    }
    public synchronized void reset() { pending.clear(); reset = true; }
    public void tick(Object currentWorld) {
        List<JsonObject> messages; boolean clear;
        synchronized (this) { clear = reset; reset = false; messages = new ArrayList<>(pending); pending.clear(); }
        if (clear || world != currentWorld) {
            for (var entry : records.values()) { if (entry.handle != null) scene.delete(entry.handle); entry.handle = null; }
            if (clear) records.clear();
            world = currentWorld;
            log.accept("7DTD proxy reset: connection=" + clear + " worldReady=" + (world != null));
        }
        for (var message : messages) {
            try { apply(parse(message)); }
            catch (IllegalArgumentException | IllegalStateException | NullPointerException ex) { log.accept("7DTD proxy rejected: " + ex.getMessage()); }
        }
        if (world != null) for (var entry : records.values()) if (entry.active && entry.handle == null) {
            entry.handle = scene.create(entry.state);
            scene.update(entry.handle, entry.state);
            log.accept("7DTD proxy spawned: id=" + entry.state.id + " sequence=" + entry.state.sequence + " count=" + activeCount());
        }
    }
    private void apply(Snapshot state) {
        if (!catalog.supports(state.type)) { log.accept("7DTD proxy unsupported type: " + state.type); return; }
        String key = state.world + "|" + state.dimension + "|" + state.id;
        var entry = records.get(key);
        if (entry != null) {
            if (!entry.state.type.equals(state.type) || !entry.state.stream.equals(state.stream)) throw new IllegalArgumentException("identity changed");
            if (state.sequence <= entry.state.sequence) return;
        }
        if (state.event.equals("spawn") && entry != null) return;
        if (state.event.equals("update") && (entry == null || !entry.active)) throw new IllegalArgumentException("unknown update");
        if (entry == null) {
            if (records.size() >= 256) throw new IllegalArgumentException("proxy registry full");
            entry = new Entry(); records.put(key, entry);
        }
        entry.state = state; entry.active = !state.event.equals("despawn");
        if (!entry.active) {
            if (entry.handle != null) scene.delete(entry.handle);
            entry.handle = null;
            log.accept("7DTD proxy despawned: id=" + state.id + " sequence=" + state.sequence + " count=" + activeCount());
        } else if (entry.handle != null) {
            scene.update(entry.handle, state);
            log.accept("7DTD proxy updated: id=" + state.id + " sequence=" + state.sequence + " x=" + state.x + " y=" + state.y + " z=" + state.z
                + " yaw=" + state.yaw + " pitch=" + state.pitch + " roll=" + state.roll);
        }
    }
    public int activeCount() { return (int) records.values().stream().filter(entry -> entry.active).count(); }
    private static Snapshot parse(JsonObject message) {
        var origin = message.getAsJsonObject("origin"); String id = message.get("entity_id").getAsString();
        String stream = message.get("stream_id").getAsString(), type = message.get("entity_type").getAsString();
        String world = message.get("world_id").getAsString(), dimension = message.get("dimension").getAsString();
        if (message.get("version").getAsInt() != 2 || !message.get("source").getAsString().equals("7dtd")
            || !message.get("authority").getAsString().equals("7dtd") || !origin.get("game").getAsString().equals("7dtd")
            || !origin.get("entity_id").getAsString().equals(id) || !origin.get("world_id").getAsString().equals(world)
            || !origin.get("dimension").getAsString().equals(dimension) || !type.startsWith("7dtd:") || !dimension.startsWith("7dtd:"))
            throw new IllegalArgumentException("invalid authority/origin");
        if (!UUID.fromString(id).toString().equals(id) || !UUID.fromString(stream).toString().equals(stream)) throw new IllegalArgumentException("invalid UUID");
        long sequence = message.get("sequence").getAsLong();
        if (sequence < 1 || sequence > 9007199254740991L) throw new IllegalArgumentException("invalid sequence");
        String event = message.getAsJsonObject("lifecycle").get("event").getAsString();
        if (event.equals("despawn")) {
            if (!message.get("position").isJsonNull() || !message.get("rotation").isJsonNull() || message.getAsJsonObject("metadata").size() != 0)
                throw new IllegalArgumentException("invalid despawn");
            return new Snapshot(id, stream, type, world, dimension, sequence, event, 0, 0, 0, 0, 0, 0);
        }
        if (!Set.of("spawn", "update").contains(event)) throw new IllegalArgumentException("invalid lifecycle");
        var p = message.getAsJsonObject("position"); var r = message.getAsJsonObject("rotation");
        if (!p.get("space").getAsString().equals("minecraft")) throw new IllegalArgumentException("unmapped position");
        double x = number(p,"x"), y = number(p,"y"), z = number(p,"z");
        double yaw = number(r,"yaw"), pitch = number(r,"pitch"), roll = number(r,"roll");
        if (yaw < 0 || yaw >= 360 || pitch < -90 || pitch > 90 || roll < -180 || roll >= 180) throw new IllegalArgumentException("invalid rotation");
        return new Snapshot(id, stream, type, world, dimension, sequence, event, x, y, z, yaw, pitch, roll);
    }
    private static double number(JsonObject object, String name) {
        var value = object.get(name);
        if (!value.isJsonPrimitive() || !value.getAsJsonPrimitive().isNumber() || !Double.isFinite(value.getAsDouble())) throw new IllegalArgumentException("invalid number");
        return value.getAsDouble();
    }
}
