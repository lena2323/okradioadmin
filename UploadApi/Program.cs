using FluentFTP;
using FluentFTP.Helpers;
using Microsoft.AspNetCore.Http.Features;
using System.Text;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(o => {
    o.MultipartBodyLengthLimit = 500_000_000; 
});

builder.Services.AddCors();
var app = builder.Build();

app.UseDeveloperExceptionPage(); 
app.UseCors(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

// --- FTP PODACI (Zajednički za obe rute) ---
string ftpHost = "usa10.fastcast4u.com";
string ftpUser = "lena2323";
string ftpPass = "av6VDA8TsZ4MXPR";

// --- RUTA ZA UPLOAD ---
app.MapPost("/upload", async (HttpRequest request) =>
{
    try
    {
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
    }
    catch (Exception ex) { return Results.Problem(ex.ToString()); }
});

// --- RUTA ZA LISTU PESAMA PREKO FTP-a ---
app.MapGet("/playlist", async () =>
{
    try 
    {
        using var ftp = new FtpClient(ftpHost, ftpUser, ftpPass);
        ftp.DataConnectionType = FtpDataConnectionType.AutoPassive;
        ftp.ValidateCertificate += (control, e) => { e.Accept = true; };

        await ftp.ConnectAsync();

        // Čitamo listu fajlova iz foldera Server_1
        var list = await ftp.GetListingAsync("/media/Server_1/");
        
        // Pretvaramo FTP listu u jednostavan JSON format koji tvoj JS razume
        var songs = list
            .Where(f => f.Type == FtpObjectType.File)
            .Select(f => new {
                name = f.Name,
                size = f.Size,
                is_file = true
            })
            .ToList();

        await ftp.DisconnectAsync();

        return Results.Json(songs);
    }
    catch (Exception ex)
    {
        return Results.Problem("FTP LIST GREŠKA: " + ex.Message);
    }
});
// --- RUTA ZA BRISANJE PESME (DELETE) ---
app.MapDelete("/playlist/{fileName}", async (string fileName) =>
{
    try 
    {
        using var ftp = new FtpClient(ftpHost, ftpUser, ftpPass);
        ftp.DataConnectionType = FtpDataConnectionType.AutoPassive;
        ftp.ValidateCertificate += (control, e) => { e.Accept = true; };

        await ftp.ConnectAsync();

        string remotePath = "/media/Server_1/" + fileName;

        // Proveravamo da li fajl postoji pre brisanja
        if (await ftp.FileExistsAsync(remotePath))
        {
            await ftp.DeleteFileAsync(remotePath);
            await ftp.DisconnectAsync();
            return Results.Ok($"Fajl {fileName} je obrisan.");
        }

        await ftp.DisconnectAsync();
        return Results.NotFound("Fajl nije pronađen na serveru.");
    }
    catch (Exception ex)
    {
        return Results.Problem("FTP DELETE GREŠKA: " + ex.Message);
    }
});
app.MapGet("/", () => "API Online - Čitanje liste ide preko FTP-a!");
app.Run();