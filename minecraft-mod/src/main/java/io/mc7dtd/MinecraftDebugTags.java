package io.mc7dtd;

import net.minecraft.client.MinecraftClient;
import net.minecraft.client.world.ClientWorld;
import net.minecraft.entity.Entity;
import net.minecraft.entity.EntityType;
import net.minecraft.entity.decoration.DisplayEntity;
import net.minecraft.entity.decoration.DisplayEntity.TextDisplayEntity;
import net.minecraft.text.Text;

/** Disposable world-space text only; no HUD, skin or model substitution. All calls on client thread. */
public final class MinecraftDebugTags {
    private record Entry(ClientWorld world,TextDisplayEntity text) { }
    private final DebugNameTagLayer layer;
    private int nextId=-2_000_000;
    public MinecraftDebugTags(EntityInspector inspector,DebugConfig config) {
        layer=new DebugNameTagLayer(inspector,config,new DebugNameTagLayer.Scene() {
            public Object create(NativeProxyController.Snapshot state,String value) {
                var world=MinecraftClient.getInstance().world;if(world==null)throw new IllegalStateException("World unavailable");
                var text=new TextDisplayEntity(EntityType.TEXT_DISPLAY,world);
                while(world.getEntityById(nextId)!=null)nextId--;
                text.setId(nextId--);text.setNoGravity(true);text.setBillboardMode(DisplayEntity.BillboardMode.CENTER);text.setLineWidth(500);
                text.setDisplayWidth(8);text.setDisplayHeight(4);var entry=new Entry(world,text);
                try { update(entry,state,value);world.addEntity(text);return entry; }
                catch(RuntimeException ex){world.removeEntity(text.getId(),Entity.RemovalReason.DISCARDED);throw ex;}
            }
            public void update(Object handle,NativeProxyController.Snapshot state,String value) {
                var entry=(Entry)handle;entry.text().setPosition(state.x(),state.y()+2.2,state.z());
                if(!entry.text().getText().getString().equals(value))entry.text().setText(Text.literal(value));
            }
            public void delete(Object handle) { var entry=(Entry)handle;entry.world().removeEntity(entry.text().getId(),Entity.RemovalReason.DISCARDED); }
        }, message->org.slf4j.LoggerFactory.getLogger("MC7DTD").info(message));
    }
    public void attach(NativeProxyController.Snapshot state) { layer.attach(state); }
    public void update(NativeProxyController.Snapshot state) { layer.update(state); }
    public void tick() { layer.tick(); }
    public void detach(NativeProxyController.Snapshot state) { layer.detach(state); }
}
