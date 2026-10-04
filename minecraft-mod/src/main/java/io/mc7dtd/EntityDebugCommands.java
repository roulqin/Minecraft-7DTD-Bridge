package io.mc7dtd;

import java.util.function.Consumer;
import net.fabricmc.fabric.api.client.command.v2.ClientCommandRegistrationCallback;
import net.minecraft.text.Text;
import static net.fabricmc.fabric.api.client.command.v2.ClientCommandManager.literal;

public final class EntityDebugCommands {
    public static void register(EntityInspector inspector,Consumer<String> log) {
        ClientCommandRegistrationCallback.EVENT.register((dispatcher,registryAccess)->{
            dispatcher.register(literal("mc7dtd_entity_inspect").executes(context->{
                String output=EntityInspector.format(inspector.latest());
                context.getSource().sendFeedback(Text.literal(output));log.accept(output);return 1;
            }));
            dispatcher.register(literal("mc7dtd_debug_entities").executes(context->{
                String output=inspector.formatList();context.getSource().sendFeedback(Text.literal(output));log.accept(output);return 1;
            }));
        });
    }
}
