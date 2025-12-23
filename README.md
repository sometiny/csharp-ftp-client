# csharp-ftp-client

ftp客户端，纯托管代码，无第三方依赖。


```csharp
using var ftpClient = new FtpClient("your-server-name", 21);
ftpClient.Login("username", "password");
var files = ftpClient.List();
foreach (var file in files)
{
    Console.WriteLine($"{file.Name} => {file.Size} => {file.Type}");
}
```
