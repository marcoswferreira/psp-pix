using Asp.Versioning;

namespace MiniPspPix.API.Extensions;

public static class AppExtensions
{
    public static IServiceCollection AddApiVersioningExtensions(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddMvc()
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }

    public static IServiceCollection AddSwaggerExtensions(this IServiceCollection services)
    {
        services.AddOpenApi();
        return services;
    }
}
