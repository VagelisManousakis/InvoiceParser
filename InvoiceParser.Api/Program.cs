var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddHttpClient();

builder.Services.AddSingleton<InvoiceParser.Api.Services.IJobStore, InvoiceParser.Api.Services.InMemoryJobStore>();
builder.Services.AddSingleton<InvoiceParser.Api.Services.IN8nClient, InvoiceParser.Api.Services.N8nClient>();
builder.Services.AddSingleton<InvoiceParser.Api.Services.IFetchHtmlService, InvoiceParser.Api.Services.FetchHtmlService>();
builder.Services.AddSingleton<InvoiceParser.Api.Services.IInvoiceParserService, InvoiceParser.Api.Services.InvoiceParserService>();
builder.Services.AddSingleton<InvoiceParser.Api.Services.IEntersSoftIframeFetcher, InvoiceParser.Api.Services.EntersSoftIframeFetcher>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
//builder.WebHost.UseUrls("http://0.0.0.0:7254");
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseCors();

app.UseMiddleware<InvoiceParser.Api.Middleware.RequestContextMiddleware>();

app.MapControllers();

app.Run();
