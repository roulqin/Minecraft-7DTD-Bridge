package io.mc7dtd;

import java.nio.file.Path;
import net.fabricmc.api.ClientModInitializer;
import org.slf4j.LoggerFactory;

public final class MinecraftBridgeMod implements ClientModInitializer {
    private BridgeClient client;
    @Override public void onInitializeClient() {
        var log = LoggerFactory.getLogger("MC7DTD");
        try {
            String root = System.getenv().getOrDefault("MC7DTD_ROOT", "D:\\wenjian\\minecraft\\7-M");
            client = new BridgeClient(Path.of(root, "config", "network.json"), log::info);
            Runtime.getRuntime().addShutdownHook(new Thread(client::close, "MC7DTD-shutdown"));
            client.start();
            PlayerPositionSampler.register(client);
            PlayerProxySampler.register(client);
            if ("1".equals(System.getenv("MC7DTD_ENTITY_TEST"))) {
                EntityTestCommands.register(client);
                log.info("Manual entity acceptance commands enabled; no automatic lifecycle tracking");
            }
        } catch (Exception ex) { log.error("MC7DTD initialization failed", ex); }
    }
}
