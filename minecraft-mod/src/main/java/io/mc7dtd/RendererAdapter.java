package io.mc7dtd;

/** Client-thread local display contract. No component mutation or network messages. */
public interface RendererAdapter {
    record Context(Object owner,NativeProxyController.Snapshot state) { }
    record Request(String sourceItemId,String marker,RendererMapping.Entry mapping) { }
    Object create(Context context,Request request);
    void update(Object handle,Context context);
    void remove(Object handle);
}
