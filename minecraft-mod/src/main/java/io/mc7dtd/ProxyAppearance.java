package io.mc7dtd;

import com.google.gson.*;

/** Local derived intent, not a model or network component. */
public record ProxyAppearance(String heldMarker,String headMarker,String heldItemId,String headItemId) {
    public static final ProxyAppearance EMPTY=new ProxyAppearance(null,null,null,null);
    public JsonObject toJson(){var result=new JsonObject();result.add("held_marker",heldMarker==null ? JsonNull.INSTANCE : new JsonPrimitive(heldMarker));result.add("head_marker",headMarker==null ? JsonNull.INSTANCE : new JsonPrimitive(headMarker));return result;}
}
