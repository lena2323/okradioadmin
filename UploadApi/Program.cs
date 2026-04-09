using FluentFTP;
using FluentFTP.Helpers; // Obavezno za ConnectAsync i UploadStreamAsync u v39
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(o => {
    o.MultipartBodyLengthLimit = 500_000_000; 
});
builder.Services.AddCors();
var app = builder.Build();

// OVO ĆE TI POKAZATI TAČNU GREŠKU UMESTO "500"
app.UseDeveloperExceptionPage(); 

app.UseCors(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

app.MapPost("/upload", async (HttpRequest request) =>
{
    try
    {
        var form = await request.ReadFormAsync();
        var file = form.Files.FirstOrDefault();
        if (file == null) return Results.BadRequest("Nema fajla.");

        var ftpHost = "usa10.fastcast4u.com";
        var ftpUser = "lena2323";
        var ftpPass = "av6VDA8TsZ4MXPR";
        var remotePath = "/media/Server_1/" + file.FileName;

        using var ftp = new FtpClient(ftpHost, ftpUser, ftpPass);

        // FIX ZA VERZIJU 39.1.0 (Podešavanja direktno na klijentu)
        ftp.ConnectTimeout = 10000; // 10 sekundi
        ftp.ReadTimeout = 10000;
        ftp.DataConnectionType = FtpDataConnectionType.AutoPassive;
        ftp.ValidateCertificate += (control, e) => { e.Accept = true; };

        Console.WriteLine("Povezujem se na FTP...");
        await ftp.ConnectAsync();

        using var fileStream = file.OpenReadStream();
        
        // Slanje fajla
        var status = await ftp.UploadStreamAsync(fileStream, remotePath, FtpRemoteExists.Overwrite);

        await ftp.DisconnectAsync();

        if (status == FtpStatus.Success)
            return Results.Ok("Fajl uspešno poslat!");
        else
            return Results.Problem($"FTP status: {status}");
    }
    catch (Exception ex)
    {
        // Ova poruka će se pojaviti u browseru zahvaljujući UseDeveloperExceptionPage
        return Results.Problem("DEBUG GREŠKA: " + ex.ToString());
    }
});
app.MapGet("/", () => "API radi!!!");
app.MapGet("/upload", () => "Upload endpoint koristi POST");
app.Run();