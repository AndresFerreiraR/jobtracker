using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace JobTracker.Api.Infrastructure.Swagger;

internal sealed class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var fileParams = context.MethodInfo.GetParameters()
            .Where(p => p.ParameterType == typeof(IFormFile)
                        || typeof(IFormFile).IsAssignableFrom(p.ParameterType)
                        || p.ParameterType.GetProperties().Any(x => x.PropertyType == typeof(IFormFile)))
            .ToList();

        if (fileParams.Count == 0) return;

        var schema = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>(),
            Required = new HashSet<string>()
        };

        foreach (var p in fileParams)
        {
            if (p.ParameterType == typeof(IFormFile))
            {
                schema.Properties[p.Name!] = new OpenApiSchema { Type = "string", Format = "binary" };
                schema.Required.Add(p.Name!);
            }
            else
            {
                foreach (var prop in p.ParameterType.GetProperties())
                {
                    var name = char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..];
                    if (prop.PropertyType == typeof(IFormFile))
                    {
                        schema.Properties[name] = new OpenApiSchema { Type = "string", Format = "binary" };
                        schema.Required.Add(name);
                    }
                    else if (prop.PropertyType == typeof(DateTimeOffset) || prop.PropertyType == typeof(DateTimeOffset?))
                    {
                        schema.Properties[name] = new OpenApiSchema { Type = "string", Format = "date-time", Nullable = true };
                    }
                    else
                    {
                        schema.Properties[name] = new OpenApiSchema { Type = "string", Nullable = true };
                    }
                }
            }
        }

        operation.Parameters = operation.Parameters
            .Where(x => x.In != ParameterLocation.Query && x.In != null)
            .ToList();

        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType { Schema = schema }
            }
        };
    }
}
