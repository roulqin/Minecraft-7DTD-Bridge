package io.mc7dtd;

import com.google.gson.JsonObject;
import java.util.*;
import java.util.function.Predicate;

/** Read-only navigation over remote entities expressed in Minecraft coordinates. */
public final class EntityDebugNavigation {
    public record Position(double x,double y,double z) {
        public double distance(Position other){return Math.hypot(Math.hypot(x-other.x,z-other.z),y-other.y);}
    }
    public record Target(String id,String type,String scope,Position position) { }
    private final EntityInspector inspector;
    private final boolean enabled;
    private String following;
    public EntityDebugNavigation(EntityInspector inspector,DebugConfig config){this.inspector=inspector;enabled=config.navigation();}
    public boolean enabled(){return enabled;}
    private void requireEnabled(){if(!enabled)throw new IllegalStateException("Debug navigation disabled in config/debug.json");}
    public List<Target> list(Position origin) {
        requireEnabled();if(!inspector.appearanceConnected())return List.of();var result=new ArrayList<Target>();
        for(var entity:inspector.list()) {
            var p=entity.getAsJsonObject("position");
            if(!entity.get("source").getAsString().equals("7dtd") || p==null || !p.has("space") || !p.get("space").getAsString().equals("minecraft"))continue;
            var position=new Position(p.get("x").getAsDouble(),p.get("y").getAsDouble(),p.get("z").getAsDouble());
            if(!Double.isFinite(position.x) || !Double.isFinite(position.y) || !Double.isFinite(position.z))continue;
            result.add(new Target(entity.get("id").getAsString(),entity.get("type").getAsString(),scope(entity),position));
        }
        result.sort(Comparator.comparingDouble((Target target)->origin.distance(target.position)).thenComparing(Target::scope));return List.copyOf(result);
    }
    private static String scope(JsonObject e){return e.get("source").getAsString()+"|"+e.get("world_id").getAsString()+"|"+e.get("dimension").getAsString()+"|"+e.get("id").getAsString()+"|"+e.get("stream_id").getAsString();}
    public Target nearest(Position origin){return list(origin).stream().findFirst().orElseThrow(()->new IllegalArgumentException("No synced entities with Minecraft positions"));}
    public Target find(String id) {
        var matches=list(new Position(0,0,0)).stream().filter(t->t.id.equals(id)).toList();
        if(matches.size()!=1)throw new IllegalArgumentException(matches.isEmpty() ? "Unknown synced entity: "+id : "Ambiguous entity ID across streams/worlds: "+id);
        return matches.getFirst();
    }
    public boolean toggleFollow(String id){requireEnabled();var target=find(id);if(target.scope.equals(following)){stop();return false;}following=target.scope;return true;}
    public void stop(){following=null;}
    public Target followed(){requireEnabled();if(following==null)return null;var target=list(new Position(0,0,0)).stream().filter(t->t.scope.equals(following)).findFirst().orElse(null);if(target==null)stop();return target;}
    /** Horizontal separation avoids the proxy; the backend must additionally check terrain and player bounds. */
    public static Position safeOffset(Target target,Predicate<Position> safe) {
        for(int radius=3;radius<=5;radius++)for(int[] direction:new int[][]{{1,0},{0,1},{-1,0},{0,-1},{1,1},{-1,1},{-1,-1},{1,-1}}) {
            var p=target.position;var candidate=new Position(p.x+direction[0]*radius,p.y+3,p.z+direction[1]*radius);
            if(safe.test(candidate))return candidate;
        }
        throw new IllegalStateException("No safe landing near entity; teleport cancelled");
    }
    public static String describe(Target target,Position origin){return String.format(Locale.ROOT,"Entity Debug:\nid: %s\ntype: %s\nposition:\nx: %.3f\ny: %.3f\nz: %.3f\ndistance: %.3f",target.id,target.type,target.position.x,target.position.y,target.position.z,origin.distance(target.position));}
    public String formatList(Position origin){var result=new StringBuilder("Entity Debug: synced entities");int index=0;for(var t:list(origin))result.append(String.format(Locale.ROOT,"\n%d.\nid: %s\ntype: %s\ndistance: %.3f",++index,t.id,t.type,origin.distance(t.position)));if(index==0)result.append("\nnone");return result.toString();}
}
