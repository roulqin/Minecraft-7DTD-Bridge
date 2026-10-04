package io.mc7dtd;

import com.google.gson.*;
import java.util.*;
import java.util.function.Consumer;

/** Equipment strategy around the unchanged Health/Identity reducer; worker-thread owned. */
public final class EquipmentReceiver {
    private static final Set<String> SLOTS=Set.of("head","body","hands","feet","held_item");
    private final HealthReceiver legacy;
    private final Consumer<String> log;
    private final Map<String,JsonObject> values=new HashMap<>();
    public EquipmentReceiver(HealthReceiver legacy,Consumer<String> log){this.legacy=legacy;this.log=log;}
    private static String key(JsonObject m){return m.get("world_id").getAsString()+"|"+m.get("dimension").getAsString()+"|"+m.get("entity_id").getAsString()+"|"+m.get("stream_id").getAsString();}
    public void reset(){values.clear();}
    public void lifecycle(JsonObject m){if(m.getAsJsonObject("lifecycle").get("event").getAsString().equals("despawn"))values.remove(key(m));}
    public JsonObject state(JsonObject m){var v=values.get(key(m));return v==null ? null : v.deepCopy();}
    private static long integer(JsonObject m,String field,long min){var v=m.get(field);if(!v.isJsonPrimitive() || !v.getAsJsonPrimitive().isNumber())throw new IllegalArgumentException("integer");long n=v.getAsBigDecimal().longValueExact();if(n<min || n>9007199254740991L)throw new IllegalArgumentException("integer");return n;}
    public static void validate(JsonElement value,boolean complete){
        if(!value.isJsonObject() || !value.getAsJsonObject().keySet().equals(Set.of("slots")) || !value.getAsJsonObject().get("slots").isJsonObject())throw new IllegalArgumentException("equipment object");
        var slots=value.getAsJsonObject().getAsJsonObject("slots");
        if(slots.isEmpty() || !SLOTS.containsAll(slots.keySet()) || complete && !slots.keySet().equals(SLOTS))throw new IllegalArgumentException("equipment slots");
        for(var slot:slots.entrySet()){
            if(slot.getValue().isJsonNull())continue;
            if(!slot.getValue().isJsonObject() || !slot.getValue().getAsJsonObject().keySet().equals(Set.of("item_id")))throw new IllegalArgumentException("equipment item");
            var id=slot.getValue().getAsJsonObject().get("item_id");
            if(!id.isJsonPrimitive() || !id.getAsJsonPrimitive().isString() || id.getAsString().length()>128 || !id.getAsString().matches("7dtd:[A-Za-z0-9_.-]+"))throw new IllegalArgumentException("equipment item id");
        }
    }
    public void receive(JsonObject m){
        try {
            var components=m.getAsJsonObject("components");boolean supplied=components.has("equipment");
            String mode=m.get("mode").getAsString();
            if(!mode.equals("snapshot") && !mode.equals("patch"))throw new IllegalArgumentException("equipment mode");
            boolean snapshot=mode.equals("snapshot");
            String key=key(m);var old=legacy.state(m);var candidate=state(m);
            if(supplied){
                if(!m.get("entity_type").getAsString().equals("7dtd:player"))throw new IllegalArgumentException("equipment route");
                var value=components.get("equipment");
                if(value.isJsonNull()){if(snapshot)throw new IllegalArgumentException("equipment snapshot null");candidate=null;}
                else {
                    validate(value,snapshot || candidate==null);
                    if(snapshot || candidate==null)candidate=value.getAsJsonObject().deepCopy();
                    else for(var slot:value.getAsJsonObject().getAsJsonObject("slots").entrySet())candidate.getAsJsonObject("slots").add(slot.getKey(),slot.getValue().deepCopy());
                }
            }else if(snapshot)candidate=null;
            var projected=m.deepCopy();projected.getAsJsonObject("components").remove("equipment");
            long revision=integer(m,"revision",1);
            if(!snapshot && supplied && projected.getAsJsonObject("components").isEmpty()){
                long base=integer(m,"base_revision",1);
                if(old==null || revision>old.revision() && base!=old.revision())throw new IllegalArgumentException("equipment baseline");
                projected.addProperty("mode","snapshot");projected.addProperty("base_revision",0);projected.add("components",old.components().deepCopy());
            }
            legacy.receive(projected);
            var accepted=legacy.state(m);
            if(accepted==null || accepted.revision()!=revision || old!=null && revision<=old.revision())return;
            if(candidate==null)values.remove(key);else values.put(key,candidate);
            if(supplied || snapshot)log.accept("7DTD equipment received: id="+m.get("entity_id").getAsString()+" revision="+revision+" mode="+m.get("mode").getAsString()+" equipment="+(candidate==null ? "{} removed=true" : candidate.toString()));
        }catch(RuntimeException ex){log.accept("7DTD equipment rejected: "+ex.getMessage());}
    }
}
