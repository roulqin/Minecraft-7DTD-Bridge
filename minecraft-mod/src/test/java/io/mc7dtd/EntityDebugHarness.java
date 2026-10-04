package io.mc7dtd;

import com.google.gson.*;
import java.nio.file.*;
import java.util.*;

public final class EntityDebugHarness {
    private static int passed;
    private static void check(boolean ok,String label) { if(!ok)throw new AssertionError(label);passed++;System.out.println("PASS "+label); }
    public static void main(String[] args) throws Exception {
        if(args.length>1 && args[1].equals("listen")) {
            try(var client=new BridgeClient(Path.of(args[0]),System.out::println)) {
                client.start();String previous="";
                for(int i=0;i<1200;i++) {
                    var snapshot=client.inspector().latest();String state=snapshot==null ? "empty" : snapshot.toString();
                    if(!state.equals(previous)){System.out.println("DEBUG SNAPSHOT "+state);previous=state;}
                    Thread.sleep(100);
                }
            }return;
        }
        Path root=Path.of(args[0]);var e=JsonParser.parseString(Files.readString(root.resolve("docs/examples/presentation/spawn.json"))).getAsJsonObject();
        e.getAsJsonObject("position").addProperty("space","minecraft");var inspector=new EntityInspector();
        var c=new JsonObject();var h=new JsonObject();h.addProperty("current",20);h.addProperty("max",20);c.add("health",h);
        var n=new JsonObject();n.addProperty("text","Steve");n.addProperty("display_name","Steve Display");c.add("name",n);
        var tags=new JsonObject();tags.addProperty("tag.role","test");c.add("custom_metadata",tags);
        var observed=new HealthReceiver.State(1,20.0,20.0,c);
        inspector.lifecycle(e,observed,e.getAsJsonObject("components").getAsJsonObject("presentation"));
        var snapshot=inspector.latest();check(snapshot.getAsJsonObject("components").getAsJsonObject("identity").get("name").getAsString().equals("Steve") && EntityInspector.tag(snapshot).contains("HP:20") && EntityInspector.format(snapshot).contains("owner"),"Entity Inspector");
        equipmentFormatting(inspector,e,observed);
        snapshot.getAsJsonObject("components").getAsJsonObject("health").addProperty("current",0);
        check(inspector.latest().getAsJsonObject("components").getAsJsonObject("health").get("current").getAsDouble()==20,"read-only copy isolation");
        var second=e.deepCopy();second.addProperty("entity_id",UUID.randomUUID().toString());second.getAsJsonObject("origin").add("entity_id",second.get("entity_id").deepCopy());second.remove("components");
        inspector.lifecycle(second,null,null);check(inspector.latest().getAsJsonObject("components").getAsJsonObject("presentation").size()==0 && EntityInspector.tag(inspector.latest()).contains("HP:unknown"),"Missing Component");
        check(inspector.list().size()==2 && inspector.formatList().contains("Synced Entities: 2"),"Entity List");
        check(EntityInspector.format(inspector.latest()).contains(second.get("entity_id").getAsString()),"Debug Command");
        var name=e.getAsJsonObject("components").getAsJsonObject("presentation").get("model").getAsString();
        var state=new NativeProxyController.Snapshot(e.get("entity_id").getAsString(),e.get("stream_id").getAsString(),e.get("entity_type").getAsString(),e.get("world_id").getAsString(),e.get("dimension").getAsString(),1,"spawn",1,2,3,0,0,0);
        var fake=new FakeScene();var logs=new ArrayList<String>();var layer=new DebugNameTagLayer(inspector,new DebugConfig(true,true),fake,logs::add);
        layer.attach(state);check(fake.created==1 && fake.text.contains(name) && fake.text.contains("HP:20"),"name tag entity/model/health");
        c.getAsJsonObject("health").addProperty("current",12);inspector.components(e,new HealthReceiver.State(2,12.0,20.0,c));layer.tick();
        check(fake.text.contains("HP:12") && fake.created==1,"health-only name tag refresh without position update");
        layer.detach(state);check(fake.deleted==1 && layer.count()==0,"name tag despawn cleanup");
        var disabledScene=new FakeScene();var disabled=new DebugNameTagLayer(inspector,new DebugConfig(false,false),disabledScene,logs::add);disabled.attach(state);disabled.update(state);disabled.tick();
        check(disabledScene.created==0 && disabledScene.updated==0 && logs.isEmpty(),"Debug Config");
        var failing=new DebugNameTagLayer(inspector,new DebugConfig(true,false),new DebugNameTagLayer.Scene(){public Object create(NativeProxyController.Snapshot s,String t){throw new IllegalStateException();}public void update(Object h,NativeProxyController.Snapshot s,String t){}public void delete(Object h){}},logs::add);failing.attach(state);
        check(failing.count()==0 && logs.isEmpty(),"tag failure isolated and logging disabled");
        var config=root.resolve("work/phase3_7_4-test/java-debug.json");Files.writeString(config,"{\"debug_name_tag\":false,\"debug_logging\":false}");
        check(!DebugConfig.load(config,logs::add).nameTag() && !DebugConfig.load(config,logs::add).logging(),"disabled configuration loaded");
        Files.writeString(config,"{\"debug_name_tag\":\"true\",\"debug_logging\":true}");check(!DebugConfig.load(config,logs::add).nameTag(),"malformed config fails closed");
        e.getAsJsonObject("lifecycle").addProperty("event","despawn");inspector.lifecycle(e,null,null);check(inspector.list().size()==1 && !inspector.formatList().contains(e.get("entity_id").getAsString()),"despawn not retained in inspector");
        inspector.reset();check(inspector.latest()==null && inspector.list().isEmpty() && EntityInspector.format(null).contains("no synced"),"reconnect and empty commands safe");
        System.out.println("TOTAL "+passed+" passed");
    }
    private static JsonObject formatted(JsonObject entity) {
        String text=EntityInspector.format(entity);
        return JsonParser.parseString(text.substring(text.indexOf('{'),text.lastIndexOf('}')+1)).getAsJsonObject();
    }
    private static void equipmentFormatting(EntityInspector inspector,JsonObject entity,HealthReceiver.State observed) {
        var names=List.of("head","body","hands","feet","held_item");
        var baseline=inspector.latest();var equipment=new JsonObject();var slots=new JsonObject();equipment.add("slots",slots);
        for(String name:names)slots.add(name,JsonNull.INSTANCE);
        inspector.components(entity,observed,equipment);
        var allEmpty=formatted(inspector.latest()).getAsJsonObject("components").getAsJsonObject("equipment").getAsJsonObject("slots");
        check(allEmpty.keySet().equals(new HashSet<>(names)) && names.stream().allMatch(n->allEmpty.get(n).isJsonNull()),"Inspector equipment all empty slots displayed");
        for(String name:names)if(!name.equals("head")) {
            var item=new JsonObject();item.addProperty("item_id","7dtd:test_"+name);slots.add(name,item);
        }
        inspector.components(entity,observed,equipment);
        var singleEmpty=formatted(inspector.latest()).getAsJsonObject("components").getAsJsonObject("equipment").getAsJsonObject("slots");
        check(singleEmpty.keySet().equals(new HashSet<>(names)) && singleEmpty.get("head").isJsonNull() && singleEmpty.equals(slots),"Inspector equipment single empty slot displayed");
        var helmet=new JsonObject();helmet.addProperty("item_id","7dtd:armorPrimitiveHelmet");slots.add("head",helmet);
        inspector.components(entity,observed,equipment);
        var stored=inspector.latest();var display=formatted(stored);
        check(display.getAsJsonObject("components").get("equipment").equals(equipment),"Inspector equipment nonempty slots displayed unchanged");
        check(List.of("health","identity","presentation","authority").stream().allMatch(n->display.getAsJsonObject("components").get(n).equals(baseline.getAsJsonObject("components").get(n)))
            && EntityInspector.tag(display).equals(EntityInspector.tag(baseline)),"Inspector Health Identity Presentation Authority display regression");
        check(inspector.latest().equals(stored) && observed.revision()==1 && equipment.equals(stored.getAsJsonObject("components").get("equipment")),"Inspector formatting preserves stored state revision and input");
    }
    private static final class FakeScene implements DebugNameTagLayer.Scene {
        int created,updated,deleted;String text;
        public Object create(NativeProxyController.Snapshot s,String t){created++;text=t;return new Object();}
        public void update(Object h,NativeProxyController.Snapshot s,String t){updated++;text=t;}
        public void delete(Object h){deleted++;}
    }
}
