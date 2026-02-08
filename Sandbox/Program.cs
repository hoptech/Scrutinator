using Sandbox.Services;
using Scrutinator.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<ISingletonDependency, SingletonDependency>();
builder.Services.AddScoped<IScopedDependency, ScopedDependency>();
builder.Services.AddTransient<ITransientDependency, TransientDependency>();
builder.Services.AddScrutinator();

builder.Services.AddHostedService<TestHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseScrutinator(opts => {
    opts.OpenDashboardAutomatically = true;
    opts.IncludeSystemServices = false;
});

app.MapGet("/sandbox", () => "Hello! Scrutinator is watching.");

app.Run();
