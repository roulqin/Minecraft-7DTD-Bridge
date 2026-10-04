package io.mc7dtd;

import com.google.gson.*;
import java.util.Objects;
import java.util.function.Consumer;

/** Wraps physical adapters; creation and update failures never escape to entity synchronization. */
public final class FallbackRendererAdapter implements RendererAdapter {
    public static final class Handle {
        private Object object;
        private boolean fallback,removed;
        private String reason;
        private Context context;
        private final Request request;
        private Handle(Context context,Request request){this.context=context;this.request=request;}
    }
    private final RendererAdapter item,marker;
    private final Consumer<String> log;
    public FallbackRendererAdapter(RendererAdapter item,RendererAdapter marker,Consumer<String> log){this.item=item;this.marker=marker;this.log=log;}
    public Object create(Context context,Request request) {
        var handle=new Handle(context,request);
        try {if(request.mapping()==null)throw new IllegalArgumentException("mapping_missing");handle.object=Objects.requireNonNull(item.create(context,request));}
        catch(RuntimeException ex){fallback(handle,ex);}
        log.accept("Renderer Create: id="+context.state().id()+" renderer_status="+(handle.fallback ? "fallback_marker" : "item_display"));return handle;
    }
    private void fallback(Handle handle,RuntimeException failure){handle.fallback=true;handle.reason=failure.getMessage()==null ? failure.getClass().getSimpleName() : failure.getMessage();try{handle.object=Objects.requireNonNull(marker.create(handle.context,handle.request));}catch(RuntimeException ex){handle.object=null;handle.reason+="; marker unavailable: "+ex.getClass().getSimpleName();}log.accept("Renderer Fallback: id="+handle.context.state().id()+" reason="+handle.reason);}
    public void update(Object value,Context context) {
        var handle=(Handle)value;if(handle.removed)return;handle.context=context;if(handle.object==null)return;
        try{(handle.fallback ? marker : item).update(handle.object,context);}
        catch(RuntimeException ex){removePhysical(handle);if(!handle.fallback)fallback(handle,ex);else{handle.object=null;handle.reason="fallback update failed: "+ex.getClass().getSimpleName();}}
    }
    private void removePhysical(Handle handle){if(handle.object!=null)try{(handle.fallback ? marker : item).remove(handle.object);}catch(RuntimeException ex){log.accept("Renderer removal failed: "+ex.getClass().getSimpleName());}finally{handle.object=null;}}
    public void remove(Object value){var handle=(Handle)value;if(handle.removed)return;removePhysical(handle);handle.removed=true;log.accept("Renderer Remove: id="+handle.context.state().id());}
    public static JsonObject state(Object value) {
        var handle=(Handle)value;var result=new JsonObject();result.addProperty("type",handle.fallback ? "block_display" : "item_display");result.addProperty("status",handle.removed ? "removed" : handle.object==null ? "unavailable" : handle.fallback ? "fallback" : "active");result.addProperty("renderer_status",handle.fallback ? "fallback_marker" : "item_display");
        result.addProperty("source_item",handle.request.sourceItemId());if(!handle.fallback)result.addProperty("item",handle.request.mapping().item());if(handle.reason!=null)result.addProperty("reason",handle.reason);return result;
    }
}
