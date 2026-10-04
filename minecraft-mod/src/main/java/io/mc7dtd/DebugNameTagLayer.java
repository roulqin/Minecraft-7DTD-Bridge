package io.mc7dtd;

import java.util.*;
import java.util.function.Consumer;

/** Client-thread adapter with a headless-testable config gate and disposable label lifecycle. */
public final class DebugNameTagLayer {
    public interface Scene {
        Object create(NativeProxyController.Snapshot state,String text);
        void update(Object handle,NativeProxyController.Snapshot state,String text);
        void delete(Object handle);
    }
    private record Entry(Object handle,NativeProxyController.Snapshot state) { }
    private final Map<String,Entry> tags=new HashMap<>();
    private final EntityInspector inspector;
    private final DebugConfig config;
    private final Scene scene;
    private final Consumer<String> log;
    public DebugNameTagLayer(EntityInspector inspector,DebugConfig config,Scene scene,Consumer<String> log) { this.inspector=inspector;this.config=config;this.scene=scene;this.log=log; }
    private static String key(NativeProxyController.Snapshot s) { return s.world()+"|"+s.dimension()+"|"+s.id()+"|"+s.stream(); }
    private String text(NativeProxyController.Snapshot s) { return EntityInspector.tag(inspector.find(s.id(),s.world(),s.dimension())); }
    public void attach(NativeProxyController.Snapshot state) {
        if(!config.nameTag())return;
        detach(state);
        try { tags.put(key(state),new Entry(scene.create(state,text(state)),state)); }
        catch(RuntimeException ex) { warning(ex); }
    }
    public void update(NativeProxyController.Snapshot state) {
        var entry=tags.get(key(state));if(entry==null)return;
        tags.put(key(state),new Entry(entry.handle(),state));
        try { scene.update(entry.handle(),state,text(state)); } catch(RuntimeException ex) { warning(ex); }
    }
    public void tick() { for(var entry:tags.values())try{scene.update(entry.handle(),entry.state(),text(entry.state()));}catch(RuntimeException ex){warning(ex);} }
    public void detach(NativeProxyController.Snapshot state) { var entry=tags.remove(key(state));if(entry!=null)try{scene.delete(entry.handle());}catch(RuntimeException ex){warning(ex);} }
    private void warning(RuntimeException ex) { if(config.logging())log.accept("[Entity Debug] Name tag unavailable: "+ex.getClass().getSimpleName()); }
    public int count() { return tags.size(); }
}
