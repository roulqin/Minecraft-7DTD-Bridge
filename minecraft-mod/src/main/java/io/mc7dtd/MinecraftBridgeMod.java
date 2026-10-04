package io.mc7dtd;

import java.nio.file.Path;
import net.fabricmc.api.ClientModInitializer;
import org.slf4j.LoggerFactory;
import net.fabricmc.fabric.api.client.event.lifecycle.v1.ClientTickEvents;
import net.fabricmc.fabric.api.client.event.lifecycle.v1.ClientLifecycleEvents;

public final class MinecraftBridgeMod implements ClientModInitializer {
    private BridgeClient client;
    @Override public void onInitializeClient() {
        var log = LoggerFactory.getLogger("MC7DTD");
        try {
            String root = System.getenv().getOrDefault("MC7DTD_ROOT", "D:\\wenjian\\minecraft\\7-M");
            var scene = new MinecraftProxyScene();
            var proxies = new NativeProxyController(scene,
                ProxyTypeCatalog.load(Path.of(root, "config", "entity_types.json")), log::info);
            client = new BridgeClient(Path.of(root, "config", "network.json"), log::info, proxies::enqueue, proxies::reset);
            client.inspector().visualWorld(null);
            client.inspector().appearanceConnected(false);
            var appearance=new ProxyAppearanceAdapter(ProxyAppearanceConfig.load(Path.of(root,"config","proxy_appearance.json"),log::warn));
            client.inspector().appearanceAdapter(appearance);
            scene.enableAppearance(client.inspector(),appearance,RendererMapping.load(Path.of(root,"config","renderer_mapping.json"),log::warn));
            scene.enableDebug(client.inspector(), client.debugConfig());
            EntityDebugCommands.register(client.inspector(), log::info);
            var navigation=new MinecraftDebugNavigation(client.inspector(),client.debugConfig(),log::info);
            navigation.register();
            ClientTickEvents.END_CLIENT_TICK.register(game -> { client.inspector().visualWorld(game.world); proxies.tick(game.world); scene.tickAppearance(); scene.tickDebug(); navigation.tick(game); });
            ClientLifecycleEvents.CLIENT_STOPPING.register(game -> { client.inspector().visualWorld(null); proxies.reset(); proxies.tick(null); });
            Runtime.getRuntime().addShutdownHook(new Thread(client::close, "MC7DTD-shutdown"));
            client.start();
            PlayerPositionSampler.register(client);
            PlayerProxySampler.register(client);
            if ("1".equals(System.getenv("MC7DTD_ENTITY_TEST"))) {
                EntityTestCommands.register(client);
                PresentationTestCommands.register(client);
                log.info("Manual entity acceptance commands enabled; no automatic lifecycle tracking");
            }
        } catch (Exception ex) { log.error("MC7DTD initialization failed", ex); }
    }
}
