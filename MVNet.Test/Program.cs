using MVNet;

try
{
    Console.Title = "MVNet.Test";

    using (HttpRequest req = new HttpRequest("https://www.google.com/"))
    {
        req.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
        using (MVNet.HttpClient client = new MVNet.HttpClient())
        {
            client.ReadWriteTimeOut = TimeSpan.FromSeconds(10);
            client.ConnectTimeout = TimeSpan.FromSeconds(10);
            using (HttpResponse res = await client.SendAsync(req))
            {
                var cookeis = res.GetCookies();
                Console.WriteLine(await res.Content.ReadAsStringAsync());
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}

Console.ReadKey();