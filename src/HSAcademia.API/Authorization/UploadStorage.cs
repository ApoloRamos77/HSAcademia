namespace HSAcademia.API.Authorization;

/// <summary>
/// Carpeta raíz de archivos subidos (vouchers, etc.). Configurable con "Uploads:Path".
/// En Docker/Easypanel debe montarse como VOLUMEN persistente; si no, los archivos se pierden en cada despliegue.
/// </summary>
public sealed class UploadStorage
{
    public const long MaxVoucherBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedVoucherExtensions = new() { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };

    public string RootPath { get; }

    public UploadStorage(IConfiguration config, IWebHostEnvironment env)
    {
        var configured = config["Uploads:Path"];
        RootPath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "wwwroot", "uploads")
            : configured;
        Directory.CreateDirectory(RootPath);
    }

    /// <summary>
    /// Valida (tamaño, extensión y firma real del archivo) y guarda un comprobante.
    /// Devuelve (url, null) si se guardó, o (null, mensaje de error para el usuario).
    /// </summary>
    public async Task<(string? Url, string? Error)> SaveVoucherAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0) return (null, "No se proporcionó ningún archivo.");
        if (file.Length > MaxVoucherBytes) return (null, "El archivo supera el tamaño máximo de 5 MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedVoucherExtensions.Contains(ext)) return (null, "Formato no permitido. Usa JPG, PNG, WEBP o PDF.");
        if (!await HasValidSignatureAsync(file, ext))
            return (null, "El contenido del archivo no corresponde a una imagen o PDF válido.");

        var folder = Path.Combine(RootPath, "vouchers");
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid()}{ext}";
        await using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.CreateNew))
            await file.CopyToAsync(stream);

        return ($"/uploads/vouchers/{fileName}", null);
    }

    /// <summary>Verifica los "magic bytes" para no confiar solo en la extensión.</summary>
    private static async Task<bool> HasValidSignatureAsync(IFormFile file, string ext)
    {
        var h = new byte[12];
        await using var s = file.OpenReadStream();
        var read = await s.ReadAsync(h.AsMemory(0, h.Length));
        if (read < 4) return false;

        return ext switch
        {
            ".jpg" or ".jpeg" => h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
            ".png"  => h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47,
            ".pdf"  => h[0] == 0x25 && h[1] == 0x50 && h[2] == 0x44 && h[3] == 0x46,
            ".webp" => read >= 12 && h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46
                                  && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50,
            _ => false
        };
    }
}
