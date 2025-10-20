using API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddSingleton<UserService>();
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline
app.MapGet("/", () => "Multi-Project API is running");

app.MapGet("/users/{id:int}", (int id, UserService userService) =>
{
    return Results.Ok(userService.GetUser(id));
});

app.MapControllers();

app.Run();
