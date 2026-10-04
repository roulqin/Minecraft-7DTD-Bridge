package io.mc7dtd;

import com.google.gson.*;
import java.util.*;

/** Immutable local description; never a network component or a rendered object. */
public final class EquipmentVisualState {
    public static final List<String> SLOTS=List.of("head","body","hands","feet","held_item");
    public enum Resolution { RESOLVED, UNMAPPED, INCOMPATIBLE }
    public record Modifier(String kind,String bindingId,String anchor) { }
    public record Slot(String itemId,Resolution resolution,Modifier modifier) {
        public Slot {
            Objects.requireNonNull(itemId);Objects.requireNonNull(resolution);
            if((resolution==Resolution.RESOLVED)!=(modifier!=null))throw new IllegalArgumentException("modifier resolution");
        }
    }
    private final boolean known;
    private final Map<String,Slot> slots;
    EquipmentVisualState(boolean known,Map<String,Slot> values) {
        if(!values.keySet().equals(new HashSet<>(SLOTS)))throw new IllegalArgumentException("five slots required");
        var copy=new LinkedHashMap<String,Slot>();
        for(String name:SLOTS)copy.put(name,known ? values.get(name) : null);
        this.known=known;slots=Collections.unmodifiableMap(copy);
    }
    public boolean known(){return known;}
    public Slot slot(String name){if(!slots.containsKey(name))throw new IllegalArgumentException("slot");return slots.get(name);}
    public JsonObject toJson() {
        var result=new JsonObject();result.addProperty("equipment_state",known ? "known" : "unknown");
        var values=new JsonObject();result.add("slots",values);
        for(var entry:slots.entrySet()) {
            var slot=entry.getValue();if(slot==null){values.add(entry.getKey(),JsonNull.INSTANCE);continue;}
            var value=new JsonObject();value.addProperty("item_id",slot.itemId());
            value.addProperty("resolution",slot.resolution().name().toLowerCase(Locale.ROOT));
            if(slot.modifier()==null)value.add("modifier",JsonNull.INSTANCE);
            else {
                var modifier=new JsonObject();modifier.addProperty("kind",slot.modifier().kind());
                modifier.addProperty("binding_id",slot.modifier().bindingId());modifier.addProperty("anchor",slot.modifier().anchor());value.add("modifier",modifier);
            }
            values.add(entry.getKey(),value);
        }
        return result;
    }
    @Override public boolean equals(Object other){return other instanceof EquipmentVisualState value && known==value.known && slots.equals(value.slots);}
    @Override public int hashCode(){return Objects.hash(known,slots);}
}
