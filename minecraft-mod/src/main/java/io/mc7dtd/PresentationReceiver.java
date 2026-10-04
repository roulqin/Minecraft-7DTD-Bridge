package io.mc7dtd;

import com.google.gson.JsonObject;
import java.util.HashMap;
import java.util.Map;
import java.util.HashSet;
import java.util.Set;
import java.util.function.Consumer;

/** Socket-thread metadata only. No rendering changes or mirrored publication. */
public final class PresentationReceiver {
    private final Map<String, JsonObject> states = new HashMap<>();
    private final Set<String> active = new HashSet<>();
    private final Consumer<String> log;
    public PresentationReceiver(Consumer<String> log) { this.log = log; }
    public static JsonObject fallback() {
        var result = new JsonObject(); result.addProperty("renderer", "unknown"); result.addProperty("model", "default"); result.addProperty("scale", 1.0); return result;
    }
    private static String key(JsonObject e) {
        return e.get("source").getAsString()+"\n"+e.get("world_id").getAsString()+"\n"+e.get("dimension").getAsString()+"\n"+e.get("entity_id").getAsString();
    }
    public void lifecycle(JsonObject entity) {
        String key = key(entity), event = entity.getAsJsonObject("lifecycle").get("event").getAsString();
        if (event.equals("despawn")) { states.remove(key); active.remove(key); return; }
        if (event.equals("spawn")) { active.add(key); states.put(key, fallback()); }
        if (!active.contains(key)) return;
        if (entity.has("components") && entity.getAsJsonObject("components").has("presentation")) {
            var value = entity.getAsJsonObject("components").get("presentation");
            if (value.isJsonNull()) { states.remove(key); log.accept("[Presentation] entity="+entity.get("entity_id").getAsString()+" removed=true"); return; }
            var result = states.getOrDefault(key, fallback()).deepCopy();
            for (var field : value.getAsJsonObject().entrySet()) result.add(field.getKey(), field.getValue().deepCopy());
            states.put(key, result);
            log.accept("[Presentation] entity="+entity.get("entity_id").getAsString()+" renderer="+result.get("renderer").getAsString()
                +" model="+result.get("model").getAsString()+" variant="+(result.has("variant") ? result.get("variant").getAsString() : "none")+" scale="+result.get("scale").getAsDouble());
        }
    }
    public JsonObject get(JsonObject entity) { String key=key(entity); return active.contains(key) ? states.getOrDefault(key, fallback()).deepCopy() : null; }
    public void reset() { states.clear(); active.clear(); }
}
