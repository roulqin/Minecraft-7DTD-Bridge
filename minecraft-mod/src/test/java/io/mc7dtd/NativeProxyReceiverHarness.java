package io.mc7dtd;
import java.nio.file.Path;
public final class NativeProxyReceiverHarness {
 public static void main(String[] args)throws Exception {
   Path config=Path.of(args[0]);var catalog=ProxyTypeCatalog.load(Path.of(System.getenv("MC7DTD_TEST_ROOT"),"config/entity_types.json"));
   var scene=new NativeProxyController.Scene(){
     public Object create(NativeProxyController.Snapshot s){System.out.println("Harness proxy created: "+s.id());return s.id();}
     public void update(Object h,NativeProxyController.Snapshot s){System.out.println("Harness proxy updated: "+s.sequence());}
     public void delete(Object h){System.out.println("Harness proxy deleted: "+h);}
   };
   var controller=new NativeProxyController(scene,catalog,System.out::println);Object world=new Object();
   try(var client=new BridgeClient(config,System.out::println,controller::enqueue,controller::reset)) {
     client.start();long end=System.nanoTime()+Long.parseLong(args[1])*1_000_000_000L;
     while(System.nanoTime()<end){controller.tick(world);Thread.sleep(25);}
   }
 }
}
