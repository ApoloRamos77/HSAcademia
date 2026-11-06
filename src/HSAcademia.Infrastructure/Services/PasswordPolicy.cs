using System.Security.Cryptography;

namespace HSAcademia.Infrastructure.Services;

/// <summary>Generación de contraseñas temporales seguras y reglas mínimas de contraseña.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    // Sin caracteres ambiguos (0/O, 1/l/I) para que sea fácil dictarla o escribirla desde el celular.
    private const string Upper  = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower  = "abcdefghjkmnpqrstuvwxyz";
    private const string Digits = "23456789";

    /// <summary>Contraseñas por defecto usadas históricamente; siempre obligan a cambiarla.</summary>
    private static readonly HashSet<string> Weak = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456", "12345", "1234567", "12345678", "123456789", "password", "contraseña", "admin"
    };

    public static bool IsWeak(string password) => Weak.Contains(password);

    /// <summary>Genera una contraseña temporal de 10 caracteres (letras y números) con RNG criptográfico.</summary>
    public static string GenerateTemporary(int length = 10)
    {
        var all = Upper + Lower + Digits;
        var chars = new char[length];
        // Garantizar al menos una mayúscula, una minúscula y un dígito.
        chars[0] = Upper[RandomNumberGenerator.GetInt32(Upper.Length)];
        chars[1] = Lower[RandomNumberGenerator.GetInt32(Lower.Length)];
        chars[2] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        for (var i = 3; i < length; i++)
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        // Mezclar (Fisher-Yates)
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }

    /// <summary>Devuelve un mensaje de error si la contraseña no cumple la política; null si es válida.</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
            return $"La contraseña debe tener al menos {MinLength} caracteres.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "La contraseña debe combinar letras y números.";
        if (IsWeak(password))
            return "La contraseña es demasiado común. Elige otra.";
        return null;
    }
}
