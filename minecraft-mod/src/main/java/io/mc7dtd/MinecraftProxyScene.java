package io.mc7dtd;

import net.minecraft.client.MinecraftClient;
import net.minecraft.client.world.ClientWorld;
import net.minecraft.entity.Entity;
import net.minecraft.entity.EntityType;
import net.minecraft.entity.decoration.DisplayEntity.BlockDisplayEntity;
import net.minecraft.block.Blocks;
import net.minecraft.util.math.AffineTransformation;
import org.joml.Quaternionf;
import org.joml.Vector3f;
import org.slf4j.LoggerFactory;

/** Purely local display objects: no server spawn, hitbox, living entity or animator. */
public final class MinecraftProxyScene implements NativeProxyController.Scene {
    private record Handle(ClientWorld world, BlockDisplayEntity body, BlockDisplayEntity nose, NativeProxyController.Snapshot original) { }
    private MinecraftDebugTags debug;
    private record Marker(ClientWorld world,BlockDisplayEntity entity,String slot,String kind) { }
    private ProxyAppearanceController appearance;
    private EntityInspector appearanceInspector;
    private record Rendered(Object owner,Object renderer,NativeProxyController.Snapshot original) { }
    public void enableAppearance(EntityInspector inspector,ProxyAppearanceAdapter adapter,RendererMapping mapping) {
        appearanceInspector=inspector;
        inspector.rendererRuntime();
        var backend=new FallbackRendererAdapter(new ItemDisplayRendererAdapter(owner->((Handle)owner).world,this::allocateId),new RendererAdapter() {
            public Object create(Context context,Request request){return createMarker((Handle)context.owner(),"held_item",request.marker(),context.state());}
            public void update(Object handle,Context context){updateMarker((Marker)handle,context.state());}
            public void remove(Object handle){var marker=(Marker)handle;marker.world.removeEntity(marker.entity.getId(),Entity.RemovalReason.DISCARDED);}
        },LoggerFactory.getLogger("MC7DTD")::info);
        appearance=new ProxyAppearanceController(new ProxyAppearanceController.Scene() {
            public Object create(Object owner,String slot,String marker,NativeProxyController.Snapshot state){return createMarker((Handle)owner,slot,marker,state);}
            public Object create(Object owner,String slot,String marker,String itemId,NativeProxyController.Snapshot state){if(!slot.equals("held_item"))return create(owner,slot,marker,state);var value=backend.create(new RendererAdapter.Context(owner,state),new RendererAdapter.Request(itemId,marker,mapping.get(itemId)));inspector.rendererState(state,FallbackRendererAdapter.state(value));return new Rendered(owner,value,state);}
            public void update(Object value,NativeProxyController.Snapshot state){if(value instanceof Rendered rendered){backend.update(rendered.renderer,new RendererAdapter.Context(rendered.owner,state));inspector.rendererState(state,FallbackRendererAdapter.state(rendered.renderer));}else updateMarker((Marker)value,state);}
            public void delete(Object value){if(value instanceof Rendered rendered){backend.remove(rendered.renderer);inspector.rendererState(rendered.original,null);}else{var marker=(Marker)value;marker.world.removeEntity(marker.entity.getId(),Entity.RemovalReason.DISCARDED);}}
        },inspector::visualState,adapter,LoggerFactory.getLogger("MC7DTD")::info,true);
    }
    public void tickAppearance(){if(appearance!=null){if(!appearanceInspector.appearanceConnected())appearance.reset();else appearance.tick();}}
    public void enableDebug(EntityInspector inspector, DebugConfig config) { debug = new MinecraftDebugTags(inspector, config); }
    public void tickDebug() { if (debug != null) debug.tick(); }
    private int nextId = -1_000_000;
    private int allocateId(ClientWorld world){while(world.getEntityById(nextId)!=null)nextId--;return nextId--;}
    @Override public Object create(NativeProxyController.Snapshot state) {
        var world = MinecraftClient.getInstance().world;
        var body = part(world); var nose = part(world);
        try {
            body.setBlockState(Blocks.CYAN_CONCRETE.getDefaultState());
            nose.setBlockState(Blocks.GOLD_BLOCK.getDefaultState());
            world.addEntity(body); world.addEntity(nose);
            LoggerFactory.getLogger("MC7DTD").info("7DTD proxy display created: id={} body={} nose={} thread={}",
                state.id(), body.getId(), nose.getId(), Thread.currentThread().getName());
            if (debug != null) debug.attach(state);
            var handle=new Handle(world,body,nose,state);
            if(appearance!=null)appearance.attach(handle,state);
            return handle;
        } catch (RuntimeException ex) { world.removeEntity(body.getId(), Entity.RemovalReason.DISCARDED); world.removeEntity(nose.getId(), Entity.RemovalReason.DISCARDED); throw ex; }
    }
    private BlockDisplayEntity part(ClientWorld world) {
        var entity = new BlockDisplayEntity(EntityType.BLOCK_DISPLAY, world);
        entity.setId(allocateId(world)); entity.setNoGravity(true);
        entity.setInterpolationDuration(0); entity.setTeleportDuration(0);
        entity.setDisplayWidth(4); entity.setDisplayHeight(4);
        return entity;
    }
    @Override public void update(Object value, NativeProxyController.Snapshot state) {
        var handle = (Handle) value;
        if (debug != null) debug.update(state);
        var rotation = new Quaternionf().rotationYXZ((float)Math.toRadians(-state.yaw()),
            (float)Math.toRadians(state.pitch()), (float)Math.toRadians(state.roll()));
        transform(handle.body, state, rotation, new Vector3f(-0.3f, 0, -0.15f), new Vector3f(0.6f, 1.8f, 0.3f));
        transform(handle.nose, state, rotation, new Vector3f(-0.1f, 1.45f, 0.15f), new Vector3f(0.2f, 0.2f, 0.4f));
        if(appearance!=null)appearance.update(handle,state);
    }
    private Marker createMarker(Handle owner,String slot,String kind,NativeProxyController.Snapshot state) {
        var entity=part(owner.world);var marker=new Marker(owner.world,entity,slot,kind);
        try {
            entity.setBlockState(switch(kind) {
                case "club" -> Blocks.OAK_PLANKS.getDefaultState();
                case "axe","helmet" -> Blocks.IRON_BLOCK.getDefaultState();
                case "torch" -> Blocks.GLOWSTONE.getDefaultState();
                case "unknown" -> Blocks.ORANGE_CONCRETE.getDefaultState();
                default -> throw new IllegalArgumentException("unknown appearance marker");
            });
            updateMarker(marker,state);owner.world.addEntity(entity);if(owner.world.getEntityById(entity.getId())!=entity)throw new IllegalStateException("marker_not_registered");return marker;
        }catch(RuntimeException ex){owner.world.removeEntity(entity.getId(),Entity.RemovalReason.DISCARDED);throw ex;}
    }
    private static void updateMarker(Marker marker,NativeProxyController.Snapshot state) {
        var rotation=new Quaternionf().rotationYXZ((float)Math.toRadians(-state.yaw()),(float)Math.toRadians(state.pitch()),(float)Math.toRadians(state.roll()));
        Vector3f offset,scale;
        if(marker.slot.equals("head")){offset=new Vector3f(-0.34f,1.8f,-0.2f);scale=new Vector3f(0.68f,0.2f,0.4f);}
        else if(marker.kind.equals("axe")){offset=new Vector3f(0.4f,0.85f,-0.08f);scale=new Vector3f(0.35f,0.4f,0.16f);}
        else {offset=new Vector3f(0.4f,0.55f,-0.08f);scale=new Vector3f(0.16f,0.7f,0.16f);}
        transform(marker.entity,state,rotation,offset,scale);
    }
    private static void transform(BlockDisplayEntity entity, NativeProxyController.Snapshot state, Quaternionf rotation, Vector3f offset, Vector3f scale) {
        entity.setPosition(state.x(), state.y(), state.z());
        rotation.transform(offset);
        entity.setTransformation(new AffineTransformation(offset, rotation, scale, new Quaternionf()));
    }
    @Override public void delete(Object value) {
        var handle = (Handle) value;
        if(appearance!=null)appearance.detach(handle);
        if (debug != null) debug.detach(handle.original);
        handle.world.removeEntity(handle.body.getId(), Entity.RemovalReason.DISCARDED);
        handle.world.removeEntity(handle.nose.getId(), Entity.RemovalReason.DISCARDED);
        LoggerFactory.getLogger("MC7DTD").info("7DTD proxy display deleted: body={} nose={} removed={} thread={}",
            handle.body.getId(), handle.nose.getId(), handle.world.getEntityById(handle.body.getId()) == null
                && handle.world.getEntityById(handle.nose.getId()) == null, Thread.currentThread().getName());
    }
}
