﻿using MVNet;

{
    Console.Title = "MVNet.Test";

    try
    {
        using (HttpRequest req = new HttpRequest("https://www.google.com/"))
        {
            var res = req.Get();
            PrintMessage(res);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
    }

    Console.ReadKey();
}

void PrintMessage(HttpResponse response)
{
    Console.WriteLine($"Request To : {response.Address}");
    Console.WriteLine($"Ssl Protocol : {response.SslProtocol}");
    Console.WriteLine($"Cipher Algorithm : {response.CipherAlgorithm.ToString()}");
    Console.WriteLine($"Hash Algorithm : {response.HashAlgorithm.ToString()}");
    Console.WriteLine($"Tls Cipher : {response.TlsCipher.ToString()}");
    Console.WriteLine($"Certificate Name : {response.RemoteCertificate.GetName()}");
    Console.WriteLine($"Content : {response.ReadAsString()}");
}