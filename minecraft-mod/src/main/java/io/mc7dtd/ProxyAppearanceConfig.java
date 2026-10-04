package io.mc7dtd;

import com.google.gson.Strictness;
import com.google.gson.stream.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import java.util.function.Consumer;

/** Target-local startup config; invalid or absent config disables appearance safely. */
public record ProxyAppearanceConfig(boolean enabled,Map<String,String> mapping) {
    private static final int MAX_BYTES=65536,MAX_MAPPINGS=256;
    public ProxyAppearanceConfig { mapping=Map.copyOf(mapping); }
    public static ProxyAppearanceConfig disabled(){return new ProxyAppearanceConfig(false,Map.of());}
    public static ProxyAppearanceConfig load(Path path,Consumer<String> warning) {
        if(!Files.exists(path))return disabled();
        try(var stream=Files.newInputStream(path)) {
            var bytes=stream.readNBytes(MAX_BYTES+1);if(bytes.length>MAX_BYTES)throw new IllegalArgumentException("config size");
            return parse(new String(bytes,StandardCharsets.UTF_8));
        }catch(IOException|RuntimeException ex){warning.accept("Proxy appearance config invalid; markers disabled");return disabled();}
    }
    public static ProxyAppearanceConfig parse(String raw) {
        if(raw.getBytes(StandardCharsets.UTF_8).length>MAX_BYTES)throw new IllegalArgumentException("config size");
        try(var reader=new JsonReader(new StringReader(raw))) {
            reader.setStrictness(Strictness.STRICT);reader.beginObject();var fields=new HashSet<String>();
            boolean enabled=false;var mapping=new LinkedHashMap<String,String>();
            while(reader.hasNext()) {
                String field=reader.nextName();if(!fields.add(field))throw new IllegalArgumentException("duplicate field");
                if(field.equals("enabled")) {if(reader.peek()!=JsonToken.BOOLEAN)throw new IllegalArgumentException("enabled boolean");enabled=reader.nextBoolean();}
                else if(field.equals("mapping")) {
                    reader.beginObject();while(reader.hasNext()) {
                        String id=reader.nextName();if(mapping.size()>=MAX_MAPPINGS || id.length()>128 || !id.matches("7dtd:[A-Za-z0-9_.-]+"))throw new IllegalArgumentException("mapping id/size");
                        if(reader.peek()!=JsonToken.STRING)throw new IllegalArgumentException("marker string");String marker=reader.nextString();
                        if(!Set.of("club","torch","axe","helmet").contains(marker) || mapping.putIfAbsent(id,marker)!=null)throw new IllegalArgumentException("marker/duplicate id");
                    }reader.endObject();
                }else throw new IllegalArgumentException("unknown config field");
            }
            reader.endObject();if(!fields.equals(Set.of("enabled","mapping")) || reader.peek()!=JsonToken.END_DOCUMENT)throw new IllegalArgumentException("config fields/trailing JSON");
            return new ProxyAppearanceConfig(enabled,mapping);
        }catch(IOException ex){throw new IllegalArgumentException("invalid config",ex);}
    }
}
