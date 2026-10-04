package io.mc7dtd;

import com.google.gson.JsonParser;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Set;
import java.util.function.Consumer;

public record DebugConfig(boolean nameTag, boolean logging, boolean navigation) {
    public DebugConfig(boolean nameTag,boolean logging){this(nameTag,logging,false);}
    public static DebugConfig load(Path path, Consumer<String> warning) {
        if (!Files.exists(path)) return new DebugConfig(false,false);
        try {
            var json=JsonParser.parseString(Files.readString(path)).getAsJsonObject();
            if (!json.keySet().containsAll(Set.of("debug_name_tag","debug_logging")) || !Set.of("debug_name_tag","debug_logging","debug_navigation").containsAll(json.keySet())) throw new IllegalArgumentException();
            for (var value:json.asMap().values()) if(!value.isJsonPrimitive() || !value.getAsJsonPrimitive().isBoolean()) throw new IllegalArgumentException();
            return new DebugConfig(json.get("debug_name_tag").getAsBoolean(),json.get("debug_logging").getAsBoolean(),json.has("debug_navigation") && json.get("debug_navigation").getAsBoolean());
        } catch(Exception ex) { warning.accept("Debug config invalid; debug display disabled"); return new DebugConfig(false,false); }
    }
}
