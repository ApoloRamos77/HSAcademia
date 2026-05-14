using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var client = new HttpClient();
        
        var loginPayload = new { Email = "superadmin@adhsoft.com", Password = "Admin2026!" };
        var content = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");
        
        var loginRes = await client.PostAsync("http://localhost:5263/api/auth/login", content);
        var loginStr = await loginRes.Content.ReadAsStringAsync();
        Console.WriteLine("Login: " + loginRes.StatusCode);
        
        using var doc = JsonDocument.Parse(loginStr);
        if (!doc.RootElement.TryGetProperty("token", out var tokenEl)) {
            Console.WriteLine("No token found");
            return;
        }
        var token = tokenEl.GetString();
        
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await client.GetAsync("http://localhost:5263/api/calendar/events?year=2026&month=5");
        Console.WriteLine("Events: " + res.StatusCode);
        var json = await res.Content.ReadAsStringAsync();
        Console.WriteLine(json.Substring(0, Math.Min(500, json.Length)));
    }
}
