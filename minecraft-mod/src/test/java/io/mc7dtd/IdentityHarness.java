package io.mc7dtd;
import com.google.gson.*;
import java.nio.file.*;
import java.util.*;
public final class IdentityHarness {
 public static void main(String[] args)throws Exception {
  Path root=Path.of(args[0]);var logs=new ArrayList<String>();var receiver=new HealthReceiver(logs::add);int[] checks={0};
  java.util.function.BiConsumer<Boolean,String> check=(ok,name)->{if(!ok)throw new AssertionError(name);checks[0]++;System.out.println("PASS "+name);};
  var entity=JsonParser.parseString(Files.readString(root.resolve("docs/examples/entity_state_v2/7dtd_spawn.json"))).getAsJsonObject();entity.addProperty("entity_type","7dtd:player");
  var initial=JsonParser.parseString(Files.readString(root.resolve("docs/examples/entity_components/7dtd_snapshot.json"))).getAsJsonObject();
  for(var key:List.of("source","authority","origin","entity_id","world_id","dimension","entity_type","stream_id"))initial.add(key,entity.get(key).deepCopy());
  var c=JsonParser.parseString("{\"health\":{\"current\":90,\"max\":100},\"name\":{\"text\":\"原生名字\",\"display_name\":\"显示名\"},\"custom_metadata\":{\"tag.label\":\"one\"}}").getAsJsonObject();initial.add("components",c);
  receiver.lifecycle(entity);receiver.receive(initial);check.accept(receiver.state(initial).components().getAsJsonObject("name").get("display_name").getAsString().equals("显示名"),"initial identity");
  var patch=initial.deepCopy();patch.addProperty("mode","patch");patch.addProperty("revision",2);patch.addProperty("base_revision",1);
  patch.add("components",JsonParser.parseString("{\"name\":{\"text\":\"原生名字\",\"display_name\":\"已更新\"}}").getAsJsonObject());receiver.receive(patch);
  check.accept(receiver.state(initial).current()==90 && receiver.state(initial).components().has("custom_metadata"),"partial identity keeps health and metadata");
  patch.getAsJsonObject("components").getAsJsonObject("name").addProperty("text","caller changed");check.accept(!receiver.state(initial).components().getAsJsonObject("name").get("text").getAsString().equals("caller changed"),"caller mutation isolated");
  for(var name:List.of("empty","control","too_long","missing_text","extra","tag_bool","tag_nested","tag_null","tag_control","nested_metadata","authority","base")) {
   var bad=initial.deepCopy();bad.addProperty("mode","patch");bad.addProperty("revision",3);bad.addProperty("base_revision",2);
   switch(name){case "empty"->bad.getAsJsonObject("components").getAsJsonObject("name").addProperty("display_name","");case "control"->bad.getAsJsonObject("components").getAsJsonObject("name").addProperty("display_name","a\nb");case "too_long"->bad.getAsJsonObject("components").getAsJsonObject("name").addProperty("text","x".repeat(129));case "missing_text"->bad.getAsJsonObject("components").getAsJsonObject("name").remove("text");case "extra"->bad.getAsJsonObject("components").getAsJsonObject("name").addProperty("authority","minecraft");case "tag_bool"->bad.getAsJsonObject("components").getAsJsonObject("custom_metadata").addProperty("tag.label",true);case "tag_nested"->bad.getAsJsonObject("components").getAsJsonObject("custom_metadata").add("tag.label",new JsonObject());case "tag_null"->bad.getAsJsonObject("components").getAsJsonObject("custom_metadata").add("tag.label",JsonNull.INSTANCE);case "tag_control"->bad.getAsJsonObject("components").getAsJsonObject("custom_metadata").addProperty("tag.label","a\nb");case "nested_metadata"->bad.getAsJsonObject("components").getAsJsonObject("custom_metadata").add("object",new JsonObject());case "authority"->bad.addProperty("authority","minecraft");case "base"->bad.addProperty("base_revision",99);}
   receiver.receive(bad);check.accept(receiver.state(initial).revision()==2 && receiver.state(initial).current()==90,"atomic reject "+name);
  }
  receiver.receive(initial);check.accept(receiver.state(initial).revision()==2,"stale identity ignored");
  var remove=patch.deepCopy();remove.addProperty("revision",3);remove.addProperty("base_revision",2);remove.add("components",JsonParser.parseString("{\"name\":null,\"custom_metadata\":null}").getAsJsonObject());receiver.receive(remove);
  check.accept(receiver.state(initial).current()==90 && receiver.state(initial).components().size()==1,"identity remove keeps health");
  initial.addProperty("revision",4);receiver.receive(initial);check.accept(receiver.state(initial).components().has("name"),"snapshot restore");
  initial.addProperty("revision",5);initial.add("components",new JsonObject());receiver.receive(initial);check.accept(receiver.state(initial).components().size()==0 && receiver.state(initial).current()==null,"empty snapshot clears all");
  check.accept(logs.stream().anyMatch(l->l.contains("identity received")&&l.contains("removed=true")),"actual receiver removal log");
  entity.getAsJsonObject("lifecycle").addProperty("event","despawn");entity.addProperty("sequence",2);receiver.lifecycle(entity);check.accept(receiver.count()==0,"despawn clears shared state");
  receiver.receive(initial);check.accept(receiver.count()==0,"late identity denied");receiver.reset();check.accept(receiver.count()==0,"connection reset");System.out.println("TOTAL "+checks[0]+" passed");
 }
}
