package io.mc7dtd;

import com.mojang.brigadier.arguments.StringArgumentType;
import net.fabricmc.fabric.api.client.command.v2.ClientCommandRegistrationCallback;
import net.minecraft.client.MinecraftClient;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.server.world.ServerWorld;
import net.minecraft.text.Text;
import net.minecraft.util.math.BlockPos;
import net.minecraft.util.math.Box;
import net.minecraft.world.Heightmap;
import java.util.Set;
import java.util.function.Consumer;
import static net.fabricmc.fabric.api.client.command.v2.ClientCommandManager.*;

/** Integrated-server teleportation, never client-only position edits or Bridge messages. */
public final class MinecraftDebugNavigation {
    private final EntityDebugNavigation navigation;
    private final Consumer<String> log;
    private Object world;
    private int ticks;
    private volatile long generation;
    private volatile boolean pending;
    public MinecraftDebugNavigation(EntityInspector inspector,DebugConfig config,Consumer<String> log){navigation=new EntityDebugNavigation(inspector,config);this.log=log;}
    public void register() {
        if(!navigation.enabled())return;
        ClientCommandRegistrationCallback.EVENT.register((dispatcher,registry)->{
            dispatcher.register(literal("mc7dtd_entity_nearest").executes(c->run(()->feedback(EntityDebugNavigation.describe(navigation.nearest(origin()),origin())))));
            dispatcher.register(literal("mc7dtd_entity_list").executes(c->run(()->feedback(navigation.formatList(origin())))));
            EntityGotoCommand.register(dispatcher,navigation.enabled(),()->navigation.list(new EntityDebugNavigation.Position(0,0,0)).stream().map(EntityDebugNavigation.Target::id).toList(),id->run(()->{var target=EntityGotoCommand.resolve(navigation,id);requireServer();navigation.stop();generation++;teleport(target,false);}));
            dispatcher.register(literal("mc7dtd_entity_follow").then(argument("id",StringArgumentType.word()).executes(c->run(()->{requireServer();generation++;boolean active=navigation.toggleFollow(StringArgumentType.getString(c,"id"));feedback(active ? "Entity follow started" : "Entity follow stopped");if(active)teleport(navigation.followed(),true);}))));
        });
    }
    private int run(Runnable action){try{action.run();return 1;}catch(IllegalArgumentException|IllegalStateException ex){feedback(ex.getMessage());return 0;}}
    private EntityDebugNavigation.Position origin(){var player=MinecraftClient.getInstance().player;if(player==null)throw new IllegalStateException("Enter a Minecraft world first");return new EntityDebugNavigation.Position(player.getX(),player.getY(),player.getZ());}
    private void requireServer(){origin();if(MinecraftClient.getInstance().getServer()==null)throw new IllegalStateException("Safe debug teleport/follow requires a single-player integrated server");}
    private void feedback(String message){var game=MinecraftClient.getInstance();if(game.player!=null)game.player.sendMessage(Text.literal(message),false);log.accept(message);}
    public void tick(MinecraftClient game) {
        if(!navigation.enabled())return;
        if(world!=game.world){world=game.world;navigation.stop();generation++;}
        if(game.world==null || game.player==null){navigation.stop();generation++;return;}
        if(++ticks%10!=0 || pending)return;
        var target=navigation.followed();
        if(target==null)return;
        if(game.getServer()==null){navigation.stop();generation++;feedback("Entity follow stopped: integrated server unavailable");return;}
        teleport(target,true);
    }
    private void teleport(EntityDebugNavigation.Target target,boolean following) {
        if(target==null || pending)return;
        var game=MinecraftClient.getInstance();var server=game.getServer();var playerId=game.player.getUuid();var expectedWorld=game.world;long token=generation;
        pending=true;
        server.execute(()->{
            String message=null;
            try {
                if(token!=generation)return;
                var current=navigation.find(target.id());if(!current.scope().equals(target.scope()))throw new IllegalStateException("Entity stream changed; navigation cancelled");
                var player=server.getPlayerManager().getPlayer(playerId);if(player==null)return;
                var serverWorld=player.getEntityWorld();
                var landing=EntityDebugNavigation.safeOffset(current,p->safe(serverWorld,player,landing(serverWorld,p)));
                landing=landing(serverWorld,landing);
                if(token!=generation)return;
                if(!player.teleport(serverWorld,landing.x(),landing.y(),landing.z(),Set.of(),player.getYaw(),player.getPitch(),true))throw new IllegalStateException("Server rejected debug teleport");
                player.fallDistance=0;
                if(!following)message=String.format(java.util.Locale.ROOT,"Entity teleport: %s → %.3f %.3f %.3f",target.id(),landing.x(),landing.y(),landing.z());
            }catch(RuntimeException ex){message="Entity navigation cancelled: "+ex.getMessage();if(following)game.execute(()->{if(token==generation){navigation.stop();generation++;}});}
            finally{pending=false;if(message!=null){String output=message;game.execute(()->{if(game.world==expectedWorld)feedback(output);});}}
        });
    }
    private static EntityDebugNavigation.Position landing(ServerWorld world,EntityDebugNavigation.Position p) {
        int x=(int)Math.floor(p.x()),z=(int)Math.floor(p.z());
        if(!world.getWorldBorder().contains(new BlockPos(x,0,z)))return p;
        int surface=world.getTopY(Heightmap.Type.MOTION_BLOCKING_NO_LEAVES,x,z);
        if(Math.abs(surface-(p.y()-3))>8)return new EntityDebugNavigation.Position(p.x(),Double.NaN,p.z());
        return new EntityDebugNavigation.Position(p.x(),surface,p.z());
    }
    private static boolean safe(ServerWorld world,ServerPlayerEntity player,EntityDebugNavigation.Position p) {
        if(!Double.isFinite(p.y()))return false;
        double half=Math.max(.3,player.getWidth()/2.0),height=Math.max(1.8,player.getHeight());
        var box=new Box(p.x()-half,p.y(),p.z()-half,p.x()+half,p.y()+height,p.z()+half);
        if(p.y()<world.getBottomY()+1 || p.y()+2>=world.getTopYInclusive() || !world.getWorldBorder().contains(box) || !world.isSpaceEmpty(player,box))return false;
        for(var pos:BlockPos.iterate((int)Math.floor(box.minX),(int)Math.floor(box.minY)-1,(int)Math.floor(box.minZ),(int)Math.floor(box.maxX),(int)Math.floor(box.maxY),(int)Math.floor(box.maxZ))) {
            if(!world.getFluidState(pos).isEmpty())return false;
            var block=world.getBlockState(pos).getBlock();
            if(block==net.minecraft.block.Blocks.MAGMA_BLOCK || block==net.minecraft.block.Blocks.CACTUS || block==net.minecraft.block.Blocks.FIRE || block==net.minecraft.block.Blocks.SOUL_FIRE || block==net.minecraft.block.Blocks.CAMPFIRE || block==net.minecraft.block.Blocks.SOUL_CAMPFIRE || block==net.minecraft.block.Blocks.POWDER_SNOW || block==net.minecraft.block.Blocks.SWEET_BERRY_BUSH)return false;
        }
        return !world.isSpaceEmpty(player,box.offset(0,-.1,0));
    }
}
