package io.mc7dtd;

import com.google.gson.*;
import java.nio.file.*;
import java.util.*;

/** Real receiver/Inspector/NativeProxyController integration with a headless marker backend. */
public final class ProxyAppearanceHarness {
    static Path root;static int checks;
    static final String AXE="7dtd:meleeToolAxeT1IronFireaxe",TORCH="7dtd:meleeToolTorch",HELMET="7dtd:armorPrimitiveHelmet",CLUB="7dtd:meleeWpnClubT0WoodenClub";
    static void check(boolean ok,String label){if(!ok)throw new AssertionError(label);checks++;System.out.println("PASS "+label);}
    static JsonObject eq(String held,String head){var slots=new JsonObject();for(String slot:EquipmentVisualState.SLOTS)slots.add(slot,JsonNull.INSTANCE);if(held!=null)slots.add("held_item",item(held));if(head!=null)slots.add("head",item(head));var result=new JsonObject();result.add("slots",slots);return result;}
    static JsonObject item(String id){var result=new JsonObject();result.addProperty("item_id",id);return result;}
    static JsonObject component(String key,JsonElement value){var result=new JsonObject();result.add(key,value);return result;}
    static ProxyAppearanceConfig config;
    static final class FakeMarkers implements ProxyAppearanceController.Scene {
        record Marker(String slot,String kind) { }
        final List<String> events=new ArrayList<>();final Set<Object> active=Collections.newSetFromMap(new IdentityHashMap<>());
        boolean failCreate,failUpdate;NativeProxyController.Snapshot last;
        public Object create(Object owner,String slot,String marker,NativeProxyController.Snapshot state){events.add("create:"+marker);if(failCreate && marker.equals("axe"))throw new IllegalStateException();var result=new Marker(slot,marker);active.add(result);return result;}
        public void update(Object handle,NativeProxyController.Snapshot state){if(failUpdate)throw new IllegalStateException();last=state;}
        public void delete(Object handle){events.add("delete:"+((Marker)handle).kind());active.remove(handle);}
    }
    static final class Fixture {
        final List<String> logs=new ArrayList<>();final HealthReceiver health=new HealthReceiver(logs::add);final EquipmentReceiver equipment=new EquipmentReceiver(health,logs::add);
        final PresentationReceiver presentation=new PresentationReceiver(logs::add);final EntityInspector inspector=new EntityInspector();
        final FakeMarkers markers=new FakeMarkers();final ProxyAppearanceAdapter adapter=new ProxyAppearanceAdapter(config);
        final ProxyAppearanceController appearance=new ProxyAppearanceController(markers,inspector::visualState,adapter,logs::add);
        final NativeProxyController proxies;
        final JsonObject initial;JsonObject entity;Object world=new Object();
        Fixture()throws Exception {
            inspector.appearanceAdapter(adapter);inspector.visualWorld(world);
            initial=JsonParser.parseString(Files.readString(root.resolve("docs/examples/equipment_components/initial.json"))).getAsJsonObject();initial.getAsJsonObject("components").add("equipment",eq(null,null));
            entity=JsonParser.parseString(Files.readString(root.resolve("docs/examples/presentation/spawn.json"))).getAsJsonObject();
            for(String key:List.of("entity_id","stream_id","world_id","dimension","origin","entity_type"))entity.add(key,initial.get(key).deepCopy());entity.getAsJsonObject("position").addProperty("space","minecraft");
            proxies=new NativeProxyController(new NativeProxyController.Scene() {
                public Object create(NativeProxyController.Snapshot state){var handle=new Object();appearance.attach(handle,state);return handle;}
                public void update(Object handle,NativeProxyController.Snapshot state){appearance.update(handle,state);}
                public void delete(Object handle){appearance.detach(handle);}
            },ProxyTypeCatalog.load(root.resolve("config/entity_types.json")),logs::add);
            lifecycle(entity);receive(initial);tick();
        }
        void lifecycle(JsonObject e){health.lifecycle(e);equipment.lifecycle(e);presentation.lifecycle(e);inspector.lifecycle(e,health.state(e),presentation.get(e));inspector.components(e,health.state(e),equipment.state(e));proxies.enqueue(e);}
        void receive(JsonObject p){equipment.receive(p);inspector.components(p,health.state(p),equipment.state(p));}
        void tick(){inspector.visualWorld(world);proxies.tick(world);if(inspector.appearanceConnected())appearance.tick();else appearance.reset();}
        void patch(JsonObject values){var p=initial.deepCopy();long rev=health.state(initial).revision();p.addProperty("mode","patch");p.addProperty("base_revision",rev);p.addProperty("revision",rev+1);p.addProperty("entity_sequence",entity.get("sequence").getAsLong());p.add("components",values);receive(p);tick();}
        void slot(String slot,String id){var slots=new JsonObject();slots.add(slot,id==null ? JsonNull.INSTANCE : item(id));var value=new JsonObject();value.add("slots",slots);patch(component("equipment",value));}
        JsonObject view(){return inspector.latest().getAsJsonObject("proxy_appearance");}
    }
    public static void main(String[] args)throws Exception {
        root=Path.of(args[0]);config=ProxyAppearanceConfig.load(root.resolve("config/proxy_appearance.json"),System.out::println);
        var adapter=new ProxyAppearanceAdapter(config);var generator=EquipmentModifierGenerator.empty();
        var visual=generator.generate(eq(AXE,HELMET));var input=visual.toJson();var result=adapter.generate(visual);
        check(result.heldMarker().equals("axe") && result.headMarker().equals("helmet") && visual.toJson().equals(input),"Test 1 EquipmentVisualState generates Appearance without source writes");
        var f=new Fixture();check(f.appearance.markerCount()==0,"initial null creates no marker");f.slot("held_item",AXE);
        check(f.markers.events.equals(List.of("create:axe")) && f.appearance.markerCount()==1 && f.view().get("held_marker").getAsString().equals("axe"),"Test 2 Proxy Appearance Create null to axe");
        f.slot("held_item",TORCH);check(f.markers.events.equals(List.of("create:axe","delete:axe","create:torch")) && f.appearance.markerCount()==1,"Test 3 Proxy Appearance Update deletes axe before torch creation");
        f.slot("held_item",null);check(f.markers.events.getLast().equals("delete:torch") && f.appearance.markerCount()==0 && f.view().get("held_marker").isJsonNull(),"Test 4 Proxy Appearance Remove torch to null");
        f.slot("head",HELMET);check(f.appearance.markerCount()==1 && f.markers.events.getLast().equals("create:helmet") && f.view().get("head_marker").getAsString().equals("helmet"),"Test 5 helmet null to helmet");
        var end=f.entity.deepCopy();end.addProperty("sequence",2);end.getAsJsonObject("lifecycle").addProperty("event","despawn");end.remove("components");end.add("position",JsonNull.INSTANCE);end.add("rotation",JsonNull.INSTANCE);f.lifecycle(end);f.tick();
        check(f.appearance.ownerCount()==0 && f.appearance.markerCount()==0 && f.markers.active.isEmpty() && f.inspector.latest()==null,"Test 6 Entity Cleanup removes all Appearance");
        check(result.equals(adapter.generate(visual)) && result.hashCode()==adapter.generate(visual).hashCode() && result.toJson().equals(adapter.generate(visual).toJson()),"Test 7 repeated generation consistency");
        check(adapter.generate(generator.generate(eq("7dtd:unknown_item",null))).equals(ProxyAppearance.EMPTY),"Test 8 Unknown Item safely degrades");
        f=new Fixture();var before=f.inspector.latest().getAsJsonObject("components").deepCopy();long revision=f.health.state(f.initial).revision();f.slot("held_item",AXE);var after=f.inspector.latest().getAsJsonObject("components");
        boolean unchanged=true;for(String name:List.of("health","identity","presentation","authority"))unchanged &= before.get(name).equals(after.get(name));
        check(unchanged && f.health.state(f.initial).revision()==revision+1 && f.view().keySet().equals(Set.of("head_marker","held_marker")),"Test 9 Regression Health Identity Presentation Authority unchanged");
        int count=f.markers.events.size();f.tick();f.tick();check(f.markers.events.size()==count && f.appearance.markerCount()==1,"duplicate visual ticks do not recreate markers");
        count=f.markers.events.size();f.slot("held_item","7dtd:meleeToolAxeT2SteelAxe");check(f.markers.events.size()==count+2 && f.markers.events.get(count).equals("delete:axe") && f.markers.events.get(count+1).equals("create:axe"),"different items with same marker still delete and recreate");
        f.patch(component("equipment",JsonNull.INSTANCE));check(f.appearance.markerCount()==0 && f.view().get("held_marker").isJsonNull(),"whole Equipment remove clears Appearance");
        f.patch(component("equipment",eq(AXE,HELMET)));check(f.appearance.markerCount()==2,"complete Equipment readd restores both supported slots");
        f.slot("head",null);check(f.appearance.markerCount()==1 && f.view().get("held_marker").getAsString().equals("axe") && f.view().get("head_marker").isJsonNull(),"head remove deletes helmet and preserves held marker");
        f.inspector.reset();f.proxies.reset();f.tick();check(f.appearance.ownerCount()==0 && f.markers.active.isEmpty(),"Disconnect clears marker cache via existing proxy reset");
        f=new Fixture();f.slot("held_item",AXE);var retained=f.inspector.latest().getAsJsonObject("components").deepCopy();f.inspector.appearanceConnected(false);f.tick();
        check(f.appearance.ownerCount()==0 && f.markers.active.isEmpty() && f.view().get("held_marker").isJsonNull() && f.inspector.latest().getAsJsonObject("components").equals(retained),"peer unavailable clears Appearance without changing fact components");
        f=new Fixture();f.slot("held_item",AXE);f.slot("head",HELMET);f.world=null;f.tick();check(f.appearance.ownerCount()==0 && f.markers.active.isEmpty(),"world exit clears marker handles");f.world=new Object();f.tick();check(f.appearance.markerCount()==2,"new target world recreates from accepted VisualState");
        var update=f.entity.deepCopy();update.remove("components");update.addProperty("sequence",2);update.getAsJsonObject("lifecycle").addProperty("event","update");update.getAsJsonObject("position").addProperty("x",99);update.getAsJsonObject("rotation").addProperty("yaw",180);f.entity=update;f.lifecycle(update);count=f.markers.events.size();f.tick();
        check(f.markers.events.size()==count && f.markers.last.x()==99 && f.markers.last.yaw()==180,"marker movement follows proxy without recreation");
        f=new Fixture();f.slot("body",HELMET);f.slot("hands",AXE);f.slot("feet",TORCH);check(f.appearance.markerCount()==0 && f.view().get("head_marker").isJsonNull(),"body hands feet do not create appearance");
        check(adapter.generate(generator.generate(eq(HELMET,AXE))).equals(ProxyAppearance.EMPTY),"wrong marker kinds in head held slots safely ignored");
        var disabled=new ProxyAppearanceAdapter(new ProxyAppearanceConfig(false,config.mapping()));check(disabled.generate(visual).equals(ProxyAppearance.EMPTY),"enabled false disables appearance independently of visual state");
        check(invalidConfig(),"invalid duplicate oversized config disables safely and remains bounded");
        f=new Fixture();f.markers.failCreate=true;f.slot("held_item",AXE);count=f.markers.events.size();f.tick();check(f.appearance.markerCount()==0 && f.markers.events.size()==count && f.proxies.activeCount()==1,"marker create failure isolated from base proxy without frame retry storm");f.slot("head",HELMET);check(f.appearance.markerCount()==1,"failed held marker does not prevent helmet");
        f=new Fixture();f.slot("held_item",AXE);f.markers.failUpdate=true;f.tick();check(f.appearance.markerCount()==0 && f.markers.active.isEmpty() && f.proxies.activeCount()==1,"marker update failure removes object while base proxy survives");
        f=new Fixture();f.slot("held_item",AXE);f.slot("head",HELMET);f.appearance.reset();check(f.appearance.ownerCount()==0 && f.markers.active.isEmpty(),"explicit Appearance reset clears all objects");
        check(adapter.generate(null).equals(ProxyAppearance.EMPTY) && adapter.generate(generator.generate(null)).equals(ProxyAppearance.EMPTY),"missing unknown VisualState clears Appearance");
        f=new Fixture();f.slot("held_item",AXE);var snapshot=new NativeProxyController.Snapshot(f.initial.get("entity_id").getAsString(),UUID.randomUUID().toString(),"7dtd:player",f.initial.get("world_id").getAsString(),"7dtd:main",1,"spawn",0,0,0,0,0,0);check(f.inspector.visualState(snapshot)==null,"exact stream lookup does not borrow stale VisualState");
        long rev=f.health.state(f.initial).revision();for(int i=0;i<5;i++){f.inspector.latest();f.tick();}check(f.health.state(f.initial).revision()==rev,"appearance queries ticks never advance source revision");
        var mutated=f.view();mutated.addProperty("held_marker","torch");check(f.view().get("held_marker").getAsString().equals("axe"),"Inspector Appearance deep copy and derived intent isolation");
        Files.writeString(root.resolve("work/phase3822-test/inspector-example.json"),new GsonBuilder().serializeNulls().setPrettyPrinting().create().toJson(f.inspector.latest()));
        System.out.println("Proxy Appearance Create PASS\nProxy Appearance Update PASS\nProxy Appearance Remove PASS\nEntity Cleanup PASS\nUnknown Item PASS\nRegression PASS");System.out.println("TOTAL "+checks+" passed");
    }
    static boolean invalidConfig()throws Exception {
        for(String raw:List.of("{}","{\"enabled\":\"true\",\"mapping\":{}}","{\"enabled\":true,\"enabled\":false,\"mapping\":{}}","{\"enabled\":true,\"mapping\":{\"7dtd:torch\":\"torch\",\"7dtd:torch\":\"axe\"}}","{\"enabled\":true,\"mapping\":{\"7dtd:torch\":\"unknown\"}}","{\"enabled\":true,\"mapping\":{}} {}"," ".repeat(65537))) {
            try{ProxyAppearanceConfig.parse(raw);return false;}catch(RuntimeException expected){}
        }
        var path=root.resolve("work/phase3822-test/bad-config.json");Files.writeString(path,"bad");var warnings=new ArrayList<String>();var bad=ProxyAppearanceConfig.load(path,warnings::add);
        var large=new JsonObject();large.addProperty("enabled",true);var map=new JsonObject();for(int i=0;i<257;i++)map.addProperty("7dtd:item"+i,"axe");large.add("mapping",map);
        try{ProxyAppearanceConfig.parse(large.toString());return false;}catch(RuntimeException expected){}
        return !bad.enabled() && warnings.size()==1 && !ProxyAppearanceConfig.load(root.resolve("work/phase3822-test/missing.json"),warnings::add).enabled();
    }
}
