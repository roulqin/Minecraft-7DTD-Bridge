package io.mc7dtd;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.google.gson.JsonNull;
import java.nio.file.Path;
import java.nio.file.Files;
import java.util.ArrayList;

public final class PresentationHarness {
    static int count;
    static void check(boolean valid, String name) { if (!valid) throw new AssertionError(name); count++; System.out.println("PASS "+name); }
    public static void main(String[] args) throws Exception {
        if (args.length > 1 && args[1].equals("listen")) {
            try (var bridge = new BridgeClient(Path.of(args[0]), System.out::println)) { bridge.start(); Thread.sleep(120000); } return;
        }
        var entity=JsonParser.parseString(Files.readString(Path.of(args[0],"docs/examples/entity_state_v2/7dtd_spawn.json"))).getAsJsonObject();
        var logs=new ArrayList<String>(); var receiver=new PresentationReceiver(logs::add);
        var components=new JsonObject(); var p=new JsonObject(); p.addProperty("renderer","humanoid"); p.addProperty("model","survivor"); p.addProperty("variant","test"); p.addProperty("scale",1.0); components.add("presentation",p); entity.add("components",components);
        receiver.lifecycle(entity); check(receiver.get(entity).get("model").getAsString().equals("survivor"),"presentation initial saved");
        receiver.get(entity).addProperty("model","tampered"); check(receiver.get(entity).get("model").getAsString().equals("survivor"),"snapshot copy isolation");
        entity.getAsJsonObject("lifecycle").addProperty("event","update");var delta=new JsonObject();delta.addProperty("scale",1.5);components.add("presentation",delta);receiver.lifecycle(entity);
        check(receiver.get(entity).get("scale").getAsDouble()==1.5 && receiver.get(entity).get("model").getAsString().equals("survivor"),"partial scale preserves model");
        entity.remove("components");receiver.lifecycle(entity);check(receiver.get(entity).get("scale").getAsDouble()==1.5,"omitted component retained");
        components.add("presentation",JsonNull.INSTANCE);entity.add("components",components);receiver.lifecycle(entity);check(receiver.get(entity).get("renderer").getAsString().equals("unknown"),"remove fallback");
        components.add("presentation",p);receiver.lifecycle(entity);check(receiver.get(entity).get("model").getAsString().equals("survivor"),"add after remove");
        entity.getAsJsonObject("lifecycle").addProperty("event","despawn");receiver.lifecycle(entity);check(receiver.get(entity)==null,"despawn cleanup");
        entity.getAsJsonObject("lifecycle").addProperty("event","spawn");entity.remove("components");receiver.lifecycle(entity);check(receiver.get(entity).get("scale").getAsDouble()==1,"legacy default");
        receiver.reset();check(receiver.get(entity)==null,"connection reset");
        check(logs.stream().anyMatch(s->s.contains("scale=1.5")),"update logged");
        System.out.println("TOTAL "+count+" passed");
    }
}
