package io.mc7dtd;

import com.google.gson.*;
import java.nio.file.*;
import java.util.*;

/** Runtime receiver allowlist, separate from the unchanged v1 send path. */
public final class ProxyTypeCatalog {
    private final Set<String> supported = new HashSet<>();
    public ProxyTypeCatalog(JsonObject catalog) {
        if (catalog.get("version").getAsInt() != 1) throw new IllegalArgumentException("Invalid catalog version");
        var ids = new HashSet<String>();
        for (var node : catalog.getAsJsonArray("types")) {
            var entry = node.getAsJsonObject(); String type = entry.get("entity_type").getAsString();
            if (!ids.add(type)) throw new IllegalArgumentException("Duplicate entity type");
            var target = entry.getAsJsonObject("target_mapping"); var caps = entry.getAsJsonObject("capabilities");
            if (!entry.get("enabled").getAsBoolean() || !target.get("game").getAsString().equals("minecraft")) continue;
            if (!type.startsWith("7dtd:") || !target.get("entity_type").getAsString().equals("minecraft:player_proxy")
                || !target.get("adapter").getAsString().equals("block_display_proxy")) continue;
            for (String key : List.of("spawn", "update_position", "update_rotation", "despawn"))
                if (!caps.get(key).getAsBoolean()) throw new IllegalArgumentException("Required proxy capability: " + key);
            for (String key : List.of("health", "collision", "ai", "combat", "persistence"))
                if (caps.get(key).getAsBoolean()) throw new IllegalArgumentException("Unsupported proxy capability: " + key);
            supported.add(type);
        }
    }
    public static ProxyTypeCatalog load(Path path) throws Exception {
        return new ProxyTypeCatalog(JsonParser.parseString(Files.readString(path)).getAsJsonObject());
    }
    public boolean supports(String type) { return supported.contains(type); }
}
