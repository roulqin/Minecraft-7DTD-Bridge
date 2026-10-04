package io.mc7dtd;

/** Consumes only immutable EquipmentVisualState; never reads source Equipment or writes components. */
public final class ProxyAppearanceAdapter {
    private final ProxyAppearanceConfig config;
    public ProxyAppearanceAdapter(ProxyAppearanceConfig config){this.config=config;}
    public ProxyAppearance generate(EquipmentVisualState visual) {
        if(!config.enabled() || visual==null || !visual.known())return ProxyAppearance.EMPTY;
        String held=marker(visual.slot("held_item")),head=marker(visual.slot("head"));
        if(!java.util.Set.of("club","torch","axe").contains(held==null ? "" : held))held=null;
        if(!"helmet".equals(head))head=null;
        return new ProxyAppearance(held,head,held==null ? null : visual.slot("held_item").itemId(),head==null ? null : visual.slot("head").itemId());
    }
    private String marker(EquipmentVisualState.Slot slot){return slot==null ? null : config.mapping().get(slot.itemId());}
    /** Renderer v1 gives unmapped held items a generic marker without changing the legacy generator. */
    public ProxyAppearance generateForRenderer(EquipmentVisualState visual) {
        var result=generate(visual);
        if(!config.enabled() || visual==null || !visual.known() || visual.slot("held_item")==null)return result;
        return new ProxyAppearance(result.heldMarker()==null ? "unknown" : result.heldMarker(),result.headMarker(),visual.slot("held_item").itemId(),result.headItemId());
    }
}
