using System;
using Npgsql;

class Program
{
    static void Main()
    {
        string connStr = "Host=76.13.164.224;Port=5432;Database=HSAcademia;Username=postgres;Password=SoftSport2026;";
        using var conn = new NpgsqlConnection(connStr);
        conn.Open();

        using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM academies", conn);
        long count = (long)cmd.ExecuteScalar();
        Console.WriteLine($"Academies count: {count}");

        using var cmd2 = new NpgsqlCommand("SELECT COUNT(*) FROM users", conn);
        long count2 = (long)cmd2.ExecuteScalar();
        Console.WriteLine($"Users count: {count2}");
    }
}
