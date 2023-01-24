using OpenTelemetry.Metrics;
using OpenTelemetry;
using System.Diagnostics.Metrics;
using OpenTelemetry.Trace;
using MetricTester;

var meter = new Meter("SampleMeter");
var counter = meter.CreateCounter<int>("requests-received", "Requests", "Simple counter using the Sample Meter");
var forecasts = meter.CreateCounter<int>("forecasts", "degrees", "Sample forecast captured from service call");

var singularity = new SingleRandomThing(314159);
MeterProvider? meterProvider = Sdk.CreateMeterProviderBuilder().AddMeter("SampleMeter").AddPrometheusExporter().Build();
TracerProvider? traceProvider = Sdk.CreateTracerProviderBuilder().AddSource("SampleMeter").AddJaegerExporter().Build();

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenTelemetry().WithTracing(config => { config.AddJaegerExporter(); }).StartWithHost();
builder.Services.AddSingleton(meterProvider);
builder.Services.AddTransient((c) =>
{
    return singularity;
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use((context, next) =>
{
    counter.Add(1, KeyValuePair.Create<string, object?>("path", (
        SourceUrl: context.Request.Path.Value,
        context.Request.Headers,
        ContentType: context.Request.ContentType?.ToString()
    )));
    return next(context);
});

app.UseHttpsRedirection();
app.UseOpenTelemetryPrometheusScrapingEndpoint();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{

    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    forecasts.Add(1, new KeyValuePair<string, object?>(forecast[0].Date.ToShortDateString(), forecast[0]));

    return forecast;



})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.MapGet("/metricstest", () =>
{
    var nbr = singularity.GetRandomThing();
    if(nbr % 12 == 0)
    {
        var request = new HttpClient().GetStringAsync("https://www.google.com/search?q=opentelemetry");
        return request.Result;
    }
    return $"Random number is {nbr}.";

}).WithOpenApi().WithName("MetricsTest");

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
