package io.mc7dtd;

import com.mojang.brigadier.CommandDispatcher;
import com.mojang.brigadier.arguments.StringArgumentType;
import com.mojang.brigadier.builder.LiteralArgumentBuilder;
import com.mojang.brigadier.builder.RequiredArgumentBuilder;
import java.util.List;
import java.util.Locale;
import java.util.UUID;
import java.util.function.Supplier;
import java.util.function.ToIntFunction;

/** Shared command tree used by Fabric and the real Brigadier parsing harness. */
public final class EntityGotoCommand {
    public static <S> void register(CommandDispatcher<S> dispatcher,boolean enabled,Supplier<List<String>> ids,ToIntFunction<String> execute) {
        if(!enabled)return;
        for(var name:List.of("mc7dtd_entity_goto","mc7dtd_entity_teleport")) {
            dispatcher.register(LiteralArgumentBuilder.<S>literal(name).then(RequiredArgumentBuilder.<S,String>argument("entity_id",StringArgumentType.word())
                .suggests((context,builder)->{String prefix=builder.getRemainingLowerCase();ids.get().stream().distinct().sorted().filter(id->id.toLowerCase(Locale.ROOT).startsWith(prefix)).forEach(builder::suggest);return builder.buildFuture();})
                .executes(context->execute.applyAsInt(StringArgumentType.getString(context,"entity_id")))));
        }
    }
    public static EntityDebugNavigation.Target resolve(EntityDebugNavigation navigation,String input) {
        if(!navigation.enabled())throw new IllegalStateException("Debug navigation disabled in config/debug.json");
        if(!input.matches("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"))throw new IllegalArgumentException("Invalid UUID: "+input);
        String id=UUID.fromString(input).toString();
        var entities=navigation.list(new EntityDebugNavigation.Position(0,0,0));
        if(entities.stream().noneMatch(target->target.id().equals(id)))throw new IllegalArgumentException("Entity not found: "+id+"\nCurrent synced entities: "+entities.size());
        return navigation.find(id);
    }
}
