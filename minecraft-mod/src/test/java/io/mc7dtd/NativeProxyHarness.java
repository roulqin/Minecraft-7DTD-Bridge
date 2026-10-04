package io.mc7dtd;
import com.google.gson.*;
import java.nio.file.*;
import java.util.*;

public final class NativeProxyHarness {
 static int passed;
 static void check(boolean value,String name){if(!value)throw new AssertionError(name);passed++;System.out.println("PASS "+name);}
 static final class Scene implements NativeProxyController.Scene {
   int created, updated, deleted; final Map<Object,NativeProxyController.Snapshot> handles=new HashMap<>();
   public Object create(NativeProxyController.Snapshot s){Object h=new Object();handles.put(h,s);created++;return h;}
   public void update(Object h,NativeProxyController.Snapshot s){handles.put(h,s);updated++;}
   public void delete(Object h){if(handles.remove(h)==null)throw new AssertionError("double deletion");deleted++;}
 }
 static JsonObject message(Path root,String event)throws Exception {
   var node=JsonParser.parseString(Files.readString(root.resolve("docs/examples/entity_state_v2/7dtd_"+event+".json"))).getAsJsonObject();
   node.addProperty("entity_type","7dtd:player");if(!event.equals("despawn"))node.getAsJsonObject("position").addProperty("space","minecraft");return node;
 }
 public static void main(String[] args)throws Exception {
   Path root=Path.of(args[0]); var json=JsonParser.parseString(Files.readString(root.resolve("config/entity_types.json"))).getAsJsonObject();var catalog=new ProxyTypeCatalog(json);
   check(catalog.supports("7dtd:player"),"enabled reverse mapping");check(!catalog.supports("minecraft:player"),"v1 path excluded from reverse adapter");check(!catalog.supports("7dtd:zombie"),"unknown source type excluded");
   var disabled=json.deepCopy();disabled.getAsJsonArray("types").get(2).getAsJsonObject().addProperty("enabled",false);check(!new ProxyTypeCatalog(disabled).supports("7dtd:player"),"disabled mapping excluded");
   for(String capability:List.of("ai","combat","collision","health","persistence")){var bad=json.deepCopy();bad.getAsJsonArray("types").get(2).getAsJsonObject().getAsJsonObject("capabilities").addProperty(capability,true);boolean rejected=false;try{new ProxyTypeCatalog(bad);}catch(IllegalArgumentException e){rejected=true;}check(rejected,"forbidden capability "+capability);}
   var scene=new Scene();var logs=new ArrayList<String>();var c=new NativeProxyController(scene,catalog,logs::add);Object world=new Object();
   var spawn=message(root,"spawn");c.enqueue(spawn);c.tick(null);check(c.activeCount()==1&&scene.created==0,"main menu buffers authoritative spawn without creating display");
   c.tick(world);check(scene.created==1,"world entry materializes buffered spawn");
   c.enqueue(spawn);c.tick(world);check(scene.created==1,"duplicate spawn creates no second display");
   var update=message(root,"update");update.getAsJsonObject("position").addProperty("x",123);update.getAsJsonObject("rotation").addProperty("pitch",15);c.enqueue(update);c.tick(world);check(scene.handles.values().iterator().next().x()==123&&scene.handles.values().iterator().next().pitch()==15,"update applies mapped position and rotation");
   int calls=scene.updated;c.enqueue(update);c.tick(world);check(scene.updated==calls,"stale update ignored");
   c.tick(null);check(scene.deleted==1&&c.activeCount()==1,"world exit removes local handles without forging source despawn");
   var secondWorld=new Object();c.tick(secondWorld);check(scene.created==2,"new client world rebuilds active snapshot");
   c.enqueue(message(root,"despawn"));c.tick(secondWorld);check(c.activeCount()==0&&scene.handles.isEmpty(),"explicit source despawn clears displays");
   int creates=scene.created;c.enqueue(update);c.tick(world);check(scene.created==creates,"retired update cannot resurrect proxy");
   var late=spawn.deepCopy();late.addProperty("sequence",100);c.enqueue(late);c.tick(world);check(scene.created==creates,"retired identity spawn ignored");
   c.reset();c.enqueue(spawn);c.tick(world);check(c.activeCount()==1&&scene.created==creates+1,"controlled reset accepts fresh snapshot");
   var forged=update.deepCopy();forged.addProperty("authority","minecraft");c.enqueue(forged);c.tick(world);check(scene.updated==calls+2,"forged authority cannot mutate display");
   var foreign=update.deepCopy();foreign.addProperty("stream_id",UUID.randomUUID().toString());int old=scene.updated;c.enqueue(foreign);c.tick(world);check(scene.updated==old,"stream mismatch ignored");
   var invalid=update.deepCopy();invalid.getAsJsonObject("position").addProperty("space","7dtd");c.enqueue(invalid);c.tick(world);check(scene.updated==old,"unmapped space rejected");
   var wrongType=update.deepCopy();wrongType.addProperty("entity_type","7dtd:zombie");c.enqueue(wrongType);c.tick(world);check(scene.updated==old,"unconfigured type does not fall back to player");
   c.reset();c.tick(world);check(scene.handles.isEmpty(),"disconnect reset deletes active display");
   c.enqueue(update);c.tick(world);check(scene.created==creates+1,"unknown update cannot create object");
   for(int i=0;i<512;i++)c.enqueue(spawn);boolean full=false;try{c.enqueue(spawn);}catch(IllegalStateException e){full=true;}check(full,"bounded pending queue fails closed");c.reset();c.tick(world);
   var isolated=new Scene();var controller=new NativeProxyController(isolated,catalog,logs::add);var before=spawn.deepCopy();controller.enqueue(before);before.getAsJsonObject("position").addProperty("x",999);controller.tick(world);check(isolated.handles.values().iterator().next().x()!=999,"queued snapshots immutable against caller mutation");
   check(isolated.handles.values().iterator().next().roll()==0,"complete rotation state retained");
   System.out.println("TOTAL "+passed+" passed");
 }
}
