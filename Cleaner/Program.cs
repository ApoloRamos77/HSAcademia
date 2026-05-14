using System;
using Npgsql;

var connStr = "Host=76.13.164.224;Port=5432;Database=HSAcademia;Username=postgres;Password=SoftSport2026;";
using var conn = new NpgsqlConnection(connStr);
conn.Open();

var targetId = Guid.Parse("7c84117f-1da8-457d-994d-db62d761dcbf");

var sqlUpdate = @"
    UPDATE ""payment_records""
    SET ""amount"" = 80.00, ""amount_paid"" = 50.00, ""is_paid"" = false, ""paid_date"" = NULL
    WHERE ""id"" = @id;
";
using var cmdUpdate = new NpgsqlCommand(sqlUpdate, conn);
cmdUpdate.Parameters.AddWithValue("id", targetId);
var rows = cmdUpdate.ExecuteNonQuery();

var sqlInst = @"
    UPDATE ""payment_installments""
    SET ""amount_paid"" = 50.00
    WHERE ""payment_record_id"" = @id;
";
using var cmdInst = new NpgsqlCommand(sqlInst, conn);
cmdInst.Parameters.AddWithValue("id", targetId);
cmdInst.ExecuteNonQuery();

Console.WriteLine($"Fixed {rows} records for ID {targetId}");

