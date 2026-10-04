package io.mc7dtd;

import com.google.gson.*;
import java.nio.file.*;
import java.util.*;
import java.util.concurrent.atomic.AtomicReference;

/** Design M01-M20 and extra requirements through real receivers and read-only Inspector. */
public final class EquipmentVisualHarness {
    private static Path root;
    private static int checks;
    private static final String BODY="7dtd:armorPrimitiveOutfit",CLUB="7dtd:meleeWpnClubT0WoodenClub",TORCH="7dtd:meleeToolTorch";
    private static void check(boolean ok,String label){if(!ok)throw new AssertionError(label);checks++;System.out.println("PASS "+label);}
    private static JsonObject item(String id){var value=new JsonObject();value.addProperty("item_id",id);return value;}
    private static JsonObject equipment(String body,String held){
        var slots=new JsonObject();for(String name:EquipmentVisualState.SLOTS)slots.add(name,JsonNull.INSTANCE);
        if(body!=null)slots.add("body",item(body));if(held!=null)slots.add("held_item",item(held));
        var result=new JsonObject();result.add("slots",slots);return result;
    }
    private static JsonObject component(String name,JsonElement value){var result=new JsonObject();result.add(name,value);return result;}
    private static JsonObject slotPatch(String name,JsonElement value){var slots=new JsonObject();slots.add(name,value);var eq=new JsonObject();eq.add("slots",slots);return component("equipment",eq);}
    private static JsonObject base(){return JsonParser.parseString("{\"renderer\":\"humanoid\",\"model\":\"survivor\",\"variant\":\"default\",\"scale\":1}").getAsJsonObject();}
    private static JsonObject rule(String slot,String id,String model){
        var rule=new JsonObject();rule.addProperty("slot",slot);rule.addProperty("item_id",id);
        var profile=new JsonObject();profile.addProperty("renderer","humanoid");profile.addProperty("model",model);rule.add("compatible_base",profile);
        var modifier=new JsonObject();modifier.addProperty("kind",slot.equals("held_item") ? "held_attachment" : "armor_overlay");
        modifier.addProperty("binding_id","test:"+slot);modifier.addProperty("anchor",slot);rule.add("modifier",modifier);return rule;
    }
    private static JsonObject catalog(){
        var result=new JsonObject();result.addProperty("format_version",1);result.addProperty("source_type","7dtd:player");result.addProperty("target_game","minecraft");
        var rules=new JsonArray();rules.add(rule("body",BODY,"survivor"));rules.add(rule("held_item",CLUB,"survivor"));rules.add(rule("held_item",TORCH,"survivor"));result.add("rules",rules);return result;
    }
    private static JsonObject slots(JsonObject view){return view.getAsJsonObject("slots");}
    private static String resolution(JsonObject view,String slot){return slots(view).getAsJsonObject(slot).get("resolution").getAsString();}
    private static boolean emptySlots(JsonObject view){return slots(view).keySet().equals(new HashSet<>(EquipmentVisualState.SLOTS)) && slots(view).asMap().values().stream().allMatch(JsonElement::isJsonNull);}
    private static final class Fixture {
        final List<String> logs=new ArrayList<>();
        final HealthReceiver health=new HealthReceiver(logs::add);
        final EquipmentReceiver eq=new EquipmentReceiver(health,logs::add);
        final PresentationReceiver presentation=new PresentationReceiver(logs::add);
        final EntityInspector inspector;
        final JsonObject packet;
        JsonObject spawn;
        Fixture()throws Exception{this(EquipmentModifierGenerator.bundled());}
        Fixture(EquipmentModifierGenerator generator)throws Exception {
            inspector=new EntityInspector(generator);
            packet=JsonParser.parseString(Files.readString(root.resolve("docs/examples/equipment_components/initial.json"))).getAsJsonObject();
            packet.getAsJsonObject("components").add("equipment",equipment(BODY,CLUB));
            spawn=packet.deepCopy();spawn.addProperty("type","entity_state");spawn.addProperty("version",2);spawn.addProperty("sequence",1);
            spawn.add("lifecycle",JsonParser.parseString("{\"event\":\"spawn\"}"));spawn.add("components",component("presentation",base()));
            spawn.add("position",JsonParser.parseString("{\"x\":0,\"y\":0,\"z\":0,\"space\":\"minecraft\"}"));spawn.add("rotation",JsonParser.parseString("{\"yaw\":0,\"pitch\":0,\"roll\":0}"));lifecycle(spawn);
        }
        void lifecycle(JsonObject message){health.lifecycle(message);eq.lifecycle(message);presentation.lifecycle(message);inspector.lifecycle(message,health.state(message),presentation.get(message));inspector.components(message,health.state(message),eq.state(message));}
        void receive(JsonObject message){eq.receive(message);inspector.components(message,health.state(message),eq.state(message));}
        void initial(){receive(packet);}
        JsonObject visual(){return inspector.latest().getAsJsonObject("equipment_visual_state");}
        long revision(){return health.state(packet)==null ? 0 : health.state(packet).revision();}
        JsonObject next(JsonObject components,boolean snapshot){
            var message=packet.deepCopy();message.addProperty("mode",snapshot ? "snapshot" : "patch");message.addProperty("base_revision",snapshot ? 0 : revision());
            message.addProperty("revision",revision()+1);message.addProperty("entity_sequence",spawn.get("sequence").getAsLong());message.add("components",components);return message;
        }
        void patch(JsonObject components){receive(next(components,false));}
        void updatePresentation(JsonElement value){var message=spawn.deepCopy();message.addProperty("sequence",spawn.get("sequence").getAsLong()+1);message.getAsJsonObject("lifecycle").addProperty("event","update");message.add("components",component("presentation",value));spawn=message;lifecycle(message);}
    }
    public static void main(String[] args)throws Exception {
        root=Path.of(args[0]);var generator=EquipmentModifierGenerator.bundled();var profile=base();
        check(generator.generate(equipment(null,null),profile).known() && emptySlots(generator.generate(equipment(null,null),profile).toJson()),"M01 all five empty slots are known null");
        var f=new Fixture();f.initial();f.patch(component("equipment",JsonNull.INSTANCE));
        check(!generator.generate(null).known() && f.visual().get("equipment_state").getAsString().equals("unknown") && emptySlots(f.visual()),"M02 missing and removed Equipment are unknown");
        var armor=generator.generate(equipment(BODY,null),profile).toJson();
        check(resolution(armor,"body").equals("resolved") && slots(armor).getAsJsonObject("body").getAsJsonObject("modifier").get("anchor").getAsString().equals("body")
            && slots(armor).getAsJsonObject("body").getAsJsonObject("modifier").get("kind").getAsString().equals("armor_overlay"),"M03 mapped armor resolves a symbolic descriptor");
        f=new Fixture();f.initial();var original=f.visual();var beforeReplacement=original;f.patch(slotPatch("held_item",item(TORCH)));var afterReplacement=f.visual();
        check(slots(afterReplacement).getAsJsonObject("held_item").get("item_id").getAsString().equals(TORCH) && EquipmentVisualState.SLOTS.stream().filter(n->!n.equals("held_item")).allMatch(n->slots(beforeReplacement).get(n).equals(slots(afterReplacement).get(n))),"M04 held replacement preserves armor");
        f.patch(slotPatch("held_item",JsonNull.INSTANCE));check(slots(f.visual()).get("held_item").isJsonNull() && !f.visual().toString().contains("meleeHand"),"M05 empty hand clears modifier without fist identity");
        original=f.visual();f.patch(slotPatch("body",JsonNull.INSTANCE));var beforeUnequip=original;var afterUnequip=f.visual();
        check(slots(afterUnequip).get("body").isJsonNull() && EquipmentVisualState.SLOTS.stream().filter(n->!n.equals("body")).allMatch(n->slots(beforeUnequip).get(n).equals(slots(afterUnequip).get(n))),"M06 unequip clears only corresponding slot");
        var cases=equipment(BODY,"7dtd:unknown_tool");cases.getAsJsonObject("slots").add("head",item(BODY));
        check(resolution(generator.generate(cases,profile).toJson(),"head").equals("unmapped") && resolution(generator.generate(cases,profile).toJson(),"held_item").equals("unmapped")
            && resolution(generator.generate(equipment(BODY.toLowerCase(Locale.ROOT),null),profile).toJson(),"body").equals("unmapped"),"M07 exact case and slot matching with unknown degradation");
        f=new Fixture();f.initial();var incompatible=component("model",new JsonPrimitive("other"));f.updatePresentation(incompatible);
        boolean incompatibleModel=resolution(f.visual(),"body").equals("incompatible") && slots(f.visual()).getAsJsonObject("body").get("modifier").isJsonNull();
        f.updatePresentation(component("model",new JsonPrimitive("survivor")));f.updatePresentation(component("renderer",new JsonPrimitive("unknown")));
        boolean incompatibleRenderer=resolution(f.visual(),"body").equals("incompatible");f.updatePresentation(component("renderer",new JsonPrimitive("humanoid")));
        check(incompatibleModel && incompatibleRenderer && resolution(f.visual(),"body").equals("resolved") && eqBody(f).equals(BODY),"M08 base incompatibility and recovery retain source equipment");
        original=f.visual();var p=JsonParser.parseString("{\"scale\":1.5,\"variant\":\"alternate\"}").getAsJsonObject();f.updatePresentation(p);
        check(original.equals(f.visual()) && f.inspector.latest().getAsJsonObject("components").getAsJsonObject("presentation").get("scale").getAsDouble()==1.5,"M09 scale variant update leaves modifiers unchanged");
        f.updatePresentation(JsonNull.INSTANCE);boolean removed=resolution(f.visual(),"body").equals("incompatible");f.updatePresentation(base());
        check(removed && resolution(f.visual(),"body").equals("resolved") && f.visual().get("equipment_state").getAsString().equals("known"),"M10 Presentation remove fallback and add");
        var without=f.health.state(f.packet).components().deepCopy();f.receive(f.next(without,true));boolean unknown=f.visual().get("equipment_state").getAsString().equals("unknown");
        f.patch(component("equipment",equipment(BODY,TORCH)));check(unknown && resolution(f.visual(),"held_item").equals("resolved"),"M11 snapshot omission and complete readd");
        check(rejections(),"M12 rejected invalid old baseline and stream packets preserve visual state revision");
        check(ordering(),"M13 early sidecar rejected and legal input orders converge");
        f=new Fixture();f.initial();original=f.visual();f.patch(component("health",JsonParser.parseString("{\"current\":95,\"max\":100}")));
        f.patch(component("name",JsonParser.parseString("{\"text\":\"renamed\",\"display_name\":\"renamed\"}")));
        for(int i=0;i<10;i++){var update=f.spawn.deepCopy();update.remove("components");update.addProperty("sequence",f.spawn.get("sequence").getAsLong()+1);update.getAsJsonObject("position").addProperty("x",i);update.getAsJsonObject("rotation").addProperty("yaw",i*10);update.getAsJsonObject("lifecycle").addProperty("event","update");f.spawn=update;f.lifecycle(update);}
        check(original.equals(f.visual()) && f.health.state(f.packet).current()==95 && f.inspector.latest().getAsJsonObject("position").get("x").getAsInt()==9
            && f.inspector.latest().getAsJsonObject("components").getAsJsonObject("identity").get("name").getAsString().equals("renamed"),"M14 Health Identity position updates preserve modifiers");
        check(cleanup(),"M15 despawn session reset and same connection new entity cleanup");
        check(isolation(),"M16 world dimension stream and source isolation");
        f=new Fixture();f.initial();var saved=f.inspector.latest().getAsJsonObject("components").deepCopy();f.inspector.visualWorld(null);
        boolean absent=f.inspector.latest().get("equipment_visual_state").isJsonNull();f.inspector.visualWorld(new Object());
        check(absent && f.inspector.latest().getAsJsonObject("components").equals(saved) && resolution(f.visual(),"body").equals("resolved"),"M17 no target world suppresses derived view and reentry rebuilds without scene");
        check(isolationAndAtomicity(),"M18 immutable output deep copy and atomic whole-view reads");
        check(badCatalogs(),"M19 invalid duplicate oversized catalogs fail closed as a whole");
        check(debugIndependent(),"M20 debug flags leave derived state and cleanup identical");
        var eq=equipment(BODY,TORCH);var copy=eq.deepCopy();var baseCopy=profile.deepCopy();var first=generator.generate(eq,profile);
        check(eq.equals(copy) && profile.equals(baseCopy) && first.toJson().keySet().equals(Set.of("equipment_state","slots")),"Extra Presentation input unchanged and no network revision");
        check(first.equals(generator.generate(eq,profile)) && first.hashCode()==generator.generate(eq,profile).hashCode() && first.toJson().toString().equals(generator.generate(eq,profile).toJson().toString()),"Extra repeated generation is deterministic");
        var unknownState=generator.generate(equipment(null,"7dtd:new_unknown_item"),profile).toJson();
        check(resolution(unknownState,"held_item").equals("unmapped") && slots(unknownState).getAsJsonObject("held_item").get("modifier").isJsonNull()
            && slots(unknownState).getAsJsonObject("held_item").get("item_id").getAsString().equals("7dtd:new_unknown_item"),"Extra unknown item preserves ID without fabricated binding");
        f=new Fixture();f.initial();check(EntityInspector.format(f.inspector.latest()).contains("equipment_visual_state") && f.inspector.list().getFirst().get("equipment_visual_state").equals(f.visual())
            && f.inspector.find(f.packet.get("entity_id").getAsString(),f.packet.get("world_id").getAsString(),f.packet.get("dimension").getAsString()).get("equipment_visual_state").equals(f.visual()),"Inspector latest list find expose local visual state");
        Files.writeString(root.resolve("work/phase3821-test/inspector-example.json"),new GsonBuilder().serializeNulls().setPrettyPrinting().create().toJson(f.inspector.latest()));
        System.out.println("TOTAL "+checks+" passed");
    }
    private static String eqBody(Fixture f){return f.eq.state(f.packet).getAsJsonObject("slots").getAsJsonObject("body").get("item_id").getAsString();}
    private static boolean rejections()throws Exception {
        var f=new Fixture();f.initial();var old=f.visual();long rev=f.revision();
        for(String mode:List.of("invalid","old","baseline","stream")) {
            var packet=f.next(slotPatch("held_item",item(TORCH)),false);
            switch(mode) {
                case "invalid" -> packet.getAsJsonObject("components").getAsJsonObject("equipment").getAsJsonObject("slots").add("legs",JsonNull.INSTANCE);
                case "old" -> packet.addProperty("revision",rev);
                case "baseline" -> packet.addProperty("base_revision",99);
                case "stream" -> packet.addProperty("stream_id",UUID.randomUUID().toString());
            }
            f.receive(packet);if(!f.visual().equals(old) || f.revision()!=rev)return false;
        }return true;
    }
    private static boolean ordering()throws Exception {
        var early=new Fixture();early.health.reset();early.eq.reset();early.presentation.reset();early.inspector.reset();early.receive(early.packet);
        if(early.inspector.latest()!=null || early.revision()!=0)return false;
        early.lifecycle(early.spawn);if(!early.visual().get("equipment_state").getAsString().equals("unknown"))return false;early.initial();
        var first=new Fixture();var second=new Fixture();first.initial();first.updatePresentation(component("model",new JsonPrimitive("other")));
        second.updatePresentation(component("model",new JsonPrimitive("other")));var current=second.packet.deepCopy();current.addProperty("entity_sequence",second.spawn.get("sequence").getAsLong());second.receive(current);
        var repeat=first.visual();return early.revision()==1 && first.visual().equals(second.visual()) && first.visual().equals(repeat);
    }
    private static boolean cleanup()throws Exception {
        var f=new Fixture();f.initial();var end=f.spawn.deepCopy();end.addProperty("sequence",2);end.getAsJsonObject("lifecycle").addProperty("event","despawn");end.remove("components");f.lifecycle(end);
        if(f.inspector.latest()!=null || !f.inspector.list().isEmpty() || f.eq.state(f.packet)!=null)return false;
        String id=UUID.randomUUID().toString();f.packet.addProperty("entity_id",id);f.packet.getAsJsonObject("origin").addProperty("entity_id",id);
        f.spawn.addProperty("entity_id",id);f.spawn.getAsJsonObject("origin").addProperty("entity_id",id);f.spawn.addProperty("sequence",3);f.packet.addProperty("entity_sequence",3);f.lifecycle(f.spawn);f.initial();
        if(f.revision()!=1 || !resolution(f.visual(),"body").equals("resolved"))return false;
        f.health.reset();f.eq.reset();f.presentation.reset();f.inspector.reset();return f.inspector.latest()==null && f.eq.state(f.packet)==null;
    }
    private static boolean isolation()throws Exception {
        var f=new Fixture();f.initial();var expected=f.visual();
        for(String field:List.of("world_id","dimension","stream_id")) {
            var other=f.spawn.deepCopy();String value=field.equals("stream_id") ? UUID.randomUUID().toString() : "7dtd:other";
            other.addProperty(field,value);if(!field.equals("stream_id"))other.getAsJsonObject("origin").addProperty(field,value);
            other.remove("components");f.inspector.lifecycle(other,null,null);f.inspector.components(other,null,equipment(null,TORCH));
        }
        if(f.inspector.list().size()!=4 || !f.inspector.list().getFirst().get("equipment_visual_state").equals(expected))return false;
        var other=f.spawn.deepCopy();other.addProperty("source","minecraft");other.addProperty("entity_type","minecraft:player");f.inspector.lifecycle(other,null,null);
        return !f.inspector.latest().has("equipment_visual_state") && f.inspector.list().size()==5;
    }
    private static boolean isolationAndAtomicity()throws Exception {
        var f=new Fixture();f.initial();var input=f.eq.state(f.packet);var initial=f.inspector.latest();var state=EquipmentModifierGenerator.bundled().generate(input,base());var output=state.toJson();output.getAsJsonObject("slots").add("body",JsonNull.INSTANCE);
        if(state.slot("body")==null || !f.inspector.latest().equals(initial))return false;
        var copy=f.inspector.latest();copy.getAsJsonObject("equipment_visual_state").getAsJsonObject("slots").add("head",item(TORCH));if(!f.inspector.latest().equals(initial))return false;
        var a=equipment(BODY,CLUB);var b=equipment(null,TORCH);var failure=new AtomicReference<Throwable>();
        var observed=f.health.state(f.packet);
        var writer=new Thread(()->{try{for(int i=0;i<500;i++)f.inspector.components(f.spawn,observed,i%2==0 ? a : b);}catch(Throwable ex){failure.set(ex);}});
        var reader=new Thread(()->{try{for(int i=0;i<500;i++){var view=f.inspector.latest().getAsJsonObject("equipment_visual_state");var held=slots(view).getAsJsonObject("held_item").get("item_id").getAsString();if(slots(view).get("body").isJsonNull()!=held.equals(TORCH))throw new AssertionError("torn view");}}catch(Throwable ex){failure.set(ex);}});
        writer.start();reader.start();writer.join();reader.join();return failure.get()==null && input.getAsJsonObject("slots").getAsJsonObject("body").get("item_id").getAsString().equals(BODY);
    }
    private static boolean badCatalogs()throws Exception {
        var samples=new ArrayList<String>();samples.add("not json");samples.add(catalog().toString()+" {}");samples.add(catalog().toString().replace("\"format_version\":1","\"format_version\":1,\"format_version\":1"));
        var duplicate=catalog();duplicate.getAsJsonArray("rules").add(duplicate.getAsJsonArray("rules").get(0).deepCopy());samples.add(duplicate.toString());
        var badKind=catalog();badKind.getAsJsonArray("rules").get(0).getAsJsonObject().getAsJsonObject("modifier").addProperty("kind","held_attachment");samples.add(badKind.toString());
        var badAnchor=catalog();badAnchor.getAsJsonArray("rules").get(0).getAsJsonObject().getAsJsonObject("modifier").addProperty("anchor","head");samples.add(badAnchor.toString());
        var badField=catalog();badField.addProperty("unknown",true);samples.add(badField.toString());
        var badPath=catalog();badPath.getAsJsonArray("rules").get(0).getAsJsonObject().getAsJsonObject("modifier").addProperty("binding_id","../../model");samples.add(badPath.toString());
        var badScope=catalog();badScope.addProperty("target_game","7dtd");samples.add(badScope.toString());
        var badVersion=catalog();badVersion.addProperty("format_version","1");samples.add(badVersion.toString());
        var over=catalog();var rules=new JsonArray();for(int i=0;i<4097;i++)rules.add(rule("body",BODY,"survivor"));over.add("rules",rules);samples.add(over.toString());samples.add(" ".repeat(2*1024*1024+1));
        for(String sample:samples)if(!resolution(EquipmentModifierGenerator.fromJson(sample).generate(equipment(BODY,null),base()).toJson(),"body").equals("unmapped"))return false;
        var absent=EquipmentModifierGenerator.empty().generate(equipment(BODY,null),base()).toJson();
        var f=new Fixture(EquipmentModifierGenerator.fromJson(samples.getFirst()));f.initial();
        return resolution(absent,"body").equals("unmapped") && f.health.state(f.packet).current()==100 && eqBody(f).equals(BODY)
            && f.inspector.latest().getAsJsonObject("components").getAsJsonObject("presentation").equals(base())
            && f.inspector.latest().getAsJsonObject("components").getAsJsonObject("identity").get("name").getAsString().equals("native");
    }
    private static boolean debugIndependent()throws Exception {
        var enabled=new Fixture();var disabled=new Fixture();enabled.initial();disabled.initial();
        for(var config:List.of(new DebugConfig(false,false),new DebugConfig(true,true))) {
            var f=config.logging() ? enabled : disabled;var scene=new DebugNameTagLayer.Scene(){public Object create(NativeProxyController.Snapshot s,String t){return new Object();}public void update(Object h,NativeProxyController.Snapshot s,String t){}public void delete(Object h){}};
            var layer=new DebugNameTagLayer(f.inspector,config,scene,f.logs::add);
            var snapshot=new NativeProxyController.Snapshot(f.packet.get("entity_id").getAsString(),f.packet.get("stream_id").getAsString(),"7dtd:player",f.packet.get("world_id").getAsString(),"7dtd:main",1,"spawn",0,0,0,0,0,0);layer.attach(snapshot);layer.tick();layer.detach(snapshot);
        }
        boolean same=enabled.visual().equals(disabled.visual());enabled.inspector.reset();disabled.inspector.reset();return same && enabled.inspector.latest()==null && disabled.inspector.latest()==null;
    }
}
