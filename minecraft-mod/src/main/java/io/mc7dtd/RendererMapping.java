package io.mc7dtd;

import com.google.gson.*;
import com.google.gson.stream.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import java.util.function.Consumer;

/** Startup-only held_item mappings. Invalid configuration falls back to markers. */
public record RendererMapping(Map<String,Entry> mapping) {
    public record Vector(double x,double y,double z) { }
    public record Entry(String item,Vector offset,Vector rotation,double scale) { }
    public static final Vector DEFAULT_OFFSET=new Vector(.55,1.05,0);
    public RendererMapping {mapping=Map.copyOf(mapping);}
    public Entry get(String id){return mapping.get(id);}
    public static RendererMapping load(Path path,Consumer<String> log) {
        try(var stream=Files.newInputStream(path)){var bytes=stream.readNBytes(65537);if(bytes.length>65536)throw new IllegalArgumentException("config too large");return parse(new String(bytes,StandardCharsets.UTF_8));}
        catch(IOException|RuntimeException ex){log.accept("Renderer mapping unavailable; using fallback markers: "+ex.getMessage());return new RendererMapping(Map.of());}
    }
    public static RendererMapping parse(String raw) {
        if(raw.getBytes(StandardCharsets.UTF_8).length>65536)throw new IllegalArgumentException("config too large");
        var json=strictJson(raw);var result=new LinkedHashMap<String,Entry>();
        if(json.size()>256)throw new IllegalArgumentException("mapping capacity");
        for(var property:json.entrySet()) {
            String id=property.getKey();if(id.length()>128 || !id.matches("7dtd:[A-Za-z0-9_.-]+"))throw new IllegalArgumentException("source item id");
            var entry=property.getValue().getAsJsonObject();
            if(!entry.keySet().containsAll(Set.of("renderer","item","anchor")) || !Set.of("renderer","item","anchor","offset","rotation","scale").containsAll(entry.keySet()))throw new IllegalArgumentException("mapping fields");
            if(!string(entry,"renderer").equals("item_display") || !string(entry,"anchor").equals("held_item"))throw new IllegalArgumentException("renderer/anchor");
            String item=string(entry,"item");if(item.length()>128 || !item.matches("[a-z0-9_.-]+:[a-z0-9_./-]+"))throw new IllegalArgumentException("Minecraft item id");
            var offset=vector(entry,"offset",DEFAULT_OFFSET,2);var rotation=vector(entry,"rotation",new Vector(0,0,0),180);
            double scale=entry.has("scale") ? number(entry.get("scale")) : .8;if(scale<.05 || scale>2)throw new IllegalArgumentException("scale range");
            result.put(id,new Entry(item,offset,rotation,scale));
        }
        return new RendererMapping(result);
    }
    private static String string(JsonObject object,String key){var value=object.get(key);if(!value.isJsonPrimitive() || !value.getAsJsonPrimitive().isString())throw new IllegalArgumentException("string required");return value.getAsString();}
    private static double number(JsonElement value){if(!value.isJsonPrimitive() || !value.getAsJsonPrimitive().isNumber())throw new IllegalArgumentException("number required");double number=value.getAsDouble();if(!Double.isFinite(number))throw new IllegalArgumentException("finite number required");return number;}
    private static Vector vector(JsonObject entry,String name,Vector fallback,double limit){if(!entry.has(name))return fallback;var v=entry.getAsJsonObject(name);if(!v.keySet().equals(Set.of("x","y","z")))throw new IllegalArgumentException("vector fields");var result=new Vector(number(v.get("x")),number(v.get("y")),number(v.get("z")));if(Math.abs(result.x)>limit || Math.abs(result.y)>limit || Math.abs(result.z)>limit)throw new IllegalArgumentException("vector range");return result;}
    private static JsonObject strictJson(String raw) {
        try(var reader=new JsonReader(new StringReader(raw))){reader.setStrictness(Strictness.STRICT);var json=read(reader,0).getAsJsonObject();if(reader.peek()!=JsonToken.END_DOCUMENT)throw new IllegalArgumentException("trailing JSON");return json;}
        catch(IOException ex){throw new IllegalArgumentException("invalid JSON",ex);}
    }
    private static JsonElement read(JsonReader reader,int depth)throws IOException {
        if(depth>4)throw new IllegalArgumentException("JSON depth");
        return switch(reader.peek()) {
            case BEGIN_OBJECT -> {reader.beginObject();var json=new JsonObject();while(reader.hasNext()){String key=reader.nextName();if(json.has(key))throw new IllegalArgumentException("duplicate key");json.add(key,read(reader,depth+1));}reader.endObject();yield json;}
            case STRING -> new JsonPrimitive(reader.nextString());
            case NUMBER -> new JsonPrimitive(new java.math.BigDecimal(reader.nextString()));
            default -> throw new IllegalArgumentException("unsupported JSON type");
        };
    }
}
