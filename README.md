# MVNet [![Nuget](https://img.shields.io/nuget/v/MVNet)](https://www.nuget.org/packages/MVNet/)
**MVNet** - provides HTTP/HTTPS, Socks 4A, Socks 4, Socks 5, Azadi.  
It's a based on [Leaf.xNet](https://github.com/csharp-leaf/Leaf.xNet). And original library [xNet](https://github.com/X-rus/xNet).  
Usage same like original xNet.

# What has changed in this project?
![MVNet](https://s24.picofile.com/file/8453214718/mvnet.png)
* Supports TLS/SSL
* Added Azadi Proxy
* Fix Bugs

# Features
### HTTP Methods
- GET
- POST
- PATCH
- DELETE
- PUT
- OPTIONS

### Keep temporary headers (when redirected)
It's enabled by default. But you can disable this behavior:
```csharp
// After redirection to www.google.com - request won't have Referer header because KeepTemporaryHeadersOnRedirect = false
using (HttpRequest req = new HttpRequest("http://google.com"))
{
    req.KeepTemporaryHeadersOnRedirect = false;
    req.AddHeader(HttpHeader.Referer, "https://google.com");
    req.Get();
}
```

### Middle response headers (when redirected)
```csharp
using (HttpRequest req = new HttpRequest("https://account.sonyentertainmentnetwork.com/"))
{
    req.EnableMiddleHeaders = true;
    
    // This requrest has a lot of redirects
    var res = req.Get();
    var md = res.MiddleHeaders;
}
```

### Cross Domain Cookies
Used native cookie storage from .NET with domain shared access support.  
Cookies enabled by default. If you wait to disable parsing it use:
```csharp
using (HttpRequest req = new HttpRequest("https://www.google.com/"))
{
    req.UseCookies = false;
}
```
Cookies now escaping values. If you wait to disable it use:
```csharp
using (HttpRequest req = new HttpRequest("https://www.google.com/"))
{
    req.Cookies.EscapeValuesOnReceive = false;

    // UnescapeValuesOnSend by default = EscapeValuesOnReceive
    // so set if to false isn't necessary
    req.Cookies.UnescapeValuesOnSend = false;
}
```

### Select SSL Protocols (downgrade when required)
```csharp
using (HttpRequest req = new HttpRequest("https://www.google.com/"))
{
    // By Default (SSL 2 & 3 not used)
    req.SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;
}
```

### My HTTPS proxy returns bad response
Sometimes HTTPS proxy require relative address instead of absolute.
This behavior can be changed:
```csharp
using (HttpRequest req = new HttpRequest("https://www.google.com/"))
{
    req.Proxy.AbsoluteUriInStartingLine = false;
}
```

## Cyrilic and Unicode Form parameters
```csharp
var urlParams = new Parameters {
    { ["name"] = "value"  },
    { ["name"] = "value" }
}

// Or

var urlParams = new Parameters();
urlParams["name"] = "value";
urlParams["name"] = "value";

// Or

var urlParams = new Parameters();
urlParams.Add("name", "value");
urlParams.Add("name", "value");

using (HttpRequest req = new HttpRequest("https://www.google.com/"))
{
    string content = req.Get(urlParams).ReadAsString();
}
```

## A lot of Substring functions
```csharp
using (HttpRequest req = new HttpRequest("https://www.google.com/"))
{
    string content = req.Get().ReadAsString();
    
    string title = content.Substring("<title>", "</title>");

    // substring or default
    string titleWithDefault  = content.Substring("<title>", "</title>") ?? "Nothing";
    string titleWithDefault2 = content.Substring("<title>", "</title>", fallback: "Nothing");

    // substring or empty
    string titleOrEmpty  = content.SubstringOrEmpty("<title>", "</title>");
    string titleOrEmpty2 = content.Substring("<title>", "</title>") ?? ""; // "" or string.Empty
    string titleOrEmpty3 = content.Substring("<title>", "</title>", fallback: string.Empty);

    // substring or thrown exception when not found
    // it will throw new SubstringException with left and right arguments in the message
    string titleOrException  = content.SubstringEx("<title>", "</title>");

    // when you need your own Exception
    string titleOrException2 = content.Substring("<title>", "</title>") ?? throw MyCustomException();
}
```

# How to:
### Get started
Add in the beggining of file.
```csharp
using MVNet;
```
And use one of this code templates:

```csharp
using (var request = new HttpRequest("https://www.google.com/")) {
    // Do something
}

// Or
HttpRequest request = null;
try {
    request = new HttpRequest("https://www.google.com/");
    // Do something 
}
catch (HttpException ex) {
    // Http error handling
    // You can use ex.Status or ex.HttpStatusCode for more details.
}
catch (Exception ex) {
	// Unhandled exceptions
}
finally {
    // Cleanup in the end if initialized
    request?.Dispose();
}

```

### Send multipart requests with fields and files
Methods `AddField()` and `AddFile()` has been removed (unstable).
Use this code:
```csharp
var multipartContent = new MultipartContent()
{
    {new StringContent("Harry Potter"), "login"},
    {new StringContent("Crucio"), "password"},
    {new FileContent(@"C:\hp.rar"), "file1", "hp.rar"}
};

// When response isn't required
using (HttpRequest req = new HttpRequest("https://www.google.com/"))
{
    req.Post(multipartContent);
}

// Or

using (HttpRequest req = new HttpRequest("https://www.google.com/"))
{
    var res = request.Post(multipartContent);
    // And then read as string
    string content = res.ReadAsString();
}
```

### Get page source (response body) and find a value between strings
```csharp
using (HttpRequest req = new HttpRequest("https://www.google.com/"))
{
    string content = request.Get().ReadAsString();
    string title = content.Substring("<title>", "</title>");
}
```

### Get response headers
```csharp
using (HttpRequest req = new HttpRequest("https://www.google.com/"))
{
    var res = req.Get();
    
    string HeaderValue = res["name"];
    // Or
    string HeaderValue = res.GetHeader("name");
}
```

### Download a file
```csharp
using (HttpRequest req = new HttpRequest("http://google.com/file.zip"))
{
    var res = req.Get();
    res.Write("C:\\myDownloadedFile.zip");
}
```

### Set Cookies
```csharp
using (HttpRequest req = new HttpRequest("https://www.google.com/"))
{
    req.AddCookie("name", "value");
}
```

### Get Cookies
```csharp
using (HttpRequest req = new HttpRequest("https://www.google.com/"))
{
    var res = req.Get();
    string CookieValue = res.GetCookie("name");
}
```

### Proxy
Your proxy server:
```csharp
using (HttpRequest req = new HttpRequest("https://www.google.com/"))
{
    // Type: HTTP/HTTPS
    req.Proxy = HttpProxyClient.Parse("127.0.0.1:8080");

    // Type: Socks4
    req.Proxy = Socks4ProxyClient.Parse("127.0.0.1:9000");

    // Type: Socks4a
    req.Proxy = Socks4aProxyClient.Parse("127.0.0.1:9000");

    // Type: Socks5
    req.Proxy = Socks5ProxyClient.Parse("127.0.0.1:9000");

    // Type: Azadi
    req.Proxy = AzadiProxyClient.Parse("ap://AwAAAAkxMjcuMC4wLjEEOTg5OAZzZWNyZXQ%3d");
    // or
    req.Proxy = AzadiProxyClient.Parse("127.0.0.1:9898:secret");
    // or
    req.Proxy = AzadiProxyClient.Parse("127.0.0.1:9898:username:password:secret");
}
```

Debug proxy server (Charles / Fiddler):
```csharp
using (HttpRequest req = new HttpRequest("https://www.google.com/"))
{
    // HTTP/HTTPS (by default is HttpProxyClient at 127.0.0.1:8888)
    req.Proxy = ProxyClient.DebugHttpProxy;

    // Socks5 (by default is Socks5ProxyClient at 127.0.0.1:8889)
    req.Proxy = ProxyClient.DebugSocksProxy;
}
```