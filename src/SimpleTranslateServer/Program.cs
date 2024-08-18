using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing.Constraints;

#if !AOT_PUBLISHING

using Microsoft.OpenApi.Models;
using SimpleTranslateServer;

#endif

var builder = WebApplication.CreateSlimBuilder(args);

ITranslator translator;

#region ali

var aliAccessKeyId = builder.Configuration.GetValue<string>("AliAccessKeyId");
var aliAccessKeySecret = builder.Configuration.GetValue<string>("AliAccessKeySecret");

translator = new AlibabaCloudTranslator(aliAccessKeyId!, aliAccessKeySecret!);

#endregion ali

#if !AOT_PUBLISHING

builder.Services.Configure<RouteOptions>(options => options.SetParameterPolicy<RegexInlineRouteConstraint>("regex"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

#endif

var app = builder.Build();

#if !AOT_PUBLISHING

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

#endif

var todosApi = app.MapGroup("/");
todosApi.MapPost("/", async (HttpContext context, [FromQuery] string from, [FromQuery] string to, CancellationToken cancellationToken) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var content = await reader.ReadToEndAsync(cancellationToken);

    return await translator.TranslateAsync(content, from, to, cancellationToken);
})
#if !AOT_PUBLISHING

.WithOpenApi(operation =>
{
    operation.RequestBody = new OpenApiRequestBody
    {
        Content =
        {
            ["text/plain"] = new OpenApiMediaType
            {
                Schema = new OpenApiSchema
                {
                    Type = "string",
                }
            }
        }
    };
    return operation;
})

#endif
;

app.Run();
