package io.mc7dtd;
import java.nio.file.Path;
public final class HealthReceiverHarness {
 public static void main(String[] args)throws Exception {
  try(var client=new BridgeClient(Path.of(args[0]),System.out::println)) {
   client.start();Thread.sleep(Long.parseLong(args[1])*1000);
  }
 }
}
