using FluentFTP;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Allow large uploads
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 500_000_000; // 500 MB
});
builder.Services.AddCors();
var app = builder.Build();


app.UseCors(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

app.MapPost("/upload", async (HttpRequest request) =>
{
    try
    {
        var form = await request.ReadFormAsync();
        var file = form.Files.FirstOrDefault();
        if (file == null) return Results.BadRequest("Nema fajla.");

        // Save temp file
        var tempFile = Path.GetTempFileName();
        using (var fs = File.Create(tempFile))
        {
            await file.CopyToAsync(fs);
        }

        var ftpHost = "usa10.fastcast4u.com";
        var ftpUser = "lena2323";
        var ftpPass = "av6VDA8TsZ4MXPR"; // replace with your real password
        var remotePath = "/media/Server_1/" + file.FileName;

        using var ftp = new FtpClient(ftpHost, ftpUser, ftpPass);
        ftp.ValidateCertificate += (control, e) => { e.Accept = true; };
        ftp.Connect(); // sync connect

        // Upload using file path
        var status = ftp.UploadFile(tempFile, remotePath, FluentFTP.FtpRemoteExists.Overwrite);

        // Delete temp file
        File.Delete(tempFile);

        if (status == FluentFTP.FtpStatus.Success)
            return Results.Ok("Fajl uspešno poslat!");
        else
            return Results.Problem("FTP upload nije uspeo!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.Run();