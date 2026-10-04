package io.mc7dtd;

import com.google.gson.JsonParser;
import com.mojang.brigadier.CommandDispatcher;
import com.mojang.brigadier.exceptions.CommandSyntaxException;
import java.nio.file.*;
import java.util.*;

public final class EntityGotoHarness {
    private static int passed;
    private static void check(boolean ok,String label){if(!ok)throw new AssertionError(label);passed++;System.out.println(label+" PASS");}
    public static void main(String[] args)throws Exception {
        var root=Path.of(args[0]);String id="3b7424bc-4917-4f19-8f46-dd2882f09825";
        var entity=JsonParser.parseString(Files.readString(root.resolve("docs/examples/presentation/spawn.json"))).getAsJsonObject();entity.addProperty("entity_id",id);entity.getAsJsonObject("origin").addProperty("entity_id",id);entity.getAsJsonObject("position").addProperty("space","minecraft");
        var inspector=new EntityInspector();inspector.lifecycle(entity,null,null);var before=inspector.list();var navigation=new EntityDebugNavigation(inspector,new DebugConfig(false,false,true));
        var dispatcher=new CommandDispatcher<Object>();var errors=new ArrayList<String>();var selected=new ArrayList<EntityDebugNavigation.Target>();Object source=new Object();
        EntityGotoCommand.register(dispatcher,true,()->navigation.list(new EntityDebugNavigation.Position(0,0,0)).stream().map(EntityDebugNavigation.Target::id).toList(),raw->{try{selected.add(EntityGotoCommand.resolve(navigation,raw));return 1;}catch(IllegalArgumentException ex){errors.add(ex.getMessage());return 0;}});
        check(dispatcher.execute("mc7dtd_entity_goto "+id,source)==1 && selected.getLast().id().equals(id),"Entity Goto UUID");
        check(dispatcher.execute("mc7dtd_entity_goto "+id.toUpperCase(Locale.ROOT),source)==1,"Uppercase UUID normalized");
        check(dispatcher.execute("mc7dtd_entity_teleport "+id,source)==1,"Teleport alias regression");
        String missing="00000000-0000-0000-0000-000000000099";
        check(dispatcher.execute("mc7dtd_entity_goto "+missing,source)==0 && errors.getLast().equals("Entity not found: "+missing+"\nCurrent synced entities: 1"),"Entity Not Found");
        check(dispatcher.execute("mc7dtd_entity_goto invalid-uuid",source)==0 && errors.getLast().startsWith("Invalid UUID:"),"Invalid UUID");
        check(dispatcher.execute("mc7dtd_entity_goto 1-1-1-1-1",source)==0,"Abbreviated UUID rejected");
        var suggestions=dispatcher.getCompletionSuggestions(dispatcher.parse("mc7dtd_entity_goto ",source)).get();check(suggestions.getList().size()==1 && suggestions.getList().getFirst().getText().equals(id),"Entity ID completion");
        check(dispatcher.getCompletionSuggestions(dispatcher.parse("mc7dtd_entity_goto 3b74",source)).get().getList().size()==1,"UUID prefix completion");
        var offset=EntityDebugNavigation.safeOffset(selected.getFirst(),p->true);check(offset.x()==selected.getFirst().position().x()+3 && offset.y()==selected.getFirst().position().y()+3,"Safe Teleport Regression");
        check(inspector.list().equals(before),"Command lookup leaves facts unchanged");
        var disabled=new CommandDispatcher<Object>();EntityGotoCommand.register(disabled,false,List::of,raw->{throw new AssertionError("disabled handler called");});
        try{disabled.execute("mc7dtd_entity_goto "+id,source);throw new AssertionError();}catch(CommandSyntaxException expected){check(disabled.getRoot().getChildren().isEmpty(),"Debug Disable Regression");}
        inspector.reset();check(dispatcher.getCompletionSuggestions(dispatcher.parse("mc7dtd_entity_goto ",source)).get().getList().isEmpty(),"Disconnect clears completion");
        check(dispatcher.execute("mc7dtd_entity_goto "+id,source)==0 && errors.getLast().endsWith("Current synced entities: 0"),"Empty not-found count");
        System.out.println("TOTAL "+passed+" passed");
    }
}
