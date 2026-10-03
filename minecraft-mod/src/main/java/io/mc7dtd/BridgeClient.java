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
    private volatile WebSocket positionSession;
    private volatile long entityEpoch;
    public long entityEpoch() { return positionSession == null ? 0 : entityEpoch; }
    // At most one pending sample: slow sockets cannot accumulate old positions.
    private final ThreadPoolExecutor positionSender = new ThreadPoolExecutor(1, 1, 0, TimeUnit.SECONDS,
        new ArrayBlockingQueue<>(1), task -> {
            var thread = new Thread(task, "MC7DTD-position-send"); thread.setDaemon(true); return thread;
        }, new ThreadPoolExecutor.DiscardOldestPolicy());
    private Thread worker;
    private final ThreadPoolExecutor entityTestSender = new ThreadPoolExecutor(1, 1, 0, TimeUnit.SECONDS,
        new ArrayBlockingQueue<>(8), task -> {
            var thread = new Thread(task, "MC7DTD-entity-test-send"); thread.setDaemon(true); return thread;
        }, new ThreadPoolExecutor.AbortPolicy());
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
                            entityEpoch++; positionSession = socket;
                        } else if (welcomed && type.equals("peer_connected")) {
                            if (message.get("client").getAsString().equals("7dtd")) entityEpoch++;
                            var test = new JsonObject(); test.addProperty("type", "test"); test.addProperty("text", "Hello from Minecraft");
                            send(test.toString());
                        } else if (welcomed && type.equals("test")) {
                            if (message.get("from").getAsString().equals("7dtd") && message.get("text").getAsString().equals("MC7DTD player proxy resync")) entityEpoch++;
                            log.accept("Test received from " + message.get("from").getAsString() + ": " + message.get("text").getAsString());
                        } else if (type.equals("error")) log.accept("Bridge error: " + message.get("code").getAsString());
                    }
                    if (!stopped) log.accept("Bridge disconnected; retry in 3 seconds");
                } catch (Exception ex) {
                    if (!stopped) log.accept("Bridge unavailable: " + ex + "; retry in 3 seconds");
                } finally { positionSession = null; positionSender.getQueue().clear(); if (socket != null) socket.abort(); socket = null; }
                if (!stopped) try { Thread.sleep(3000); } catch (InterruptedException ex) { break; }
            }
        } finally { http.shutdownNow(); }
    }
    private synchronized void send(String text) throws Exception { socket.sendText(text, true).get(5, TimeUnit.SECONDS); }
    public void publishPlayerPosition(double x, double y, double z) {
        var expectedSession = positionSession;
        if (stopped || expectedSession == null || !Double.isFinite(x) || !Double.isFinite(y) || !Double.isFinite(z)) return;
        try { positionSender.execute(() -> {
            try {
                synchronized (this) {
                    if (stopped || positionSession != expectedSession || socket != expectedSession) return;
                    var message = new JsonObject();
                    message.addProperty("type", "player_position"); message.addProperty("source", "minecraft");
                    message.addProperty("x", x); message.addProperty("y", y); message.addProperty("z", z);
                    send(message.toString());
                }
            } catch (Exception ex) { if (!stopped) log.accept("Player position send failed: " + ex.getMessage()); }
        }); } catch (RejectedExecutionException ex) { if (!stopped) throw ex; }
    }
    // Acceptance-only snapshots captured on the game thread; never block it on network I/O.
    public boolean publishEntityTest(JsonObject message) {
        return publishEntity(message, -1, "Entity test");
    }
    public boolean publishPlayerEntity(JsonObject message, long epoch) {
        return publishEntity(message, epoch, "Player proxy");
    }
    private boolean publishEntity(JsonObject message, long epoch, String label) {
        var expectedSession = positionSession;
        if (stopped || expectedSession == null || (epoch >= 0 && entityEpoch != epoch)) return false;
        String payload = message.toString();
        try {
            entityTestSender.execute(() -> {
                try {
                    synchronized (this) {
                        if (stopped || positionSession != expectedSession || socket != expectedSession || (epoch >= 0 && entityEpoch != epoch)) {
                            log.accept("Entity test skipped: connection changed"); return;
                        }
                        send(payload);
                        log.accept(label + " sent: " + payload);
                    }
                } catch (Exception ex) {
                    if (!stopped) log.accept(label + " send failed: " + ex.getMessage());
                    if (epoch >= 0 && positionSession == expectedSession) expectedSession.abort();
                }
            });
            return true;
        } catch (RejectedExecutionException ex) {
            if (epoch >= 0 && positionSession == expectedSession) expectedSession.abort();
            return false;
        }
    }
    @Override public void close() {
        // Client stopping has already queued despawn; drain it before closing the socket.
        entityTestSender.shutdown();
        try { entityTestSender.awaitTermination(2, TimeUnit.SECONDS); }
        catch (InterruptedException ex) { Thread.currentThread().interrupt(); }
        stopped = true;
        positionSession = null;
        positionSender.shutdownNow();
        entityTestSender.shutdownNow();
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
