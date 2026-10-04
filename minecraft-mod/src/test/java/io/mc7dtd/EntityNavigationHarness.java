package io.mc7dtd;

import com.google.gson.*;
import java.nio.file.*;
import java.util.*;

public final class EntityNavigationHarness {
    private static int passed;
    private static void check(boolean ok,String label){if(!ok)throw new AssertionError(label);passed++;System.out.println(label+" PASS");}
    private static void rejects(Runnable action,String label){try{action.run();throw new AssertionError(label);}catch(IllegalStateException|IllegalArgumentException expected){check(true,label);}}
    private static JsonObject spawn(Path root,String id,double x,double y,double z)throws Exception {
        var e=JsonParser.parseString(Files.readString(root.resolve("docs/examples/presentation/spawn.json"))).getAsJsonObject();
        e.addProperty("entity_id",id);e.getAsJsonObject("origin").addProperty("entity_id",id);e.getAsJsonObject("position").addProperty("space","minecraft");
        e.getAsJsonObject("position").addProperty("x",x);e.getAsJsonObject("position").addProperty("y",y);e.getAsJsonObject("position").addProperty("z",z);return e;
    }
    public static void main(String[] args)throws Exception {
        var root=Path.of(args[0]);var inspector=new EntityInspector();var config=new DebugConfig(false,false,true);
        var nav=new EntityDebugNavigation(inspector,config);var origin=new EntityDebugNavigation.Position(0,64,0);
        rejects(()->nav.nearest(origin),"Empty nearest safe");check(nav.formatList(origin).contains("none"),"Empty list safe");
        var a=spawn(root,"00000000-0000-0000-0000-000000000001",20,64,0);var b=spawn(root,"00000000-0000-0000-0000-000000000002",3,68,0);
        inspector.lifecycle(a,null,a.getAsJsonObject("components").getAsJsonObject("presentation"));inspector.lifecycle(b,null,b.getAsJsonObject("components").getAsJsonObject("presentation"));
        var before=inspector.list();var target=nav.nearest(origin);check(target.id().endsWith("2") && origin.distance(target.position())==5,"Nearest Entity");
        check(nav.formatList(origin).contains("1.\nid: "+target.id()) && nav.list(origin).size()==2 && nav.formatList(origin).contains("distance: 5.000"),"Entity List");
        var offset=EntityDebugNavigation.safeOffset(target,p->true);
        check(offset.x()==6 && offset.y()==71 && offset.z()==0 && offset.distance(target.position())>=3,"Teleport安全偏移");
        var attempts=new ArrayList<EntityDebugNavigation.Position>();var fallback=EntityDebugNavigation.safeOffset(target,p->{attempts.add(p);return p.x()==target.position().x() && p.z()>target.position().z();});
        check(attempts.size()==2 && fallback.z()==3,"Blocked landing uses alternate direction");
        rejects(()->EntityDebugNavigation.safeOffset(target,p->false),"No safe landing cancels teleport");
        check(inspector.list().equals(before),"Navigation preserves all fact components and revisions");
        check(nav.toggleFollow(target.id()) && nav.followed().id().equals(target.id()) && !nav.toggleFollow(target.id()) && nav.followed()==null,"Follow启动/停止");
        nav.toggleFollow(target.id());b.getAsJsonObject("lifecycle").addProperty("event","update");b.addProperty("sequence",2);b.getAsJsonObject("position").addProperty("x",7);inspector.lifecycle(b,null,null);
        check(nav.followed().position().x()==7,"Follow reads updated position");
        b.getAsJsonObject("lifecycle").addProperty("event","despawn");inspector.lifecycle(b,null,null);check(nav.followed()==null,"Follow despawn cleanup");
        nav.toggleFollow(a.get("entity_id").getAsString());inspector.reset();check(nav.followed()==null,"Follow disconnect cleanup");
        inspector.lifecycle(a,null,null);nav.toggleFollow(a.get("entity_id").getAsString());inspector.appearanceConnected(false);check(nav.followed()==null && nav.list(origin).isEmpty(),"Peer unavailable stops follow with retained facts");inspector.appearanceConnected(true);inspector.reset();
        var disabled=new EntityDebugNavigation(inspector,new DebugConfig(false,false));rejects(()->disabled.list(origin),"Debug关闭禁止使用");rejects(()->disabled.find("x"),"Disabled teleport target blocked");rejects(()->disabled.toggleFollow("x"),"Disabled follow blocked");
        inspector.lifecycle(a,null,null);var duplicate=a.deepCopy();duplicate.addProperty("stream_id",UUID.randomUUID().toString());inspector.lifecycle(duplicate,null,null);
        rejects(()->nav.find(a.get("entity_id").getAsString()),"Ambiguous scope rejects teleport");
        var local=a.deepCopy();local.addProperty("source","minecraft");local.addProperty("entity_id",UUID.randomUUID().toString());inspector.lifecycle(local,null,null);check(nav.list(origin).size()==2,"Local player excluded");
        check(before.getFirst().getAsJsonObject("components").getAsJsonObject("authority").get("owner").getAsString().equals("7dtd"),"Fact component read-only snapshot");
        var path=root.resolve("work/phase3822-navigation-test/debug-config.json");Files.createDirectories(path.getParent());var warnings=new ArrayList<String>();
        Files.writeString(path,"{\"debug_name_tag\":false,\"debug_logging\":false,\"debug_navigation\":true}");check(DebugConfig.load(path,warnings::add).navigation() && !DebugConfig.load(path,warnings::add).logging(),"Navigation config independent from logging");
        Files.writeString(path,"{\"debug_name_tag\":true,\"debug_logging\":true}");check(!DebugConfig.load(path,warnings::add).navigation() && DebugConfig.load(path,warnings::add).nameTag(),"Legacy config compatible and navigation default off");
        Files.writeString(path,"{\"debug_name_tag\":false,\"debug_logging\":false,\"debug_navigation\":\"true\"}");check(!DebugConfig.load(path,warnings::add).navigation(),"Invalid config fails closed");
        System.out.println("TOTAL "+passed+" passed");
    }
}
