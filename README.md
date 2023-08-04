# MVNet [![Nuget](https://img.shields.io/nuget/v/MVNet)](https://www.nuget.org/packages/MVNet/)
The library has been moved from [Leaf.xNet](https://github.com/csharp-leaf/Leaf.xNet) to [RuriLib.Http](https://github.com/openbullet/OpenBullet2/tree/master/RuriLib.Http) and [RuriLib.Proxies](https://github.com/openbullet/OpenBullet2/tree/master/RuriLib.Proxies).    

**Maybe the question is, how is it different from the main library?**  
*I must say that I made some changes to make it easier to use and also fixed some minor bugs*

# Example
```csharp
using MVNet;

CookieStorage cookies = new CookieStorage();
using (MVNet.HttpClient client = new MVNet.HttpClient())
{
    client.ReadWriteTimeOut = TimeSpan.FromSeconds(10);
    client.ConnectTimeout = TimeSpan.FromSeconds(10);
    client.Proxy = ProxyClient.Parse("http://127.0.0.1:8080");
    using (HttpRequest req = new HttpRequest("https://www.google.com/"))
    {
        req.Cookies = cookies;
        req.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
        using (HttpResponse res = await client.SendAsync(req))
        {
            string source = await res.Content.ReadAsStringAsync();
        }
    }
}
```

## How to add header?
```csharp
using (HttpRequest req = new HttpRequest("https://www.example.com/"))
{
    req.AddHeader("name", "value");
}
```

## How to add cookie?
```csharp
using (HttpRequest req = new HttpRequest("https://www.example.com/"))
{
    req.AddCookie("name", "value");
}
```

## How to change proxy?
```csharp
using (MVNet.HttpClient client = new MVNet.HttpClient())
{
    client.Proxy = ProxyClient.Parse("http://127.0.0.1:8080"); //http
    //or
    client.Proxy = ProxyClient.Parse("socks4://127.0.0.1:1080"); //socks4
    //or
    client.Proxy = ProxyClient.Parse("socks4a://127.0.0.1:1080"); //socks4a
    //or
    client.Proxy = ProxyClient.Parse("socks5://127.0.0.1:1080"); //socks5
    //or
    client.Proxy = ProxyClient.Parse("ap://03000000093132372E302E302E3104393839380431323334"); //azadi
}
```

