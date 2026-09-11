using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

class Program {
    static async Task Main() {
        var _httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.deepseek.com/chat/completions");
        request.Headers.Add("Authorization", "Bearer sk-73bff44156544967aac08fb4d313f4ce");
        string json = "{"model":"deepseek-flash", "messages":[{"role":"user", "content":"ping"}], "stream":true}";
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.SendAsync(request);
        Console.WriteLine(response.StatusCode);
    }
}
