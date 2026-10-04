package io.mc7dtd;

import com.google.gson.*;
import java.util.*;
import java.util.function.Consumer;

/** Observations only, on the connection worker. Never changes Minecraft health or scene objects. */
public final class HealthReceiver {
    private record Entity(String type, long sequence) { }
    public record State(long revision, Double current, Double max, JsonObject components) { }
    private final Map<String, Entity> entities = new HashMap<>();
    private final Map<String, State> states = new HashMap<>();
    private final Consumer<String> log;
    public HealthReceiver(Consumer<String> logger) { log = logger; }
    public void reset() { entities.clear(); states.clear(); }
    private static String key(JsonObject m) { return m.get("world_id").getAsString()+"|"+m.get("dimension").getAsString()+"|"+m.get("entity_id").getAsString()+"|"+m.get("stream_id").getAsString(); }
    public int count() { return states.size(); }
    public State state(JsonObject m) { return states.get(key(m)); }
    public void lifecycle(JsonObject m) {
        String key=key(m); var action=m.getAsJsonObject("lifecycle").get("event").getAsString();
        long sequence=m.get("sequence").getAsLong(); var old=entities.get(key);
        if (old!=null && sequence<=old.sequence()) return;
        if (action.equals("despawn")) { entities.remove(key); states.remove(key); return; }
        if (action.equals("spawn") && old==null || action.equals("update") && old!=null)
            entities.put(key,new Entity(m.get("entity_type").getAsString(),sequence));
    }
    public void receive(JsonObject m) {
        try {
            if (!m.get("type").getAsString().equals("entity_components") || integer(m,"version",1)!=1 || integer(m,"entity_state_version",2)!=2
                || !m.get("source").getAsString().equals("7dtd") || !m.get("authority").getAsString().equals("7dtd")) throw new IllegalArgumentException("authority/version");
            var origin=m.getAsJsonObject("origin");
            if (!origin.get("game").getAsString().equals("7dtd")) throw new IllegalArgumentException("origin");
            for (var name:List.of("world_id","dimension","entity_id")) if (!origin.get(name).equals(m.get(name))) throw new IllegalArgumentException("origin");
            String key=key(m); var entity=entities.get(key);
            if(entity==null || !entity.type().equals(m.get("entity_type").getAsString()) || !entity.type().equals("7dtd:player")
                || entity.sequence()!=integer(m,"entity_sequence",1)) throw new IllegalArgumentException("inactive/mismatched entity");
            long revision=integer(m,"revision",1), base=integer(m,"base_revision",0);
            String mode=m.get("mode").getAsString();var c=m.getAsJsonObject("components");
            if(c.keySet().stream().anyMatch(n->!Set.of("health","name","custom_metadata").contains(n))) throw new IllegalArgumentException("unsupported component");
            Double current=null,max=null;
            if(c.has("health") && !c.get("health").isJsonNull()) {
                var h=c.getAsJsonObject("health"); if(!h.keySet().equals(Set.of("current","max"))) throw new IllegalArgumentException("health fields");
                current=number(h,"current");max=number(h,"max"); if(current<0 || max<=0 || current>max) throw new IllegalArgumentException("health range");
            }
            for(var item:c.entrySet()) {
                if(item.getValue().isJsonNull()) { if(mode.equals("snapshot"))throw new IllegalArgumentException("snapshot null");continue; }
                if(item.getKey().equals("name"))IdentityComponent.name(item.getValue());
                if(item.getKey().equals("custom_metadata"))IdentityComponent.metadata(item.getValue());
            }
            if(mode.equals("snapshot")) { if(base!=0) throw new IllegalArgumentException("snapshot"); }
            else if(!mode.equals("patch") || c.size()==0 || base<1) throw new IllegalArgumentException("patch");
            var old=states.get(key);if(old!=null && revision<=old.revision()) return;
            if(mode.equals("patch") && (old==null || base!=old.revision())) throw new IllegalArgumentException("baseline");
            var result=mode.equals("snapshot") ? new JsonObject() : old.components().deepCopy();
            for(var item:c.entrySet())if(item.getValue().isJsonNull())result.remove(item.getKey());else result.add(item.getKey(),item.getValue().deepCopy());
            current=result.has("health") ? result.getAsJsonObject("health").get("current").getAsDouble() : null;
            max=result.has("health") ? result.getAsJsonObject("health").get("max").getAsDouble() : null;
            states.put(key,new State(revision,current,max,result));
            if(c.has("health") || mode.equals("snapshot"))log.accept("7DTD health received: id="+m.get("entity_id").getAsString()+" revision="+revision+" mode="+mode
                +(current==null ? " removed=true" : " current="+current+" max="+max));
            if(c.has("name") || c.has("custom_metadata") || mode.equals("snapshot") && old!=null && (old.components().has("name") || old.components().has("custom_metadata")))
                log.accept("7DTD identity received: id="+m.get("entity_id").getAsString()+" revision="+revision+" mode="+mode+" identity="+result
                    +" removed="+(!result.has("name") && !result.has("custom_metadata")));
        } catch(RuntimeException ex) { log.accept("7DTD health rejected: "+ex.getMessage()); }
    }
    private static long integer(JsonObject m,String n,long min) { var v=m.get(n);if(!v.isJsonPrimitive()||!v.getAsJsonPrimitive().isNumber()) throw new IllegalArgumentException("integer");
        long x;try{x=v.getAsBigDecimal().longValueExact();}catch(ArithmeticException ex){throw new IllegalArgumentException("integer");}if(x<min||x>9007199254740991L)throw new IllegalArgumentException("integer");return x; }
    private static double number(JsonObject m,String n) { var v=m.get(n);if(!v.isJsonPrimitive()||!v.getAsJsonPrimitive().isNumber()||!Double.isFinite(v.getAsDouble()))throw new IllegalArgumentException("health number");return v.getAsDouble(); }
}
