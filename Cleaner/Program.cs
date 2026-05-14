using System;
using Npgsql;

var connStr = "Host=76.13.164.224;Port=5432;Database=HSAcademia;Username=postgres;Password=SoftSport2026;";
using var conn = new NpgsqlConnection(connStr);
conn.Open();

var sqlSelect = @"
    SELECT ""Id"", ""FirstName"", ""LastName"", ""CreatedAt"" 
    FROM ""Students"" 
    WHERE ""FirstName"" ILIKE '%Valentino%' AND ""LastName"" ILIKE '%Rengifo%'
    ORDER BY ""CreatedAt"" ASC;
";

using var cmdSelect = new NpgsqlCommand(sqlSelect, conn);
using var reader = cmdSelect.ExecuteReader();
var ids = new System.Collections.Generic.List<Guid>();
while (reader.Read())
{
    Console.WriteLine($"{reader.GetGuid(0)} - {reader.GetString(1)} {reader.GetString(2)} - {reader.GetDateTime(3)}");
    ids.Add(reader.GetGuid(0));
}
reader.Close();

if (ids.Count > 1)
{
    for (int i = 1; i < ids.Count; i++)
    {
        var id = ids[i];
        Console.WriteLine($"Attempting to delete {id}");
        
        try {
            using var cmd1 = new NpgsqlCommand(@"DELETE FROM ""PaymentInstallments"" WHERE ""PaymentRecordId"" IN (SELECT ""Id"" FROM ""PaymentRecords"" WHERE ""StudentId"" = @id)", conn);
            cmd1.Parameters.AddWithValue("id", id);
            cmd1.ExecuteNonQuery();
        } catch {}

        try {
            using var cmd2 = new NpgsqlCommand(@"DELETE FROM ""payment_installments"" WHERE ""payment_record_id"" IN (SELECT ""id"" FROM ""payment_records"" WHERE ""student_id"" = @id)", conn);
            cmd2.Parameters.AddWithValue("id", id);
            cmd2.ExecuteNonQuery();
        } catch {}

        try {
            using var cmd3 = new NpgsqlCommand(@"DELETE FROM ""PaymentRecords"" WHERE ""StudentId"" = @id", conn);
            cmd3.Parameters.AddWithValue("id", id);
            cmd3.ExecuteNonQuery();
        } catch {}

        try {
            using var cmd4 = new NpgsqlCommand(@"DELETE FROM ""payment_records"" WHERE ""student_id"" = @id", conn);
            cmd4.Parameters.AddWithValue("id", id);
            cmd4.ExecuteNonQuery();
        } catch {}

        try {
            using var cmd5 = new NpgsqlCommand(@"DELETE FROM ""StudentHealthRecords"" WHERE ""StudentId"" = @id", conn);
            cmd5.Parameters.AddWithValue("id", id);
            cmd5.ExecuteNonQuery();
        } catch {}

        try {
            using var cmd6 = new NpgsqlCommand(@"DELETE FROM ""StudentNutritionRecords"" WHERE ""StudentId"" = @id", conn);
            cmd6.Parameters.AddWithValue("id", id);
            cmd6.ExecuteNonQuery();
        } catch {}

        try {
            var sqlDelete = @"DELETE FROM ""Students"" WHERE ""Id"" = @id;";
            using var cmdDelete = new NpgsqlCommand(sqlDelete, conn);
            cmdDelete.Parameters.AddWithValue("id", id);
            var rows = cmdDelete.ExecuteNonQuery();
            Console.WriteLine($"Deleted duplicate {id}: {rows} rows affected.");
        } catch (Exception ex) {
            Console.WriteLine("Error deleting student: " + ex.Message);
        }
    }
}
else
{
    Console.WriteLine("No duplicates found to delete.");
}
