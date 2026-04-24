using System;
using System.IO;
using Npgsql;

class Program
{
    static void Main()
    {
        string connStr = "Host=76.13.164.224;Port=5432;Database=HSAcademia;Username=postgres;Password=SoftSport2026;";
        using var conn = new NpgsqlConnection(connStr);
        conn.Open();

        string newHash = BCrypt.Net.BCrypt.HashPassword("Admin2026!");
        using var cmd = new NpgsqlCommand($"UPDATE users SET password_hash = '{newHash}' WHERE email = 'superadmin@adhsoft.com'", conn);
        int rows = cmd.ExecuteNonQuery();
        Console.WriteLine($"SuperAdmin Password updated. Rows affected: {rows}");
    }
}
