package io.mc7dtd;
import java.nio.file.Path;
public final class ClientHarness {
    public static void main(String[] args) throws Exception {
        try (var client = new BridgeClient(Path.of(args[0]), System.out::println)) {
            client.start();
            long end = System.nanoTime() + Long.parseLong(args[1]) * 1_000_000_000L;
            while (System.nanoTime() < end) {
                if (args.length > 2 && args[2].equals("positions")) client.publishPlayerPosition(100, 64, 200);
                Thread.sleep(500);
            }
        }
    }
}
