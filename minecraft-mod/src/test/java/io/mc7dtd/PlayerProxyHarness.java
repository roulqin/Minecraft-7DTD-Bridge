package io.mc7dtd;
import com.google.gson.JsonObject;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;

public final class PlayerProxyHarness {
    private static int passed;
    private static void check(boolean ok, String name) {
        if (!ok) throw new AssertionError(name);
        passed++; System.out.println("PASS " + name);
    }
    public static void main(String[] args) throws Exception {
        var messages = new ArrayList<JsonObject>();
        var tracker = new PlayerProxyTracker((message, epoch) -> { messages.add(message); return true; });
        var owner = new Object();
        var first = new PlayerProxyTracker.Sample(owner, "test-world", "minecraft:overworld", 10,64,20,-90,0);
        tracker.tick(first,false,0,0); check(messages.isEmpty(),"disconnected does not emit");
        tracker.tick(first,false,1,0); check(messages.size()==1 && event(messages.getLast()).equals("spawn"),"enter emits automatic spawn");
        var id=messages.getLast().get("entity_id").getAsString();
        check(messages.getLast().getAsJsonObject("rotation").get("yaw").getAsDouble()==270,"yaw normalized");
        tracker.tick(first,false,1,499_999_999); check(messages.size()==1,"500ms rate limit");
        var moved = new PlayerProxyTracker.Sample(owner,"test-world","minecraft:overworld",11,65,22,90,30);
        tracker.tick(moved,true,1,600_000_000); check(messages.size()==1,"pause does not update");
        tracker.tick(moved,false,1,600_000_000); check(event(messages.getLast()).equals("update"),"resume emits update");
        check(messages.getLast().get("entity_id").getAsString().equals(id),"update preserves identity");
        check(messages.getLast().getAsJsonObject("rotation").get("pitch").getAsDouble()==30,"pitch captured");
        tracker.tick(null,true,1,700_000_000); check(event(messages.getLast()).equals("despawn"),"exit emits despawn even paused");
        check(messages.getLast().get("position").isJsonNull() && messages.getLast().get("rotation").isJsonNull() && messages.getLast().getAsJsonObject("metadata").isEmpty(),"despawn obeys protocol");
        var count=messages.size(); tracker.tick(null,false,1,800_000_000); check(messages.size()==count,"exit idempotent");
        tracker.tick(first,false,1,900_000_000); check(!messages.getLast().get("entity_id").getAsString().equals(id),"reentry uses new lifetime");
        count=messages.size(); tracker.tick(first,false,2,1_000_000_000);
        check(messages.size()==count+2 && event(messages.get(count)).equals("despawn") && event(messages.getLast()).equals("spawn"),"target ready resync retires old key before spawn");
        tracker.tick(first,false,0,1_100_000_000); count=messages.size(); tracker.tick(first,false,3,1_200_000_000);
        check(messages.size()==count+1 && event(messages.getLast()).equals("spawn"),"Bridge reconnect sends fresh spawn");
        var nether=new PlayerProxyTracker.Sample(owner,"test-world","minecraft:the_nether",0,70,0,0,0);
        count=messages.size(); tracker.tick(nether,false,3,1_300_000_000);
        check(messages.size()==count+2 && messages.get(count).getAsJsonObject("lifecycle").get("reason").getAsString().equals("dimension_changed"),"dimension transition despawns then spawns");
        var rejectedEvents=new ArrayList<String>();
        var rejected=new PlayerProxyTracker((message,epoch)->{ rejectedEvents.add(event(message)); return false; });
        rejected.tick(first,false,1,0); rejected.tick(null,false,1,1); rejected.tick(first,false,1,2);
        check(rejectedEvents.equals(java.util.List.of("spawn","spawn")),"rejected spawn retries without phantom despawn");
        var output=Path.of(args[0],"work/phase3_5_1-test"); Files.createDirectories(output);
        Files.writeString(output.resolve("java-results.json"),"{\"passed\":"+passed+",\"failed\":0}");
        System.out.println("Player tracker checks passed: " + passed);
    }
    private static String event(JsonObject message) { return message.getAsJsonObject("lifecycle").get("event").getAsString(); }
}
