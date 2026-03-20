using InsightMail.API.Middleware;
using InsightMail.API.Services;
using InsightMail.Repositories;
using InsightMail.Services;
using InsightMail.Services.InsightMail.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
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
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});
var app = builder.Build();


app.UseCors("AllowAll");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
