package io.mc7dtd;
import com.google.gson.*;
import java.nio.file.*;
import java.util.*;
public final class EquipmentHarness {
    static int checks;
    static void check(boolean value,String label){if(!value)throw new AssertionError(label);checks++;System.out.println("PASS "+label);}
    public static void main(String[] args)throws Exception {
        if(args.length>1 && args[1].equals("live")){
            try(var client=new BridgeClient(Path.of(args[0]),System.out::println)){
                client.start();String last="";boolean seen=false;
                for(int i=0;i<2400;i++){var entities=client.inspector().list();if(!entities.isEmpty()){
                    String value=entities.getFirst().toString();if(!value.equals(last)){System.out.println("EQUIPMENT_INSPECT "+value);last=value;}seen=true;
                }else if(seen){System.out.println("EQUIPMENT_EMPTY");seen=false;last="";}Thread.sleep(50);}
            }return;
        }
        var root=Path.of(args[0]);var m=JsonParser.parseString(Files.readString(root.resolve("docs/examples/equipment_components/initial.json"))).getAsJsonObject();
        var logs=new ArrayList<String>();var health=new HealthReceiver(logs::add);var eq=new EquipmentReceiver(health,logs::add);
        var spawn=m.deepCopy();spawn.addProperty("sequence",1);var life=new JsonObject();life.addProperty("event","spawn");spawn.add("lifecycle",life);health.lifecycle(spawn);
        eq.receive(m);check(eq.state(m).getAsJsonObject("slots").size()==5,"Java initial equipment snapshot");
        var patch=m.deepCopy();patch.addProperty("mode","patch");patch.addProperty("revision",2);patch.addProperty("base_revision",1);
        var slots=new JsonObject();slots.add("held_item",JsonNull.INSTANCE);var equipment=new JsonObject();equipment.add("slots",slots);var components=new JsonObject();components.add("equipment",equipment);patch.add("components",components);
        eq.receive(patch);check(eq.state(m).getAsJsonObject("slots").get("held_item").isJsonNull() && eq.state(m).getAsJsonObject("slots").get("head").isJsonObject(),"Java slot clear keeps armor");
        check(health.state(m).revision()==2 && health.state(m).current()==100,"Java shared revision preserves health");
        check(health.state(m).components().get("name").equals(m.getAsJsonObject("components").get("name")),"Java equipment patch preserves Identity");
        patch.addProperty("mode","unknown");eq.receive(patch);check(health.state(m).revision()==2,"Java invalid mode rejected before projection");patch.addProperty("mode","patch");
        var old=eq.state(m).deepCopy();patch.addProperty("revision",3);patch.addProperty("base_revision",99);eq.receive(patch);check(eq.state(m).equals(old)&&health.state(m).revision()==2,"Java baseline atomic rejection");
        patch.addProperty("base_revision",2);slots.add("legs",JsonNull.INSTANCE);eq.receive(patch);check(eq.state(m).equals(old),"Java invalid slot atomic rejection");slots.remove("legs");
        var badHealth=new JsonObject();badHealth.addProperty("current",101);badHealth.addProperty("max",100);components.add("health",badHealth);eq.receive(patch);check(eq.state(m).equals(old)&&health.state(m).revision()==2,"Java invalid mixed Health atomic rejection");components.remove("health");
        components.add("equipment",JsonNull.INSTANCE);eq.receive(patch);check(eq.state(m)==null && health.state(m).current()==100,"Java equipment remove preserves Health Identity");
        patch.addProperty("revision",4);patch.addProperty("base_revision",3);components.add("equipment",equipment);eq.receive(patch);check(eq.state(m)==null && health.state(m).revision()==3,"Java partial add rejected");
        components.add("equipment",m.getAsJsonObject("components").get("equipment").deepCopy());eq.receive(patch);check(eq.state(m)!=null && health.state(m).revision()==4,"Java complete add");
        var copy=eq.state(m);copy.getAsJsonObject("slots").add("head",JsonNull.INSTANCE);check(eq.state(m).getAsJsonObject("slots").get("head").isJsonObject(),"Java returned equipment deep copy");
        life.addProperty("event","despawn");spawn.addProperty("sequence",2);health.lifecycle(spawn);eq.lifecycle(spawn);check(eq.state(m)==null&&health.state(m)==null,"Java despawn cleanup");
        eq.reset();check(eq.state(m)==null,"Java reconnect cleanup");
        System.out.println("TOTAL "+checks+" passed");
    }
}
