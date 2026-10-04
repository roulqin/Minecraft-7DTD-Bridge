using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MC7DTD.Bridge;

public static class InspectorEndpoints
{
    public static void Map(WebApplication app, EntityInspector inspector, SemaphoreSlim gate, MC7DTD.DebugConfig config)
    {
        bool Local(HttpContext c)=>c.Connection.RemoteIpAddress is {} ip && IPAddress.IsLoopback(ip) && !c.Request.Headers.ContainsKey("Origin");
        app.MapPost("/debug/inspect_entity",async (HttpContext context)=>
        {
            if(!Local(context))return Results.StatusCode(403);
            if(!context.Request.HasJsonContentType())return Results.BadRequest(new{error="json_required"});
            try
            {
                // Independent HTTP debug API. No additions to the WebSocket message set.
                var bytes=new byte[8193];int total=0,read;
                while(total<bytes.Length && (read=await context.Request.Body.ReadAsync(bytes.AsMemory(total),context.RequestAborted))>0)total+=read;
                if(total>8192)return Results.BadRequest(new{error="debug_request_too_large"});
                using var document=JsonDocument.Parse(bytes.AsMemory(0,total));var root=document.RootElement;
                if(root.ValueKind!=JsonValueKind.Object)return Results.BadRequest(new{error="invalid_inspect_request"});
                var fields=root.EnumerateObject().ToArray();
                if(fields.Select(p=>p.Name).Distinct().Count()!=fields.Length || fields.Any(p=>p.Name is not ("type" or "entity_id" or "source" or "world_id" or "dimension")))return Results.BadRequest(new{error="invalid_inspect_request"});
                string? Read(string name,bool required=false){if(!root.TryGetProperty(name,out var p)){if(required)throw new InvalidDataException();return null;}if(p.ValueKind!=JsonValueKind.String || p.GetString()!.Length is <1 or >128)throw new InvalidDataException();return p.GetString();}
                if(Read("type",true)!="inspect_entity")return Results.BadRequest(new{error="invalid_inspect_request"});
                var id=Read("entity_id",true)!;var source=Read("source");var world=Read("world_id");var dimension=Read("dimension");
                await gate.WaitAsync(context.RequestAborted);
                try
                {
                    var matches=inspector.Find(id,source,world,dimension);
                    if(matches.Count==0)return Results.NotFound(new{error="entity_not_found",entity_id=id});
                    if(matches.Count>1)return Results.Conflict(new{error="ambiguous_entity_id",candidates=matches.Select(e=>new{source=e["source"]!.GetValue<string>(),world_id=e["world_id"]!.GetValue<string>(),dimension=e["dimension"]!.GetValue<string>()})});
                    if(config.Logging)app.Logger.LogInformation("{Inspector}",EntityInspector.Format(matches[0]));
                    return Results.Json(matches[0]);
                }
                finally{gate.Release();}
            }
            catch(Exception ex)when(ex is JsonException or InvalidDataException){return Results.BadRequest(new{error="invalid_inspect_request"});}
        });
        app.MapGet("/debug/entities",async (HttpContext context)=>
        {
            if(!Local(context))return Results.StatusCode(403);
            await gate.WaitAsync(context.RequestAborted);
            try{return Results.Json(new{entities=inspector.List()});}finally{gate.Release();}
        });
    }
}
