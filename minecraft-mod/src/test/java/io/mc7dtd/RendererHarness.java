package io.mc7dtd;

import com.google.gson.*;
import java.nio.file.*;
import java.util.*;

/** Real reducers and proxy lifecycle with instrumented physical adapters; no claim of rendered pixels. */
public final class RendererHarness {
    static Path root;static int passed;static RendererMapping mapping;static ProxyAppearanceConfig appearanceConfig;
    static void check(boolean ok,String label){if(!ok)throw new AssertionError(label);passed++;System.out.println(label+" PASS");}
    static final class Physical implements RendererAdapter {
        final Set<Object> live=Collections.newSetFromMap(new IdentityHashMap<>());final List<String> events;
        final boolean item;boolean failCreate,failUpdate;int creates;Context last;
        Physical(boolean item,List<String> events){this.item=item;this.events=events;}
        public Object create(Context c,Request r){creates++;events.add("create:"+(item ? r.mapping().item() : r.marker()));if(failCreate || (item && r.mapping().item().equals("minecraft:no_such_item")))throw new IllegalArgumentException("item_not_found");var h=new Object();live.add(h);last=c;return h;}
        public void update(Object h,Context c){if(failUpdate)throw new IllegalStateException("update_failed");last=c;}
        public void remove(Object h){events.add("remove:"+(item ? "item" : "marker"));live.remove(h);}
    }
    static final class Fixture {
        final List<String> events=new ArrayList<>(),logs=new ArrayList<>();final Physical items=new Physical(true,events),markers=new Physical(false,events);
        final FallbackRendererAdapter backend=new FallbackRendererAdapter(items,markers,logs::add);
        final HealthReceiver health=new HealthReceiver(logs::add);final EquipmentReceiver equipment=new EquipmentReceiver(health,logs::add);final PresentationReceiver presentation=new PresentationReceiver(logs::add);
        final EntityInspector inspector=new EntityInspector();final ProxyAppearanceAdapter adapter=new ProxyAppearanceAdapter(appearanceConfig);
        final ProxyAppearanceController appearance;final NativeProxyController proxies;final JsonObject initial,entity;Object world=new Object();Object latest;
        record Rendered(Object owner,Object handle,NativeProxyController.Snapshot initial) { }
        Fixture(RendererMapping config)throws Exception {
            inspector.appearanceAdapter(adapter);inspector.rendererRuntime();inspector.visualWorld(world);
            appearance=new ProxyAppearanceController(new ProxyAppearanceController.Scene() {
                public Object create(Object owner,String slot,String marker,NativeProxyController.Snapshot state){return new Object();}
                public Object create(Object owner,String slot,String marker,String itemId,NativeProxyController.Snapshot state){if(!slot.equals("held_item"))return new Object();latest=backend.create(new RendererAdapter.Context(owner,state),new RendererAdapter.Request(itemId,marker,config.get(itemId)));inspector.rendererState(state,FallbackRendererAdapter.state(latest));return new Rendered(owner,latest,state);}
                public void update(Object value,NativeProxyController.Snapshot state){if(value instanceof Rendered r){backend.update(r.handle,new RendererAdapter.Context(r.owner,state));inspector.rendererState(state,FallbackRendererAdapter.state(r.handle));}}
                public void delete(Object value){if(value instanceof Rendered r){backend.remove(r.handle);inspector.rendererState(r.initial,null);}}
            },inspector::visualState,adapter,logs::add,true);
            proxies=new NativeProxyController(new NativeProxyController.Scene(){public Object create(NativeProxyController.Snapshot s){var h=new Object();appearance.attach(h,s);return h;}public void update(Object h,NativeProxyController.Snapshot s){appearance.update(h,s);}public void delete(Object h){appearance.detach(h);}},ProxyTypeCatalog.load(root.resolve("config/entity_types.json")),logs::add);
            initial=JsonParser.parseString(Files.readString(root.resolve("docs/examples/equipment_components/initial.json"))).getAsJsonObject();initial.getAsJsonObject("components").add("equipment",ProxyAppearanceHarness.eq(null,null));
            entity=JsonParser.parseString(Files.readString(root.resolve("docs/examples/presentation/spawn.json"))).getAsJsonObject();for(String key:List.of("entity_id","stream_id","world_id","dimension","origin","entity_type"))entity.add(key,initial.get(key).deepCopy());entity.getAsJsonObject("position").addProperty("space","minecraft");
            lifecycle();equipment.receive(initial);inspector.components(initial,health.state(initial),equipment.state(initial));tick();
        }
        void lifecycle(){health.lifecycle(entity);equipment.lifecycle(entity);presentation.lifecycle(entity);inspector.lifecycle(entity,health.state(entity),presentation.get(entity));inspector.components(entity,health.state(entity),equipment.state(entity));proxies.enqueue(entity);}
        void held(String id){slot("held_item",id);}
        void slot(String name,String id){var slots=new JsonObject();slots.add(name,id==null ? JsonNull.INSTANCE : ProxyAppearanceHarness.item(id));var eq=new JsonObject();eq.add("slots",slots);patch(eq);}
        void patch(JsonElement eq){var patch=initial.deepCopy();long rev=health.state(initial).revision();patch.addProperty("mode","patch");patch.addProperty("base_revision",rev);patch.addProperty("revision",rev+1);patch.add("components",ProxyAppearanceHarness.component("equipment",eq));equipment.receive(patch);inspector.components(patch,health.state(patch),equipment.state(patch));tick();}
        void tick(){inspector.visualWorld(world);proxies.tick(world);if(inspector.appearanceConnected())appearance.tick();else appearance.reset();}
        JsonObject state(){return inspector.latest().getAsJsonObject("proxy_appearance").getAsJsonObject("renderer");}
    }
    static void rejects(String json,String label){try{RendererMapping.parse(json);throw new AssertionError(label);}catch(RuntimeException expected){check(true,label);}}
    public static void main(String[] args)throws Exception {
        root=Path.of(args[0]);mapping=RendererMapping.load(root.resolve("config/renderer_mapping.json"),System.out::println);appearanceConfig=ProxyAppearanceConfig.load(root.resolve("config/proxy_appearance.json"),System.out::println);
        for(String[] expected:new String[][]{{"7dtd:woodenClub","minecraft:wooden_sword"},{"7dtd:torch","minecraft:torch"},{"7dtd:ironAxe","minecraft:iron_axe"},{ProxyAppearanceHarness.CLUB,"minecraft:wooden_sword"},{ProxyAppearanceHarness.TORCH,"minecraft:torch"},{ProxyAppearanceHarness.AXE,"minecraft:iron_axe"}})check(mapping.get(expected[0]).item().equals(expected[1]),"Mapping "+expected[0]);
        check(mapping.get(ProxyAppearanceHarness.CLUB).offset().equals(RendererMapping.DEFAULT_OFFSET) && mapping.get(ProxyAppearanceHarness.CLUB).scale()==.8,"Anchor defaults");
        var f=new Fixture(mapping);check(f.items.live.isEmpty() && f.markers.live.isEmpty(),"Initial empty no renderer");f.held(ProxyAppearanceHarness.CLUB);
        check(f.items.live.size()==1 && f.markers.live.isEmpty() && f.state().get("status").getAsString().equals("active") && f.state().get("item").getAsString().equals("minecraft:wooden_sword"),"Renderer Create");
        int events=f.events.size();f.held(ProxyAppearanceHarness.TORCH);check(f.items.live.size()==1 && f.events.get(events).equals("remove:item") && f.events.get(events+1).equals("create:minecraft:torch"),"Renderer Update replace order");
        f.held(null);check(f.items.live.isEmpty() && f.inspector.latest().getAsJsonObject("proxy_appearance").get("renderer").isJsonNull(),"Renderer Remove");
        f.held(ProxyAppearanceHarness.AXE);int creates=f.items.creates;var components=f.inspector.latest().get("components").deepCopy();long revision=f.health.state(f.initial).revision();f.tick();f.tick();
        check(f.items.creates==creates,"Deterministic no duplicate creation");
        check(f.inspector.latest().get("components").equals(components) && f.health.state(f.initial).revision()==revision,"Regression Health Identity Equipment Presentation Authority revision");
        var original=f.state();original.addProperty("status","tampered");check(f.state().get("status").getAsString().equals("active"),"Inspector renderer deep copy");
        f.entity.addProperty("sequence",2);f.entity.getAsJsonObject("lifecycle").addProperty("event","update");f.entity.getAsJsonObject("position").addProperty("x",42);f.entity.getAsJsonObject("rotation").addProperty("yaw",90);f.lifecycle();f.tick();check(f.items.last.state().x()==42 && f.items.last.state().yaw()==90 && f.items.creates==creates,"Position rotation update");
        f.world=new Object();f.tick();check(f.items.live.size()==1 && f.state().get("status").getAsString().equals("active"),"World cleanup and recreation");
        f.entity.addProperty("sequence",3);f.entity.getAsJsonObject("lifecycle").addProperty("event","despawn");f.entity.add("position",JsonNull.INSTANCE);f.entity.add("rotation",JsonNull.INSTANCE);f.entity.add("metadata",new JsonObject());f.entity.remove("components");f.lifecycle();f.tick();check(f.items.live.isEmpty() && f.markers.live.isEmpty() && f.appearance.ownerCount()==0 && f.inspector.latest()==null,"Renderer Cleanup despawn");
        f=new Fixture(mapping);f.held("7dtd:unknown_item");check(f.markers.live.size()==1 && f.items.live.isEmpty() && f.state().get("renderer_status").getAsString().equals("fallback_marker") && f.state().get("reason").getAsString().equals("mapping_missing"),"Unknown Item fallback_marker");
        f.held(null);check(f.markers.live.isEmpty(),"Fallback removal");
        f=new Fixture(new RendererMapping(Map.of()));f.held(ProxyAppearanceHarness.CLUB);check(f.markers.live.size()==1 && f.state().get("type").getAsString().equals("block_display"),"Missing mapping fallback");
        f=new Fixture(mapping);f.items.failCreate=true;f.held(ProxyAppearanceHarness.CLUB);creates=f.items.creates;f.tick();f.tick();check(f.items.live.isEmpty() && f.markers.live.size()==1 && f.items.creates==creates && f.proxies.activeCount()==1,"Item creation failure isolated fallback no retry storm");
        f=new Fixture(new RendererMapping(Map.of(ProxyAppearanceHarness.CLUB,new RendererMapping.Entry("minecraft:no_such_item",RendererMapping.DEFAULT_OFFSET,new RendererMapping.Vector(0,0,0),.8))));f.held(ProxyAppearanceHarness.CLUB);check(f.state().get("renderer_status").getAsString().equals("fallback_marker"),"Invalid registered item fallback");
        f=new Fixture(mapping);f.held(ProxyAppearanceHarness.CLUB);f.items.failUpdate=true;f.tick();check(f.items.live.isEmpty() && f.markers.live.size()==1 && f.state().get("status").getAsString().equals("fallback"),"Update failure switches to fallback");
        f.inspector.appearanceConnected(false);f.tick();check(f.items.live.isEmpty() && f.markers.live.isEmpty() && f.appearance.ownerCount()==0,"Peer unavailable renderer cleanup");
        f=new Fixture(mapping);f.held(ProxyAppearanceHarness.CLUB);f.inspector.reset();f.proxies.reset();f.tick();check(f.items.live.isEmpty() && f.appearance.ownerCount()==0,"Disconnect cache cleanup");
        f=new Fixture(mapping);f.items.failCreate=true;f.markers.failCreate=true;f.held(ProxyAppearanceHarness.CLUB);check(f.state().get("status").getAsString().equals("unavailable") && f.proxies.activeCount()==1,"Both backends failed isolated unavailable");
        f=new Fixture(mapping);f.slot("body",ProxyAppearanceHarness.CLUB);f.slot("hands",ProxyAppearanceHarness.TORCH);f.slot("feet",ProxyAppearanceHarness.AXE);check(f.items.live.isEmpty() && f.markers.live.isEmpty(),"Unsupported slots do not create renderer");
        f.held(ProxyAppearanceHarness.CLUB);f.patch(JsonNull.INSTANCE);check(f.items.live.isEmpty() && f.markers.live.isEmpty() && f.proxies.activeCount()==1,"Equipment remove clears renderer only");
        f=new Fixture(mapping);f.held(ProxyAppearanceHarness.CLUB);events=f.events.size();f.held("7dtd:woodenClub");check(f.events.size()==events+2 && f.items.live.size()==1,"Same Minecraft item different source replaces object");
        var enabledAppearance=appearanceConfig;appearanceConfig=ProxyAppearanceConfig.disabled();f=new Fixture(mapping);f.held(ProxyAppearanceHarness.CLUB);check(f.items.live.isEmpty() && f.markers.live.isEmpty() && f.proxies.activeCount()==1,"Appearance disable preserves base entity");appearanceConfig=enabledAppearance;
        String valid="{\"7dtd:x\":{\"renderer\":\"item_display\",\"item\":\"minecraft:torch\",\"anchor\":\"held_item\"}}";
        rejects(valid.replace("\"anchor\":\"held_item\"","\"anchor\":\"head\""),"Unsupported anchor rejected");rejects(valid.replace("\"item\":\"minecraft:torch\"","\"item\":\"minecraft:torch\",\"item\":\"minecraft:iron_axe\""),"Duplicate mapping field rejected");rejects(valid.replace("\"anchor\":\"held_item\"","\"anchor\":\"held_item\",\"scale\":3"),"Unsafe scale rejected");rejects(valid.replace("\"anchor\":\"held_item\"","\"anchor\":\"held_item\",\"offset\":{\"x\":9,\"y\":0,\"z\":0}"),"Unsafe anchor offset rejected");
        rejects(valid+"{}","Trailing JSON rejected");rejects(valid.replace("\"anchor\":\"held_item\"","\"anchor\":\"held_item\",\"scale\":\"0.8\""),"String scale rejected");rejects(" ".repeat(65537),"Oversized config rejected");
        var invalidConfig=root.resolve("work/phase3831-test/invalid-mapping.json");Files.writeString(invalidConfig,"{}");f=new Fixture(RendererMapping.load(invalidConfig,System.out::println));f.held(ProxyAppearanceHarness.CLUB);check(f.state().get("renderer_status").getAsString().equals("fallback_marker"),"Empty config automatic fallback");
        f=new Fixture(mapping);f.held(ProxyAppearanceHarness.CLUB);Files.writeString(root.resolve("work/phase3831-test/inspector-example.json"),new GsonBuilder().serializeNulls().setPrettyPrinting().create().toJson(f.inspector.latest()));
        System.out.println("TOTAL "+passed+" passed");
    }
}
