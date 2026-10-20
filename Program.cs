using Microsoft.AspNetCore.Http.Features;
using SkyNet;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
const long UploadLimit = 110L * 1024 * 1024;
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = UploadLimit);
builder.Services.Configure<IISServerOptions>(o => o.MaxRequestBodySize = UploadLimit);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = UploadLimit);

var app = builder.Build();
FlowerShop.Core.Local.Accessor = app.Services.GetRequiredService<IHttpContextAccessor>();
app.UseMiddleware<IHandler>();

app.UseStaticHttpCurrent();

app.Run();
