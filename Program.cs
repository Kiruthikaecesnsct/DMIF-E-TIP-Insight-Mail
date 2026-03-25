using InsightMail.API.Middleware;
using InsightMail.API.Services;
using InsightMail.Repositories;
using InsightMail.Services;
using InsightMail.Services.InsightMail.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Replace your existing AddCors with this:
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.WithOrigins(
                      "https://localhost:7097",  // your Blazor UI https port
                      "http://localhost:5097",
                      "https://dmif-e-tip-insight-mail-7vwkbgpzl-kiruthikaecesnscts-projects.vercel.app/"// your Blazor UI http port
                  )
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // required for SignalR
        });
});

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<MongoDbService>();

builder.Services.AddScoped<IEmailParserService, EmailParserService>();
builder.Services.AddScoped<IEmailRepository, EmailRepository>();

// ⭐ ADD THIS
builder.Services.AddScoped<IActionItemRepository, ActionItemRepository>();

builder.Services.AddHttpClient<IGeminiClientService, GeminiClientService>();
builder.Services.AddHttpClient<IEmbeddingService, GeminiEmbeddingService>();
builder.Services.AddScoped<IClassifierService, ClassifierService>();
builder.Services.AddScoped<IActionExtractorService, ActionExtractorService>();
builder.Services.AddScoped<EmailSearchService>();
builder.Services.AddScoped<EmailRAGService>();
builder.Services.AddScoped<ReplyContextService>();
builder.Services.AddScoped<ReplyPromptBuilder>();
builder.Services.AddScoped<ReplyGenerationService>();
builder.Services.AddScoped<ReplyValidationService>();
builder.Services.AddScoped<ReplyAnalyticsService>();
builder.Services.AddScoped<ThreadChunker>();
builder.Services.AddScoped<ThreadSummarizerAgent>();
builder.Services.AddScoped<SummaryAnalyticsService>();
builder.Services.AddScoped<EmailThreadSplitter>();
builder.Services.AddScoped<IThreadSummaryRepository, ThreadSummaryRepository>();
builder.Services.AddSignalR();
builder.Services.AddScoped<IDraftAssistantAgent, DraftAssistantAgent>();
builder.Services.AddSingleton<EmailTemplateService>();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});
var app = builder.Build();
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://*:{port}");
app.UseCors("AllowAll");                                          // ← must be first
app.MapHub<InsightMail.API.Hubs.EmailHub>("/emailhub");          // ← after CORS
app.UseMiddleware<ExceptionHandlingMiddleware>();
// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();
//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "🚀 InsightMail API is LIVE on Render!");
app.Run();
