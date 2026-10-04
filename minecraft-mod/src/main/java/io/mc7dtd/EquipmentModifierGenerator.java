package io.mc7dtd;

import com.google.gson.*;
import com.google.gson.stream.*;
import java.io.*;
import java.math.BigDecimal;
import java.nio.charset.StandardCharsets;
import java.util.*;

/** Pure, bounded projection of accepted full Equipment and effective Presentation values. */
public final class EquipmentModifierGenerator {
    private static final int MAX_BYTES=2*1024*1024,MAX_RULES=4096;
    private record Key(String slot,String itemId) { }
    private record Rule(String renderer,String model,EquipmentVisualState.Modifier modifier) { }
    private final Map<Key,List<Rule>> rules;
    private EquipmentModifierGenerator(Map<Key,List<Rule>> values) {
        var copy=new HashMap<Key,List<Rule>>();values.forEach((key,value)->copy.put(key,List.copyOf(value)));rules=Map.copyOf(copy);
    }
    public static EquipmentModifierGenerator empty(){return new EquipmentModifierGenerator(Map.of());}
    public static EquipmentModifierGenerator bundled() {
        try(var stream=EquipmentModifierGenerator.class.getResourceAsStream("/equipment_visual_mappings.json")) {
            if(stream==null)return empty();var bytes=stream.readNBytes(MAX_BYTES+1);
            if(bytes.length>MAX_BYTES)return empty();return fromJson(new String(bytes,StandardCharsets.UTF_8));
        }catch(IOException ex){return empty();}
    }
    /** Invalid catalogs fail closed as a whole, never partially enable their rules. */
    public static EquipmentModifierGenerator fromJson(String raw) {
        try {
            if(raw.getBytes(StandardCharsets.UTF_8).length>MAX_BYTES)throw new IllegalArgumentException("catalog size");
            JsonObject root;
            try(var reader=new JsonReader(new StringReader(raw))) {
                reader.setStrictness(Strictness.STRICT);root=read(reader,0).getAsJsonObject();
                if(reader.peek()!=JsonToken.END_DOCUMENT)throw new IllegalArgumentException("trailing JSON");
            }
            fields(root,"format_version","source_type","target_game","rules");
            var version=root.get("format_version");
            if(!version.isJsonPrimitive() || !version.getAsJsonPrimitive().isNumber() || version.getAsBigDecimal().compareTo(BigDecimal.ONE)!=0
                || !string(root,"source_type").equals("7dtd:player") || !string(root,"target_game").equals("minecraft"))throw new IllegalArgumentException("catalog scope/version");
            var entries=root.getAsJsonArray("rules");if(entries.size()>MAX_RULES)throw new IllegalArgumentException("catalog rules");
            var result=new HashMap<Key,List<Rule>>();
            for(var entry:entries) {
                var rule=entry.getAsJsonObject();fields(rule,"slot","item_id","compatible_base","modifier");
                String slot=string(rule,"slot"),id=string(rule,"item_id");
                if(!EquipmentVisualState.SLOTS.contains(slot) || id.length()>128 || !id.matches("7dtd:[A-Za-z0-9_.-]+"))throw new IllegalArgumentException("rule identity");
                var base=rule.getAsJsonObject("compatible_base");fields(base,"renderer","model");
                String renderer=identifier(base,"renderer"),model=identifier(base,"model");
                var modifier=rule.getAsJsonObject("modifier");fields(modifier,"kind","binding_id","anchor");
                String kind=string(modifier,"kind"),anchor=string(modifier,"anchor"),binding=identifier(modifier,"binding_id");
                if(!anchor.equals(slot) || !kind.equals(slot.equals("held_item") ? "held_attachment" : "armor_overlay"))throw new IllegalArgumentException("rule kind/anchor");
                var matching=result.computeIfAbsent(new Key(slot,id),key->new ArrayList<>());
                if(matching.stream().anyMatch(old->old.renderer.equals(renderer) && old.model.equals(model)))throw new IllegalArgumentException("duplicate rule");
                matching.add(new Rule(renderer,model,new EquipmentVisualState.Modifier(kind,binding,anchor)));
            }
            return new EquipmentModifierGenerator(result);
        }catch(IOException|RuntimeException ex){return empty();}
    }
    public EquipmentVisualState generate(JsonObject equipment){return generate(equipment,PresentationReceiver.fallback());}
    public EquipmentVisualState generate(JsonObject equipment,JsonObject presentation) {
        var values=new LinkedHashMap<String,EquipmentVisualState.Slot>();
        for(String name:EquipmentVisualState.SLOTS)values.put(name,null);
        if(equipment==null || equipment.isEmpty())return new EquipmentVisualState(false,values);
        try { EquipmentReceiver.validate(equipment,true); }
        catch(RuntimeException ex){return new EquipmentVisualState(false,values);}
        String renderer=baseField(presentation,"renderer"),model=baseField(presentation,"model");
        var slots=equipment.getAsJsonObject("slots");
        for(String name:EquipmentVisualState.SLOTS) {
            var value=slots.get(name);if(value.isJsonNull())continue;
            String id=value.getAsJsonObject().get("item_id").getAsString();var candidates=rules.get(new Key(name,id));
            var resolution=EquipmentVisualState.Resolution.UNMAPPED;EquipmentVisualState.Modifier modifier=null;
            if(candidates!=null) {
                resolution=EquipmentVisualState.Resolution.INCOMPATIBLE;
                for(var candidate:candidates)if(candidate.renderer.equals(renderer) && candidate.model.equals(model)) {
                    resolution=EquipmentVisualState.Resolution.RESOLVED;modifier=candidate.modifier;break;
                }
            }
            values.put(name,new EquipmentVisualState.Slot(id,resolution,modifier));
        }
        return new EquipmentVisualState(true,values);
    }
    private static String baseField(JsonObject base,String name) {
        if(base==null || !base.has(name) || !base.get(name).isJsonPrimitive() || !base.get(name).getAsJsonPrimitive().isString())return "";
        return base.get(name).getAsString();
    }
    private static String string(JsonObject object,String name) {
        var value=object.get(name);if(value==null || !value.isJsonPrimitive() || !value.getAsJsonPrimitive().isString())throw new IllegalArgumentException("string");return value.getAsString();
    }
    private static String identifier(JsonObject object,String name) {
        String value=string(object,name);if(value.length()>64 || !value.matches("[a-z0-9_.:-]+"))throw new IllegalArgumentException("identifier");return value;
    }
    private static void fields(JsonObject object,String... names){if(!object.keySet().equals(Set.of(names)))throw new IllegalArgumentException("fields");}
    private static JsonElement read(JsonReader reader,int depth)throws IOException {
        if(depth>8)throw new IllegalArgumentException("catalog depth");
        return switch(reader.peek()) {
            case BEGIN_OBJECT -> {
                var value=new JsonObject();reader.beginObject();
                while(reader.hasNext()){String key=reader.nextName();if(value.has(key))throw new IllegalArgumentException("duplicate key");value.add(key,read(reader,depth+1));}
                reader.endObject();yield value;
            }
            case BEGIN_ARRAY -> {
                var value=new JsonArray();reader.beginArray();while(reader.hasNext())value.add(read(reader,depth+1));reader.endArray();yield value;
            }
            case STRING -> new JsonPrimitive(reader.nextString());
            case NUMBER -> new JsonPrimitive(new BigDecimal(reader.nextString()));
            case BOOLEAN -> new JsonPrimitive(reader.nextBoolean());
            case NULL -> {reader.nextNull();yield JsonNull.INSTANCE;}
            default -> throw new IllegalArgumentException("JSON token");
        };
    }
}
