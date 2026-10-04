package io.mc7dtd;

import com.google.gson.*;
import java.util.*;

/** Synchronized read-only diagnostic mirror; snapshots never escape by reference. */
public final class EntityInspector {
    private final Map<String,JsonObject> records=new LinkedHashMap<>();
    private String recent;
    private final EquipmentModifierGenerator modifierGenerator;
    private boolean visualWorldAvailable=true;
    private ProxyAppearanceAdapter appearanceAdapter=new ProxyAppearanceAdapter(ProxyAppearanceConfig.disabled());
    private boolean appearanceConnected=true;
    private boolean rendererRuntime;
    private Object visualWorldToken;
    private final Map<String,JsonObject> renderers=new HashMap<>();
    public synchronized void rendererRuntime(){rendererRuntime=true;}
    public synchronized void rendererState(NativeProxyController.Snapshot state,JsonObject value){String key="7dtd|"+state.world()+"|"+state.dimension()+"|"+state.id()+"|"+state.stream();if(value==null)renderers.remove(key);else if(renderers.containsKey(key) || renderers.size()<256)renderers.put(key,value.deepCopy());}
    public EntityInspector(){this(EquipmentModifierGenerator.bundled());}
    public EntityInspector(EquipmentModifierGenerator generator){modifierGenerator=Objects.requireNonNull(generator);}
    /** Local diagnostic availability only; does not clear or mutate synchronized components. */
    public synchronized void visualWorld(Object world){if(visualWorldToken!=world)renderers.clear();visualWorldToken=world;visualWorldAvailable=world!=null;}
    public synchronized void appearanceAdapter(ProxyAppearanceAdapter adapter){appearanceAdapter=Objects.requireNonNull(adapter);}
    /** Local appearance availability; retained fact components are never rewritten. */
    public synchronized void appearanceConnected(boolean value){appearanceConnected=value;if(!value)renderers.clear();}
    public synchronized boolean appearanceConnected(){return appearanceConnected;}
    /** Exact scope lookup returning only derived data to the scene, never raw Equipment. */
    public synchronized EquipmentVisualState visualState(NativeProxyController.Snapshot state) {
        if(!visualWorldAvailable)return null;
        var record=records.get("7dtd|"+state.world()+"|"+state.dimension()+"|"+state.id()+"|"+state.stream());
        return record==null || !record.get("type").getAsString().equals("7dtd:player") ? null : derive(record);
    }
    private EquipmentVisualState derive(JsonObject record) {
        var components=record.getAsJsonObject("components");var base=components.getAsJsonObject("presentation");
        if(base==null || base.isEmpty())base=PresentationReceiver.fallback();
        return modifierGenerator.generate(components.getAsJsonObject("equipment"),base);
    }
    private JsonObject project(JsonObject record) {
        var copy=record.deepCopy();
        if(copy.get("source").getAsString().equals("7dtd") && copy.get("type").getAsString().equals("7dtd:player")) {
            var visual=visualWorldAvailable ? derive(copy) : null;
            copy.add("equipment_visual_state",visual==null ? JsonNull.INSTANCE : visual.toJson());
            var appearance=visual==null ? null : (rendererRuntime ? appearanceAdapter.generateForRenderer(appearanceConnected ? visual : null) : appearanceAdapter.generate(appearanceConnected ? visual : null)).toJson();
            if(appearance!=null && rendererRuntime){String scope=record.get("source").getAsString()+"|"+record.get("world_id").getAsString()+"|"+record.get("dimension").getAsString()+"|"+record.get("id").getAsString()+"|"+record.get("stream_id").getAsString();var renderer=renderers.get(scope);appearance.add("renderer",renderer==null ? JsonNull.INSTANCE : renderer.deepCopy());}
            copy.add("proxy_appearance",appearance==null ? JsonNull.INSTANCE : appearance);
        }
        return copy;
    }
    private static String key(JsonObject e) { return e.get("source").getAsString()+"|"+e.get("world_id").getAsString()+"|"+e.get("dimension").getAsString()+"|"+e.get("entity_id").getAsString()+"|"+e.get("stream_id").getAsString(); }
    public synchronized void lifecycle(JsonObject e,HealthReceiver.State observed,JsonObject presentation) {
        String key=key(e), event=e.getAsJsonObject("lifecycle").get("event").getAsString();
        if(event.equals("despawn")) { records.remove(key);renderers.remove(key); if(key.equals(recent))recent=null; return; }
        if(event.equals("update") && !records.containsKey(key))return;
        if(!records.containsKey(key) && records.size()>=4096)return;
        var components=records.containsKey(key) ? records.get(key).getAsJsonObject("components").deepCopy() : new JsonObject();
        for(String n:List.of("identity","health","presentation"))if(!components.has(n))components.add(n,new JsonObject());
        if(e.has("components") && e.getAsJsonObject("components").has("presentation")) {
            var p=e.getAsJsonObject("components").get("presentation");
            components.add("presentation",p.isJsonNull() || presentation==null ? new JsonObject() : presentation.deepCopy());
        }
        var authority=new JsonObject();authority.add("source",e.get("source").deepCopy());authority.add("owner",e.has("authority") ? e.get("authority").deepCopy() : e.get("source").deepCopy());
        if(e.has("origin"))authority.add("origin",e.get("origin").deepCopy());components.add("authority",authority);
        var result=new JsonObject();result.add("id",e.get("entity_id").deepCopy());result.add("type",e.get("entity_type").deepCopy());
        for(String n:List.of("source","world_id","dimension","stream_id","sequence","position","rotation","metadata"))if(e.has(n))result.add(n,e.get(n).deepCopy());
        result.add("components",components);records.put(key,result);recent=key;
        copyComponents(result,observed);
    }
    public synchronized void components(JsonObject e,HealthReceiver.State observed) { var record=records.get(key(e));if(record!=null && observed!=null)copyComponents(record,observed); }
    public synchronized void components(JsonObject e,HealthReceiver.State observed,JsonObject equipment) {
        components(e,observed);var record=records.get(key(e));
        if(record!=null)record.getAsJsonObject("components").add("equipment",equipment==null ? new JsonObject() : equipment.deepCopy());
    }
    private static void copyComponents(JsonObject record,HealthReceiver.State state) {
        if(state==null)return;
        var observed=state.components();var identity=new JsonObject();
        if(observed.has("name")) {
            var n=observed.getAsJsonObject("name");identity.add("name",n.get("text").deepCopy());
            if(n.has("display_name"))identity.add("display_name",n.get("display_name").deepCopy());
        }
        if(observed.has("custom_metadata"))identity.add("metadata",observed.get("custom_metadata").deepCopy());
        var components=record.getAsJsonObject("components");components.add("identity",identity);
        components.add("health",observed.has("health") ? observed.get("health").deepCopy() : new JsonObject());
    }
    public synchronized JsonObject latest() {
        if(recent!=null && records.containsKey(recent))return project(records.get(recent));
        return records.isEmpty() ? null : project(new ArrayList<>(records.values()).getLast());
    }
    public synchronized List<JsonObject> list() { return records.values().stream().map(this::project).toList(); }
    public synchronized JsonObject find(String id,String world,String dimension) {
        return records.values().stream().filter(e->e.get("id").getAsString().equals(id) && e.get("world_id").getAsString().equals(world) && e.get("dimension").getAsString().equals(dimension)).findFirst().map(this::project).orElse(null);
    }
    public synchronized void reset() { records.clear();renderers.clear();recent=null; }
    public static String tag(JsonObject entity) {
        if(entity==null)return "[unknown]\ndefault\nHP:unknown";
        var c=entity.getAsJsonObject("components");var p=c.getAsJsonObject("presentation");var h=c.getAsJsonObject("health");
        return "["+entity.get("id").getAsString()+"]\n"+(p.has("model") ? p.get("model").getAsString() : "default")+"\nHP:"+(h.has("current") ? h.get("current").getAsString() : "unknown");
    }
    public static String format(JsonObject entity) { return entity==null ? "Entity Inspector: no synced entities" : "======== Entity Inspector ========\n"+new GsonBuilder().serializeNulls().setPrettyPrinting().create().toJson(entity)+"\n=================================="; }
    public synchronized String formatList() {
        var result=new StringBuilder("Synced Entities: "+records.size());int index=0;
        for(var e:records.values())result.append("\n").append(++index).append(". ").append(e.get("type").getAsString()).append(" ").append(tag(e).replace('\n',' ')).append(" world=").append(e.get("world_id").getAsString()).append(" dimension=").append(e.get("dimension").getAsString());
        return result.toString();
    }
}
