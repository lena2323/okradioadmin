using FluentFTP;
using Microsoft.AspNetCore.Http.Features;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<FormOptions>(o => { o.MultipartBodyLengthLimit = 500_000_000; });
builder.Services.AddCors();
var app = builder.Build();

app.UseCors(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

async Task<(string Host, string User, string Pass)?> GetFtpConfig(string slug)
{
    try {
        using var http = new HttpClient();
        string url = $"https://wnfvyylfinrghsrmhmlp.supabase.co/rest/v1/cafes?slug=eq.{slug}&select=ftp_host,ftp_user,ftp_pass";
        string key = "sb_publishable_l2-lCystkLYT6wzFY1D4Kg_X9KOU_vB";

        http.DefaultRequestHeaders.Add("apikey", key);
        http.DefaultRequestHeaders.Add("Authorization", "Bearer " + key);

        var content = await http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        if (root.GetArrayLength() == 0) return null;

        return (root[0].GetProperty("ftp_host").GetString() ?? "", 
                root[0].GetProperty("ftp_user").GetString() ?? "", 
                root[0].GetProperty("ftp_pass").GetString() ?? "");
    }
    catch { return null; }
}

app.MapPost("/upload/{cafe}", async (string cafe, string folder, HttpRequest request) =>
{
    var cfg = await GetFtpConfig(cafe);
    if (cfg == null) return Results.BadRequest("Kafić nije nađen.");

    try {
        var form = await request.ReadFormAsync();
        var files = form.Files; 
        
        if (files == null || files.Count == 0) return Results.BadRequest("Nema fajlova za upload.");

        using var ftp = new FtpClient(cfg.Value.Host, cfg.Value.User, cfg.Value.Pass);
        ftp.ValidateCertificate += (control, e) => { e.Accept = true; };
        await ftp.ConnectAsync();

        int uploadedCount = 0;

        foreach (var file in files)
        {
            string remotePath = $"/media/Server_1/{folder}/{file.FileName}";
            
            using var fileStream = file.OpenReadStream();
            var status = await ftp.UploadStreamAsync(fileStream, remotePath, FtpRemoteExists.Overwrite);
            
            if (status == FtpStatus.Success)
            {
                uploadedCount++;
            }
        }

        await ftp.DisconnectAsync();

        return Results.Ok(new { 
            message = "Batch upload završen", 
            total = files.Count, 
            uploaded = uploadedCount 
        });
    } catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapGet("/playlist/{cafe}", async (string cafe, string folder) =>
{
    var cfg = await GetFtpConfig(cafe);
    if (cfg == null) return Results.BadRequest("Kafić nije nađen.");

    try {
        using var ftp = new FtpClient(cfg.Value.Host, cfg.Value.User, cfg.Value.Pass);
        ftp.ValidateCertificate += (control, e) => { e.Accept = true; };
        await ftp.ConnectAsync();
        
        var list = await ftp.GetListingAsync($"/media/Server_1/{folder}/");
        var songs = list.Where(f => f.Type == FtpObjectType.File)
                        .Select(f => new { name = f.Name, size = f.Size }).ToList();
        
        await ftp.DisconnectAsync();
        return Results.Json(songs);
    } catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.Run();