namespace ConverterPoc.Validation;

public record ValidationResult(bool IsValid, string Reason)
{
    public static ValidationResult Ok() => new(true, "OK");
    public static ValidationResult Fail(string reason) => new(false, reason);
}

public static class OutputValidator
{
    private const int MinimumBytes = 100;

    private static readonly Dictionary<string, byte[]> MagicBytes = new()
    {
        [".pdf"] = "%PDF"u8.ToArray(),
        [".png"] = [0x89, 0x50, 0x4E, 0x47],
        [".jpg"] = [0xFF, 0xD8, 0xFF],
        [".docx"] = [0x50, 0x4B, 0x03, 0x04],   // zip container
        [".zip"] = [0x50, 0x4B, 0x03, 0x04],
    };

    public static ValidationResult Validate(string path)
    {
        if (!File.Exists(path))
            return ValidationResult.Fail("File was not written to disk");

        var info = new FileInfo(path);

        if (info.Length == 0)
            return ValidationResult.Fail("File is zero bytes");

        if (info.Length < MinimumBytes)
            return ValidationResult.Fail($"Suspiciously small: {info.Length} bytes");

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!MagicBytes.TryGetValue(ext, out var expected))
            return ValidationResult.Ok();   // unknown type — size check only

        var actual = new byte[expected.Length];
        using (var fs = File.OpenRead(path))
        {
            if (fs.Read(actual, 0, expected.Length) < expected.Length)
                return ValidationResult.Fail("File too short to read header");
        }

        // An HTML error page saved with a .pdf extension is caught here.
        return actual.SequenceEqual(expected)
            ? ValidationResult.Ok()
            : ValidationResult.Fail($"Wrong signature for {ext} — got {Convert.ToHexString(actual)}");
    }
}