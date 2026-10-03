package io.mc7dtd;

import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.WebSocket;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
import java.util.concurrent.*;
import java.util.function.Consumer;

/** Shared by the Fabric entrypoint and the headless test harness. */
public final class BridgeClient implements AutoCloseable {
    private final URI uri;
    private final Consumer<String> log;
    private volatile boolean stopped;
    private volatile WebSocket socket;
    private Thread worker;
    public BridgeClient(Path configPath, Consumer<String> logger) throws Exception {
        log = logger;
        var config = JsonParser.parseString(Files.readString(configPath)).getAsJsonObject();
        String host = config.get("host").getAsString();
        int port = config.get("port").getAsInt();
        if (!host.equals("localhost") || port < 1024 || port > 65535)
            throw new IllegalArgumentException("network.json requires localhost and port 1024..65535");
        uri = URI.create("ws://localhost:" + port + "/ws");
    }
    public synchronized void start() {
        if (worker != null) return;
        worker = new Thread(this::run, "MC7DTD-websocket");
        worker.setDaemon(true);
        worker.start();
    }
    private void run() {
        var http = HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(5))
            .proxy(new java.net.ProxySelector() {
                public java.util.List<java.net.Proxy> select(URI uri) { return java.util.List.of(java.net.Proxy.NO_PROXY); }
                public void connectFailed(URI uri, java.net.SocketAddress address, java.io.IOException error) { }
            }).build();
        try {
            while (!stopped) {
                try {
                    var listener = new Listener();
                    socket = http.newWebSocketBuilder().connectTimeout(Duration.ofSeconds(5))
                        .buildAsync(uri, listener).get(6, TimeUnit.SECONDS);
                    send("{\"type\":\"minecraft_connect\",\"client\":\"minecraft\"}");
                    boolean welcomed = false;
                    while (!stopped) {
                        String raw = welcomed ? listener.messages.take() : listener.messages.poll(10, TimeUnit.SECONDS);
                        if (raw == null) throw new TimeoutException("Bridge welcome timeout");
                        if (raw.equals("\0")) break;
                        var message = JsonParser.parseString(raw).getAsJsonObject();
                        String type = message.get("type").getAsString();
                        if (type.equals("welcome") && message.get("client").getAsString().equals("minecraft")) {
                            welcomed = true; log.accept("Bridge connected"); log.accept("Minecraft connected");
                        } else if (welcomed && type.equals("peer_connected")) {
                            var test = new JsonObject(); test.addProperty("type", "test"); test.addProperty("text", "Hello from Minecraft");
                            send(test.toString());
                        } else if (welcomed && type.equals("test")) {
                            log.accept("Test received from " + message.get("from").getAsString() + ": " + message.get("text").getAsString());
                        } else if (type.equals("error")) log.accept("Bridge error: " + message.get("code").getAsString());
                    }
                    if (!stopped) log.accept("Bridge disconnected; retry in 3 seconds");
                } catch (Exception ex) {
                    if (!stopped) log.accept("Bridge unavailable: " + ex + "; retry in 3 seconds");
                } finally { if (socket != null) socket.abort(); socket = null; }
                if (!stopped) try { Thread.sleep(3000); } catch (InterruptedException ex) { break; }
            }
        } finally { http.shutdownNow(); }
    }
    private void send(String text) throws Exception { socket.sendText(text, true).get(5, TimeUnit.SECONDS); }
    @Override public void close() {
        stopped = true;
        var current = socket; if (current != null) current.abort();
        if (worker != null) worker.interrupt();
    }
    private static final class Listener implements WebSocket.Listener {
        final BlockingQueue<String> messages = new ArrayBlockingQueue<>(32);
        final StringBuilder pending = new StringBuilder();
        @Override public void onOpen(WebSocket ws) { ws.request(1); }
        private void end(WebSocket ws) { ws.abort(); messages.clear(); messages.offer("\0"); }
        @Override public CompletionStage<?> onText(WebSocket ws, CharSequence data, boolean last) {
            if (pending.length() + data.length() > 8192) { end(ws); return null; }
            pending.append(data);
            if (last) {
                if (!messages.offer(pending.toString())) { end(ws); return null; }
                pending.setLength(0);
            }
            ws.request(1); return null;
        }
        @Override public CompletionStage<?> onBinary(WebSocket ws, java.nio.ByteBuffer data, boolean last) { end(ws); return null; }
        @Override public CompletionStage<?> onClose(WebSocket ws, int status, String reason) { messages.offer("\0"); return null; }
        @Override public void onError(WebSocket ws, Throwable error) { end(ws); }
    }
}
