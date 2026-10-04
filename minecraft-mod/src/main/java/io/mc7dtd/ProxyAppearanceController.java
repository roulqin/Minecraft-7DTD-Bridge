package io.mc7dtd;

import java.util.*;
import java.util.function.Consumer;
import java.util.function.Function;

/** Client-thread marker lifecycle, bounded by actual proxy handles, independently of position updates. */
public final class ProxyAppearanceController {
    public interface Scene {
        Object create(Object owner,String slot,String marker,NativeProxyController.Snapshot state);
        default Object create(Object owner,String slot,String marker,String itemId,NativeProxyController.Snapshot state){return create(owner,slot,marker,state);}
        void update(Object marker,NativeProxyController.Snapshot state);
        void delete(Object marker);
    }
    private record Part(String marker,String itemId,Object handle) { }
    private static final class Entry {
        NativeProxyController.Snapshot state;
        final Map<String,Part> parts=new LinkedHashMap<>();
        Entry(NativeProxyController.Snapshot state){this.state=state;}
    }
    private final Map<Object,Entry> entries=new IdentityHashMap<>();
    private final Scene scene;
    private final Function<NativeProxyController.Snapshot,EquipmentVisualState> visual;
    private final ProxyAppearanceAdapter adapter;
    private final Consumer<String> log;
    private final boolean rendererMode;
    public ProxyAppearanceController(Scene scene,Function<NativeProxyController.Snapshot,EquipmentVisualState> visual,ProxyAppearanceAdapter adapter,Consumer<String> log){this(scene,visual,adapter,log,false);}
    public ProxyAppearanceController(Scene scene,Function<NativeProxyController.Snapshot,EquipmentVisualState> visual,ProxyAppearanceAdapter adapter,Consumer<String> log,boolean rendererMode){this.scene=scene;this.visual=visual;this.adapter=adapter;this.log=log;this.rendererMode=rendererMode;}
    public void attach(Object owner,NativeProxyController.Snapshot state){if(!state.type().equals("7dtd:player"))return;detach(owner);if(entries.size()>=256)throw new IllegalStateException("appearance capacity");entries.put(owner,new Entry(state));}
    public void update(Object owner,NativeProxyController.Snapshot state){var entry=entries.get(owner);if(entry!=null)entry.state=state;else attach(owner,state);}
    public void tick() {
        for(var owner:entries.entrySet()) {
            var entry=owner.getValue();var snapshot=visual.apply(entry.state);var desired=rendererMode ? adapter.generateForRenderer(snapshot) : adapter.generate(snapshot);
            reconcile(owner.getKey(),entry,"held_item",desired.heldMarker(),desired.heldItemId());reconcile(owner.getKey(),entry,"head",desired.headMarker(),desired.headItemId());
        }
    }
    private void reconcile(Object owner,Entry entry,String slot,String desired,String itemId) {
        var old=entry.parts.get(slot);
        if(old!=null && (!Objects.equals(old.marker,desired) || !Objects.equals(old.itemId,itemId))) {remove(old);entry.parts.remove(slot);log.accept("Proxy Appearance Remove: id="+entry.state.id()+" slot="+slot+" marker="+old.marker);old=null;}
        if(desired==null)return;
        if(old==null) {
            try {
                var handle=Objects.requireNonNull(scene.create(owner,slot,desired,itemId,entry.state));
                old=new Part(desired,itemId,handle);entry.parts.put(slot,old);
                log.accept("Proxy Appearance Create: id="+entry.state.id()+" slot="+slot+" marker="+desired);
            }catch(RuntimeException ex){entry.parts.put(slot,new Part(desired,itemId,null));warning(ex);return;}
        }
        if(old.handle!=null)try{scene.update(old.handle,entry.state);}
        catch(RuntimeException ex){remove(old);entry.parts.put(slot,new Part(desired,itemId,null));warning(ex);}
    }
    private void remove(Part part){if(part.handle!=null)try{scene.delete(part.handle);}catch(RuntimeException ex){warning(ex);}}
    private void warning(RuntimeException ex){log.accept("Proxy Appearance marker unavailable: "+ex.getClass().getSimpleName());}
    public void detach(Object owner){var entry=entries.remove(owner);if(entry!=null){for(var part:entry.parts.values())remove(part);log.accept("Proxy Appearance Cleanup: id="+entry.state.id()+" parts="+entry.parts.size());}}
    public void reset(){for(var owner:new ArrayList<>(entries.keySet()))detach(owner);}
    public int ownerCount(){return entries.size();}
    public int markerCount(){return entries.values().stream().mapToInt(e->(int)e.parts.values().stream().filter(p->p.handle!=null).count()).sum();}
}
