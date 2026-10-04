package io.mc7dtd;
import com.google.gson.*;
import java.nio.file.*;
import java.util.*;
public final class HealthHarness {
 public static void main(String[] args)throws Exception {
  Path root=Path.of(args[0]);var logs=new ArrayList<String>();var h=new HealthReceiver(logs::add);
  var nativeState=JsonParser.parseString(Files.readString(root.resolve("docs/examples/entity_state_v2/7dtd_spawn.json"))).getAsJsonObject();
  nativeState.addProperty("entity_type","7dtd:player");
  var sample=JsonParser.parseString(Files.readString(root.resolve("docs/examples/entity_components/7dtd_snapshot.json"))).getAsJsonObject();
  // Match complete identity to the canonical native fixture.
  for(var n:List.of("source","authority","origin","entity_id","world_id","dimension","entity_type","stream_id")) sample.add(n,nativeState.get(n).deepCopy());
  sample.addProperty("entity_sequence",nativeState.get("sequence").getAsLong());
  var c=new JsonObject();var health=new JsonObject();health.addProperty("current",90);health.addProperty("max",100);c.add("health",health);sample.add("components",c);
  int[] count={0};java.util.function.BiConsumer<Boolean,String> check=(ok,name)->{if(!ok)throw new AssertionError(name);System.out.println("PASS "+name);count[0]++;};
  h.receive(sample);check.accept(h.count()==0,"unknown entity denied");
  h.lifecycle(nativeState);h.receive(sample);check.accept(h.state(sample).current()==90,"initial snapshot");
  var patch=sample.deepCopy();patch.addProperty("mode","patch");patch.addProperty("revision",2);patch.addProperty("base_revision",1);patch.getAsJsonObject("components").getAsJsonObject("health").addProperty("current",75);
  h.receive(patch);check.accept(h.state(sample).current()==75,"partial health update");
  h.receive(sample);check.accept(h.state(sample).current()==75,"stale snapshot ignored");
  for(var mutation:List.of("authority","origin","entity_sequence","baseline","range","fields","bool","unknown","version","fraction")) {
   var bad=patch.deepCopy();bad.addProperty("revision",3);bad.addProperty("base_revision",2);
   switch(mutation){case "authority"->bad.addProperty("authority","minecraft");case "origin"->bad.getAsJsonObject("origin").addProperty("world_id","bad");case "entity_sequence"->bad.addProperty("entity_sequence",999);case "baseline"->bad.addProperty("base_revision",99);case "range"->bad.getAsJsonObject("components").getAsJsonObject("health").addProperty("current",101);case "fields"->bad.getAsJsonObject("components").getAsJsonObject("health").remove("max");case "bool"->bad.getAsJsonObject("components").getAsJsonObject("health").addProperty("current",true);case "unknown"->bad.getAsJsonObject("components").addProperty("inventory",true);case "version"->bad.addProperty("version",2);case "fraction"->bad.addProperty("revision",3.5);}
   h.receive(bad);check.accept(h.state(sample).revision()==2 && h.state(sample).current()==75,"atomic rejection "+mutation);
  }
  var remove=patch.deepCopy();remove.addProperty("revision",3);remove.addProperty("base_revision",2);remove.getAsJsonObject("components").add("health",JsonNull.INSTANCE);
  h.receive(remove);check.accept(h.state(sample).current()==null,"explicit remove");
  sample.addProperty("revision",4);h.receive(sample);check.accept(h.state(sample).current()==90,"fresh snapshot after remove");
  sample.addProperty("revision",5);sample.add("components",new JsonObject());h.receive(sample);check.accept(h.state(sample).current()==null,"empty snapshot clears");
  nativeState.getAsJsonObject("lifecycle").addProperty("event","despawn");nativeState.addProperty("sequence",nativeState.get("sequence").getAsLong()+1);h.lifecycle(nativeState);
  check.accept(h.count()==0,"despawn clears components");h.receive(sample);check.accept(h.count()==0,"late component denied");
  h.reset();check.accept(h.count()==0,"reset clears");System.out.println("TOTAL "+count[0]+" passed");
 }
}
