package io.mc7dtd;
import com.google.gson.*;
import java.util.*;
public final class IdentityComponent {
 private IdentityComponent() { }
 public static void text(JsonElement value,int max) {
  if(!value.isJsonPrimitive() || !value.getAsJsonPrimitive().isString())throw new IllegalArgumentException("identity text");
  String s=value.getAsString();int count=s.codePointCount(0,s.length());
  if(count<1 || count>max || s.codePoints().anyMatch(c->c<32 || c>=127 && c<=159 || c>=0xD800 && c<=0xDFFF))throw new IllegalArgumentException("identity text range/control");
 }
 public static void name(JsonElement value) {
  var n=value.getAsJsonObject();if(!n.has("text") || n.keySet().stream().anyMatch(k->!Set.of("text","display_name").contains(k)))throw new IllegalArgumentException("name fields");
  text(n.get("text"),128);if(n.has("display_name"))text(n.get("display_name"),128);
 }
 public static void metadata(JsonElement value) {
  var v=value.getAsJsonObject();if(v.size()>32)throw new IllegalArgumentException("metadata limit");
  for(var p:v.entrySet()) {
   String key=p.getKey();var state=p.getValue();if(key.length()>64 || !key.matches("[a-zA-Z][a-zA-Z0-9_.-]*"))throw new IllegalArgumentException("metadata key");
   if(key.startsWith("tag.")){if(key.length()==4)throw new IllegalArgumentException("tag key");text(state,256);continue;}
   if(state.isJsonNull())continue;if(!state.isJsonPrimitive())throw new IllegalArgumentException("metadata flat");var primitive=state.getAsJsonPrimitive();
   if(primitive.isString() && state.getAsString().codePointCount(0,state.getAsString().length())>256 || primitive.isNumber() && !Double.isFinite(state.getAsDouble()))throw new IllegalArgumentException("metadata scalar");
  }
 }
}
