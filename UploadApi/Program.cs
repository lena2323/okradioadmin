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
        var file = form.Files.FirstOrDefault();
        if (file == null) return Results.BadRequest("Nema fajla.");
        var remotePath = "/media/Server_1/" + file.FileName;
        using var ftp = new FtpClient(ftpHost, ftpUser, ftpPass);
        ftp.DataConnectionType = FtpDataConnectionType.AutoPassive;
        ftp.ValidateCertificate += (control, e) => { e.Accept = true; };
        await ftp.ConnectAsync();
        using var fileStream = file.OpenReadStream();
        var status = await ftp.UploadStreamAsync(fileStream, remotePath, FtpRemoteExists.Overwrite);
        await ftp.DisconnectAsync();
        return status == FtpStatus.Success ? Results.Ok("Fajl poslat!") : Results.Problem("FTP Greška.");
    } catch (Exception ex) { return Results.Problem(ex.ToString()); }
});

app.MapGet("/playlist", async () => {
    try {
        using var ftp = new FtpClient(ftpHost, ftpUser, ftpPass);
        ftp.DataConnectionType = FtpDataConnectionType.AutoPassive;
        ftp.ValidateCertificate += (control, e) => { e.Accept = true; };
        await ftp.ConnectAsync();
        var list = await ftp.GetListingAsync("/media/Server_1/");
        var songs = list.Where(f => f.Type == FtpObjectType.File)
                        .Select(f => new { name = f.Name, size = f.Size, is_file = true }).ToList();
        await ftp.DisconnectAsync();
        return Results.Json(songs);
    } catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapGet("/", () => "API Online - Samo Upload i Pregled");
app.Run();