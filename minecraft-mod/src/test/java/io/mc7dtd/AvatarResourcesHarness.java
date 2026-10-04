package io.mc7dtd;

import java.nio.file.*;
import java.util.*;

public final class AvatarResourcesHarness {
    private static int passed;
    private static void check(boolean ok,String label){if(!ok)throw new AssertionError(label);passed++;System.out.println(label+" PASS");}
    private static void rejects(Runnable action,String label){try{action.run();throw new AssertionError(label);}catch(RuntimeException expected){check(true,label);}}
    public static void main(String[] args)throws Exception {
        var root=Path.of(args[0]);var avatar=root.resolve("assets/avatar");var result=AvatarResources.load(avatar);
        check(result.ready() && !result.configFallback() && result.config().avatarId().equals("minecraft_alex_default"),"Config Load");
        check(result.config().skeleton().equals("minecraft_avatar_v1"),"Skeleton identifier parse");
        check(AvatarResources.parse("{\"avatar_id\":\"default_player\",\"model\":\"minecraft_humanoid\",\"variant\":\"alex_slim\",\"skin\":\"skins/player_default.png\"}").skeleton().equals("minecraft_avatar_v1"),"Legacy four-field config compatible");
        check(result.skin().path().equals(avatar.resolve("skins/player_default.png").toRealPath()) && result.skin().source()==AvatarResources.Source.LOCAL,"Skin Path Resolve");
        check(result.config().variant()==AvatarResources.Variant.ALEX_SLIM,"Alex Variant Parse");
        var temporary=Files.createTempDirectory(root.resolve("work/phase3841-test"),"resources-");Files.createDirectories(temporary.resolve("skins"));Files.copy(avatar.resolve("skins/player_default.png"),temporary.resolve("skins/player_default.png"));
        String json=Files.readString(avatar.resolve("avatar_config.json"));Files.writeString(temporary.resolve("avatar_config.json"),json.replace("skins/player_default.png","skins/missing.png"));
        rejects(()->AvatarResources.parse(json.replace("minecraft_avatar_v1","unknown_skeleton")),"Unknown skeleton rejected");
        var fallback=AvatarResources.load(temporary);check(fallback.ready() && fallback.skin().source()==AvatarResources.Source.DEFAULT_FILE && !fallback.warnings().isEmpty(),"Missing Skin Fallback");
        Files.delete(temporary.resolve("skins/player_default.png"));fallback=AvatarResources.load(temporary);check(fallback.ready() && fallback.skin().source()==AvatarResources.Source.BUNDLED && fallback.skin().path()==null,"Missing default file bundled fallback");
        Files.writeString(temporary.resolve("avatar_config.json"),"{}");fallback=AvatarResources.load(temporary);check(fallback.configFallback() && fallback.ready() && fallback.config().equals(AvatarResources.Config.defaults()),"Invalid config defaults");
        Files.delete(temporary.resolve("avatar_config.json"));check(AvatarResources.load(temporary).configFallback(),"Missing config defaults");
        rejects(()->AvatarResources.parse(json.replace("alex_slim","unknown")),"Unknown variant rejected");
        check(AvatarResources.parse(json.replace("alex_slim","steve_classic")).variant()==AvatarResources.Variant.STEVE_CLASSIC,"Classic identifier compatible");
        rejects(()->AvatarResources.parse(json.replace("\"variant\":\"alex_slim\"","\"variant\":\"alex_slim\",\"variant\":\"steve_classic\"")),"Duplicate config key rejected");
        rejects(()->{try{AvatarResources.resolve(avatar,"../outside.png");}catch(java.io.IOException ex){throw new IllegalArgumentException(ex);}},"Path traversal rejected");
        rejects(()->{try{AvatarResources.resolve(avatar,"https://example.invalid/skin.png");}catch(java.io.IOException ex){throw new IllegalArgumentException(ex);}},"Remote URL rejected");
        var copy=result.skin().png();copy[0]=0;check(result.skin().png()[0]==(byte)137,"Skin bytes immutable copy");
        Files.write(temporary.resolve("skins/player_default.png"),new byte[]{1,2,3});check(AvatarResources.load(temporary).skin().source()==AvatarResources.Source.BUNDLED,"Corrupt PNG fallback");
        System.out.println("TOTAL "+passed+" passed");
    }
}
