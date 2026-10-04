package io.mc7dtd;

import com.google.gson.*;
import com.google.gson.stream.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import javax.imageio.ImageIO;

/** Independent local resource manager. Does not create models, textures, entities or network messages. */
public final class AvatarResources {
    public enum Variant { ALEX_SLIM, STEVE_CLASSIC }
    public enum Source { LOCAL, DEFAULT_FILE, BUNDLED }
    public record Config(String avatarId,String model,Variant variant,String skin,String skeleton) {
        public Config(String avatarId,String model,Variant variant,String skin){this(avatarId,model,variant,skin,"minecraft_avatar_v1");}
        public static Config defaults(){return new Config("minecraft_alex_default","minecraft_humanoid",Variant.ALEX_SLIM,"skins/player_default.png","minecraft_avatar_v1");}
    }
    public record Skin(Path path,Source source,byte[] png) {
        public Skin {png=png.clone();}
        @Override public byte[] png(){return png.clone();}
    }
    public record Result(Config config,Skin skin,boolean configFallback,List<String> warnings) {
        public Result {warnings=List.copyOf(warnings);}
        public boolean ready(){return skin!=null;}
    }
    private static final int MAX_PNG=262144,MAX_CONFIG=4096;
    public static Result load(Path avatarRoot) {
        var warnings=new ArrayList<String>();Config config;boolean configFallback=false;
        try {config=parse(read(avatarRoot.resolve("avatar_config.json"),MAX_CONFIG));}
        catch(IOException|RuntimeException ex){config=Config.defaults();configFallback=true;warnings.add("Avatar config fallback: "+ex.getMessage());}
        Skin skin;
        try {skin=file(avatarRoot,config.skin(),Source.LOCAL);}
        catch(IOException|RuntimeException ex) {
            warnings.add("Requested skin unavailable: "+ex.getMessage());
            try {skin=file(avatarRoot,Config.defaults().skin(),Source.DEFAULT_FILE);}
            catch(IOException|RuntimeException missing) {
                warnings.add("Default skin file unavailable; using bundled skin");
                try(var stream=AvatarResources.class.getResourceAsStream("/io/mc7dtd/avatar/player_default.png")) {
                    if(stream==null)throw new IOException("bundled skin missing");var bytes=stream.readNBytes(MAX_PNG+1);validatePng(bytes);skin=new Skin(null,Source.BUNDLED,bytes);
                }catch(IOException|RuntimeException unavailable){skin=null;warnings.add("Avatar resources unavailable: "+unavailable.getMessage());}
            }
        }
        return new Result(config,skin,configFallback,warnings);
    }
    public static Path resolve(Path root,String relative)throws IOException {
        var skin=Path.of(relative);if(skin.isAbsolute() || relative.isBlank() || relative.indexOf(':')>=0 || !relative.toLowerCase(Locale.ROOT).endsWith(".png"))throw new IllegalArgumentException("skin must be a local relative PNG");
        var base=root.toAbsolutePath().normalize();var path=base.resolve(skin).normalize();
        if(!path.startsWith(base))throw new IllegalArgumentException("skin path escapes avatar root");
        var real=path.toRealPath();if(!real.startsWith(base.toRealPath()) || !Files.isRegularFile(real))throw new IllegalArgumentException("skin path escapes avatar root or is not a file");return real;
    }
    private static Skin file(Path root,String relative,Source source)throws IOException {var path=resolve(root,relative);byte[] bytes;try(var stream=Files.newInputStream(path)){bytes=stream.readNBytes(MAX_PNG+1);}validatePng(bytes);return new Skin(path,source,bytes);}
    private static String read(Path path,int max)throws IOException {try(var stream=Files.newInputStream(path)){var bytes=stream.readNBytes(max+1);if(bytes.length>max)throw new IllegalArgumentException("config size");return new String(bytes,StandardCharsets.UTF_8);}}
    public static Config parse(String raw) {
        if(raw.getBytes(StandardCharsets.UTF_8).length>MAX_CONFIG)throw new IllegalArgumentException("config size");
        try(var reader=new JsonReader(new StringReader(raw))) {
            reader.setStrictness(Strictness.STRICT);reader.beginObject();var fields=new HashMap<String,String>();
            while(reader.hasNext()){String key=reader.nextName();if(!Set.of("avatar_id","model","variant","skin","skeleton").contains(key) || fields.containsKey(key) || reader.peek()!=JsonToken.STRING)throw new IllegalArgumentException("config fields/type");fields.put(key,reader.nextString());}
            reader.endObject();if(!fields.keySet().containsAll(Set.of("avatar_id","model","variant","skin")) || reader.peek()!=JsonToken.END_DOCUMENT)throw new IllegalArgumentException("required fields/trailing JSON");
            if(!fields.get("avatar_id").matches("[a-z0-9_.-]{1,64}") || !fields.get("model").equals("minecraft_humanoid"))throw new IllegalArgumentException("avatar/model identifier");
            var variant=switch(fields.get("variant")){case "alex_slim" -> Variant.ALEX_SLIM;case "steve_classic" -> Variant.STEVE_CLASSIC;default -> throw new IllegalArgumentException("unknown avatar variant");};
            String skin=fields.get("skin");if(skin.isBlank() || skin.length()>256)throw new IllegalArgumentException("skin path");String skeleton=fields.getOrDefault("skeleton","minecraft_avatar_v1");if(!skeleton.equals("minecraft_avatar_v1"))throw new IllegalArgumentException("unknown skeleton");return new Config(fields.get("avatar_id"),fields.get("model"),variant,skin,skeleton);
        }catch(IOException ex){throw new IllegalArgumentException("invalid avatar JSON",ex);}
    }
    public static void validatePng(byte[] bytes)throws IOException {
        if(bytes.length<33 || bytes.length>MAX_PNG || !Arrays.equals(Arrays.copyOf(bytes,8),new byte[]{(byte)137,80,78,71,13,10,26,10}) || bytes[12]!='I' || bytes[13]!='H' || bytes[14]!='D' || bytes[15]!='R')throw new IOException("invalid PNG signature/size");
        var header=java.nio.ByteBuffer.wrap(bytes);if(header.getInt(16)!=64 || header.getInt(20)!=64)throw new IOException("only 64x64 PNG skins supported");
        var image=ImageIO.read(new ByteArrayInputStream(bytes));if(image==null || image.getWidth()!=64 || image.getHeight()!=64)throw new IOException("PNG decode failed");
    }
}
