using System.Net.Mime;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using BankApi.Authentication;
using BankApi.DataAccessLayer;
using BankApi.Documentation;
using BankApi.Security;
using FluentValidation.AspNetCore;
using Hellang.Middleware.ProblemDetails;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using OperationResults.AspNetCore;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;
using TinyHelpers.Json.Serialization;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureServices(builder.Services, builder.Configuration, builder.Host);

        var app = builder.Build();
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        Configure(app, provider);

        await app.RunAsync();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration, IHostBuilder host)
    {
        host.UseSerilog((hostingContext, loggerConfiguration) =>
        {
            loggerConfiguration.ReadFrom.Configuration(hostingContext.Configuration);
        });

        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddOperationResult();

        services.AddProblemDetails(options =>
        {
            options.Map<Exception>(ex => new StatusCodeProblemDetails(StatusCodes.Status500InternalServerError));
        });

        services.AddFluentValidationAutoValidation(options =>
        {
            options.DisableDataAnnotationsValidation = true;
        });

        services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
            options.JsonSerializerOptions.Converters.Add(new TimeSpanTicksConverter());
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services.AddApiVersioning(options =>
        {
            options.ReportApiVersions = true;
        });
        services.AddVersionedApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        services.AddEndpointsApiExplorer();

        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
        services.AddSwaggerGen(options =>
        {
            options.OperationFilter<SwaggerDefaultValues>();

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "Insert JWT token with the \"Bearer \" prefix",
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = JwtBearerDefaults.AuthenticationScheme
                            }
                        },
                        Array.Empty<string>()
                    }
                });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            options.IncludeXmlComments(xmlPath);

            options.CustomOperationIds(api => $"{api.ActionDescriptor.RouteValues["controller"]}_{api.ActionDescriptor.RouteValues["action"]}");
            options.UseAllOfToExtendReferenceSchemas();
        })
        .AddFluentValidationRulesToSwagger(options =>
        {
            options.SetNotNullableIfMinLengthGreaterThenZero = true;
        });

        var hashedConnectionString = configuration.GetConnectionString("SqlConnection");
        var connectionString = StringHasher.GetOriginalString(hashedConnectionString);

        services.AddSqlServer<ApplicationDataContext>(connectionString);
        services.AddSqlServer<AuthenticationDataContext>(connectionString);

        services.AddScoped<IDataContext>(services => services.GetRequiredService<ApplicationDataContext>());

        services.AddHealthChecks().AddAsyncCheck("sql", async () =>
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(ex.Message, ex);
            }

            return HealthCheckResult.Healthy();
        });
    }

    private static void Configure(IApplicationBuilder app, IApiVersionDescriptionProvider provider)
    {
        app.UseProblemDetails();

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            foreach (var description in provider.ApiVersionDescriptions)
            {
                options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName);
                options.RoutePrefix = string.Empty;
            }
        });

        app.UseHttpsRedirection();
        app.UseRouting();

        app.UseAuthorization();

        app.UseSerilogRequestLogging(options =>
        {
            options.IncludeQueryInRequestPath = true;
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();

            endpoints.MapHealthChecks("/status",
                    new HealthCheckOptions
                    {
                        ResponseWriter = async (context, report) =>
                        {
                            var result = JsonSerializer.Serialize(
                                new
                                {
                                    Status = report.Status.ToString(),
                                    Details = report.Entries.Select(e => new
                                    {
                                        Service = e.Key,
                                        Status = Enum.GetName(typeof(HealthStatus), e.Value.Status),
                                        Description = e.Value.Description
                                    })
                                });

                            context.Response.ContentType = MediaTypeNames.Application.Json;
                            await context.Response.WriteAsync(result);
                        }
                    });
        });
    }
}