using FluentFTP;
using FluentFTP.Helpers;
using Microsoft.AspNetCore.Http.Features;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<FormOptions>(o => { o.MultipartBodyLengthLimit = 500_000_000; });
builder.Services.AddCors();
var app = builder.Build();

app.UseDeveloperExceptionPage(); 
app.UseCors(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

string ftpHost = "usa10.fastcast4u.com";
string ftpUser = "lena2323";
string ftpPass = "av6VDA8TsZ4MXPR";

app.MapPost("/upload", async (HttpRequest request) => {
    try {
        var form = await request.ReadFormAsync();
        var files = form.Files; 
        var folder = request.Query["folder"].ToString(); 

        if (files.Count == 0) return Results.BadRequest("Nema fajlova za upload.");
        if (string.IsNullOrEmpty(folder)) return Results.BadRequest("Folder nije specificiran.");

        using var ftp = new FtpClient(ftpHost, ftpUser, ftpPass);
        ftp.DataConnectionType = FtpDataConnectionType.AutoPassive;
        ftp.ValidateCertificate += (control, e) => { e.Accept = true; };
        await ftp.ConnectAsync();

        foreach (var file in files) {
            var remotePath = $"/media/Server_1/{folder}/{file.FileName}";
            
            using var fileStream = file.OpenReadStream();
            await ftp.UploadStreamAsync(fileStream, remotePath, FtpRemoteExists.Overwrite);
        }

        await ftp.DisconnectAsync();
        return Results.Ok($"{files.Count} fajlova uspešno poslato u folder {folder}!");
    } catch (Exception ex) { 
        return Results.Problem(ex.ToString()); 
    }
});

app.MapGet("/playlist", async (HttpContext context) => { 
    try {
        string folder = context.Request.Query["folder"].ToString();
        if (string.IsNullOrEmpty(folder)) folder = "domace";

        using var ftp = new FtpClient(ftpHost, ftpUser, ftpPass);
        ftp.DataConnectionType = FtpDataConnectionType.AutoPassive;
        ftp.ValidateCertificate += (control, e) => { e.Accept = true; };
        await ftp.ConnectAsync();

        var remotePath = $"/media/Server_1/{folder}/";
        var list = await ftp.GetListingAsync(remotePath);
        
        var songs = list.Where(f => f.Type == FtpObjectType.File)
                        .Select(f => new { name = f.Name, size = f.Size, is_file = true })
                        .ToList();
                        
        await ftp.DisconnectAsync();
        return Results.Json(songs);
    } catch (Exception ex) { 
        return Results.Problem(ex.Message); 
    }
});
app.MapGet("/", () => "API Online - Samo Upload i Pregled");
app.Run();