package io.mc7dtd;

import net.minecraft.client.world.ClientWorld;
import net.minecraft.entity.Entity;
import net.minecraft.entity.EntityType;
import net.minecraft.entity.decoration.DisplayEntity.ItemDisplayEntity;
import net.minecraft.item.ItemDisplayContext;
import net.minecraft.item.ItemStack;
import net.minecraft.item.Items;
import net.minecraft.registry.Registries;
import net.minecraft.util.Identifier;
import net.minecraft.util.math.AffineTransformation;
import org.joml.Quaternionf;
import org.joml.Vector3f;
import java.util.function.Function;
import java.util.function.ToIntFunction;

/** Actual client-world ItemDisplay backend. Creation failure rolls back its entity. */
public final class ItemDisplayRendererAdapter implements RendererAdapter {
    private record Handle(ClientWorld world,ItemDisplayEntity entity,RendererMapping.Entry spec) { }
    private final Function<Object,ClientWorld> worlds;
    private final ToIntFunction<ClientWorld> ids;
    public ItemDisplayRendererAdapter(Function<Object,ClientWorld> worlds,ToIntFunction<ClientWorld> ids){this.worlds=worlds;this.ids=ids;}
    public Object create(Context context,Request request) {
        var spec=request.mapping();var id=Identifier.tryParse(spec.item());
        if(id==null || !Registries.ITEM.containsId(id))throw new IllegalArgumentException("item_not_found: "+spec.item());
        var item=Registries.ITEM.get(id);if(item==Items.AIR)throw new IllegalArgumentException("item_is_air");
        var world=worlds.apply(context.owner());var entity=new ItemDisplayEntity(EntityType.ITEM_DISPLAY,world);entity.setId(ids.applyAsInt(world));
        var handle=new Handle(world,entity,spec);
        try {
            entity.setNoGravity(true);entity.setInterpolationDuration(0);entity.setTeleportDuration(0);entity.setDisplayWidth(4);entity.setDisplayHeight(4);
            entity.setItemStack(new ItemStack(item,1));entity.setItemDisplayContext(ItemDisplayContext.FIXED);
            update(handle,context);world.addEntity(entity);
            if(world.getEntityById(entity.getId())!=entity)throw new IllegalStateException("item_display_not_registered");
            return handle;
        }catch(RuntimeException ex){world.removeEntity(entity.getId(),Entity.RemovalReason.DISCARDED);throw ex;}
    }
    public void update(Object value,Context context) {
        var handle=(Handle)value;if(worlds.apply(context.owner())!=handle.world)throw new IllegalStateException("renderer_world_changed");
        if(handle.entity.isRemoved())throw new IllegalStateException("item_display_removed");
        var state=context.state();var spec=handle.spec;
        var worldRotation=new Quaternionf().rotationYXZ((float)Math.toRadians(-state.yaw()),(float)Math.toRadians(state.pitch()),(float)Math.toRadians(state.roll()));
        var offset=spec.offset();var translation=new Vector3f((float)offset.x(),(float)offset.y(),(float)offset.z());worldRotation.transform(translation);
        var local=spec.rotation();var localRotation=new Quaternionf().rotationYXZ((float)Math.toRadians(local.y()),(float)Math.toRadians(local.x()),(float)Math.toRadians(local.z()));
        handle.entity.setPosition(state.x(),state.y(),state.z());
        handle.entity.setTransformation(new AffineTransformation(translation,worldRotation.mul(localRotation),new Vector3f((float)spec.scale()),new Quaternionf()));
    }
    public void remove(Object value){var handle=(Handle)value;handle.world.removeEntity(handle.entity.getId(),Entity.RemovalReason.DISCARDED);}
}
