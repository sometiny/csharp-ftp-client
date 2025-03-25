using System.IO;
using System.Net;
using System.Net.Security;
using System.Text.RegularExpressions;
using Zhger.Net.Ftp.Vendor;

namespace Zhger.Net.Ftp
{

    /// <summary>
    /// 不用元组了
    /// </summary>
    public struct FtpResponse
    {
        public FtpResponse(int code, string text, IEnumerable<string> lines)
        {
            StatusCode = code;
            StatusText = text;
            ResponseBodyLines = lines;
        }
        public int StatusCode;
        public string StatusText;
        public IEnumerable<string> ResponseBodyLines;
    }
    public class FtpClient : IDisposable
    {
        private EndPoint _endpoint;

        private Socket _socket = null;
        private string username = null;
        private string password = null;
        private byte[] _buffer = new byte[256];

        private byte[] _pre_buffer = new byte[4096];
        private int _offset = 0;
        private int _length = 0;
        private int _timeout = 15000;

        private string _work_directory = "/";
        private Stream _stream;
        private FtpFeatures _features = null;
        private bool _pret = true;
        private bool printLog = false;
        private bool secure = false;
        private string _base_directory = "/";
        private IPAddress _force_passive_ip = null;
        private bool _force_passive_ip_as_connected = false;

        private bool implicitSecure = false;

        public string WorkDirectory => _work_directory;
        public string ForcePassiveIP
        {
            get => _force_passive_ip.ToString();
            set => _force_passive_ip = value == null ? null : IPAddress.Parse(value);
        }

        public bool Pret { get => _pret; set => _pret = value; }

        public bool PrintLog { get => printLog; set => printLog = value; }

        public bool Secure { get => secure; set => secure = value; }
        public bool ForcePassiveIPAsConnected { get => _force_passive_ip_as_connected; set => _force_passive_ip_as_connected = value; }
        public bool ImplicitSecure { get => implicitSecure; set => implicitSecure = value; }

        public FtpFeatures Features => _features;

        public FtpClient(Uri uri, bool lockBaseDirectory = true)
        {
            _endpoint = new AutoEndPoint(uri.Host, uri.Port);
            string userInfo = uri.UserInfo;

            if (Utility.IsNotEmpty(userInfo))
            {
                KeyValuePair<string, string> info = Utility.GetKeyValue(userInfo, ':');
                username = info.Key;
                password = info.Value;
            }
            if (Utility.IsNotEmpty(uri.PathAndQuery))
            {
                _work_directory = uri.PathAndQuery;
                if (lockBaseDirectory && _work_directory != "/") ChangeDirectory();
            }
        }
        public FtpClient(string uri, bool lockBaseDirectory = true)
        {
            Match match = Regex.Match(uri, @"^ftp://(.+?):(.+)@(?:([0-9\.a-z_\-]+)(?::([0-9]+))?)(/(?:.*))?$");
            if (!match.Success) throw new ArgumentException("ftp链接格式不正确", nameof(uri));

            _endpoint = new AutoEndPoint(match.Groups[3].Value, int.Parse(Utility.IfEmpty(match.Groups[4].Value, "21")));
            this.username = match.Groups[1].Value;
            this.password = match.Groups[2].Value;
            if (Utility.IsNotEmpty(match.Groups[5].Value))
            {
                _work_directory = match.Groups[5].Value;
                if (lockBaseDirectory && _work_directory != "/") ChangeDirectory();
            }
        }
        public FtpClient(EndPoint endPoint)
        {
            _endpoint = endPoint;
        }
        public FtpClient(string host, int port)
        {
            _endpoint = new AutoEndPoint(host, port);
        }

        /// <summary>
        /// 设置初始工作目录
        /// </summary>
        /// <param name="dir"></param>
        public void InitializeWorkDirectory(string dir, bool lockBaseDirectory = true)
        {
            _work_directory = dir;
            if (lockBaseDirectory && _work_directory != "/") ChangeDirectory();
        }

        private void ChangeDirectory()
        {
            _base_directory = _work_directory;
            if (_base_directory.EndsWith("/")) _base_directory = _base_directory.Substring(0, _base_directory.Length - 1);
            if (!_base_directory.StartsWith("/")) _base_directory = "/" + _base_directory;

            _work_directory = "/";
        }

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public void Login(string username, string password)
        {
            this.username = username;
            this.password = password;
            ReLogin();
        }

        /// <summary>
        /// 登录
        /// </summary>
        public void Login()
        {
            ReLogin();
        }

        private bool quited = false;
        /// <summary>
        /// 退出
        /// </summary>
        public void Quit()
        {
            if (quited) return;
            SendCommand(FtpCommands.QUIT);
            _stream?.TryClose();
            _stream = null;
            quited = true;
        }


        private bool CanAuthAsTls()
        {
            return _features.Get("AUTH", "").Contains("TLS");
        }

        private void TlsAuth()
        {
            Logging("Try tls authenticate...\r\n");
            _stream = new SslStream(_stream, false, (a, b, c, d) => true, null);
            (_stream as SslStream).AuthenticateAsClient("");
            Logging("Tls authenticated\r\n");
        }

        /// <summary>
        /// 登录
        /// </summary>
        private void ReLogin()
        {
            TryResolveEndPoint();


            Logging("Try connect server...\r\n");
            _socket = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _socket.ReceiveTimeout = _socket.SendTimeout = _timeout;
            _socket.Connect(_endpoint);

            Logging("Server connected.\r\n");

            _stream = new NetworkStream(_socket, true);

            //隐式TLS，直接开始TLS协议
            if (secure && implicitSecure) TlsAuth();

            //welcome message
            ReadResponse();


            //FETA
            FtpResponse response = SendCommand(FtpCommands.FEAT, false);


            if (response.StatusCode < 400) _features = new FtpFeatures(response.ResponseBodyLines);
            else _features = new FtpFeatures();

            //显式TLS
            if (!implicitSecure && secure && CanAuthAsTls())
            {
                SendCommand(FtpCommands.AUTH_TLS);

                TlsAuth();
            }
            //user
            response = SendCommand(FtpCommands.USER(username));


            //need password
            if (response.StatusCode >= 300) SendCommand(FtpCommands.PASS(password));


            if (_features.Has("UTF8"))
            {
                SendCommand(FtpCommands.OPTS_UTF8_ON, false);
            }


            ChangeWorkDirectory(_work_directory);

        }

        public string MakeAbsolutePath(string path)
        {
            if (_base_directory == "/") return path;

            if (path.StartsWith("/")) return _base_directory.TrimEnd('/') + path;

            return _base_directory.TrimEnd('/') + _work_directory.TrimEnd('/') + "/" + path;
        }

        /// <summary>
        /// 修改工作目录
        /// </summary>
        /// <param name="dir"></param>
        public void ChangeWorkDirectory(string dir)
        {
            SendCommand(FtpCommands.CWD(MakeAbsolutePath(dir)));

            if (dir.StartsWith("/"))
            {
                _work_directory = dir;
                return;
            }
            _work_directory = _work_directory.TrimEnd('/') + "/" + dir;
        }

        /// <summary>
        /// 打印工作目录
        /// </summary>
        public string PrintWorkDirectory()
        {
            return SendCommand(FtpCommands.PWD).StatusText;
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="localFileName"></param>
        public void Retrieve(string fileName, string localFileName)
        {
            using FileStream output = File.OpenWrite(localFileName);
            Retrieve(fileName, output);
        }
        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="fileName"></param>
        public byte[] Retrieve(string fileName)
        {
            using MemoryStream output = new();
            Retrieve(fileName, output);
            return output.ToArray();
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="output"></param>
        public void Retrieve(string fileName, Stream output)
        {
            using (Stream input = OpenRead(fileName)) input.CopyTo(output);

            ReadResponse();

        }
        public void OpenRead(string fileName, Action<Stream> afterStreamOpen)
        {
            using (Stream input = OpenRead(fileName)) afterStreamOpen(input);
            ReadResponse();
        }
        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="fileName"></param>
        private Stream OpenRead(string fileName)
        {
            fileName = MakeAbsolutePath(fileName);
            SendCommand(FtpCommands.TypeI);
            if (_pret && _features.Has("PRET"))
            {
                SendCommand(FtpCommands.RETR(fileName).Pret());
            }
            Stream input = OpenPasvStream();
            try
            {
                SendCommand(FtpCommands.RETR(fileName));
            }
            catch
            {
                input.TryClose();
                throw;
            }

            return input;
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="localFileName"></param>
        public void Store(string fileName, string localFileName)
        {
            using FileStream input = File.OpenRead(localFileName);
            Store(fileName, input);
        }
        public void Store(string fileName, Stream input)
        {
            using (Stream output = OpenWrite(fileName)) input.CopyTo(output);
            ReadResponse();
        }
        public void Store(string fileName, byte[] buffer)
        {
            using MemoryStream input = new(buffer);
            Store(fileName, input);
        }
        public void OpenWrite(string fileName, Action<Stream> afterStreamOpen)
        {
            using (Stream output = OpenWrite(fileName)) afterStreamOpen(output);
            ReadResponse();
        }
        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="fileName"></param>
        private Stream OpenWrite(string fileName)
        {
            fileName = MakeAbsolutePath(fileName);
            SendCommand(FtpCommands.TypeI);
            if (_pret && _features.Has("PRET"))
            {
                SendCommand(FtpCommands.STOR(fileName).Pret());
            }
            Stream output = OpenPasvStream();

            try
            {
                SendCommand(FtpCommands.STOR(fileName));
            }
            catch
            {
                output.TryClose();
                throw;
            }
            return output;
        }

        public void TryCreateDirectory(string fullPath)
        {
            fullPath = MakeAbsolutePath(fullPath);
            int startIndex = 1;
            while (true)
            {
                int idx = fullPath.IndexOf('/', startIndex);
                if (idx == -1) break;

                string dir = fullPath.Substring(0, idx);

                try
                {
                    if (dir != "") CreateDirectory(dir);
                }
                catch { }

                startIndex = idx + 1;
            }
        }

        /// <summary>
        /// 创建文件夹
        /// </summary>
        /// <param name="dir"></param>
        public void CreateDirectory(string dir, bool re = false)
        {
            if (re)
            {
                TryCreateDirectory(dir);
                return;
            }
            SendCommand(FtpCommands.MKD(MakeAbsolutePath(dir)));
        }

        /// <summary>
        /// 移除文件夹
        /// </summary>
        /// <param name="dir"></param>
        public void RemoveDirectory(string dir)
        {
            SendCommand(FtpCommands.RMD(MakeAbsolutePath(dir)));
        }

        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="fileName"></param>
        public void RemoveFile(string fileName)
        {
            SendCommand(FtpCommands.DELE(MakeAbsolutePath(fileName)));
        }

        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="fileName"></param>
        public bool TryRemoveFile(string fileName)
        {
            return SendCommand(FtpCommands.DELE(MakeAbsolutePath(fileName)), false).StatusCode < 400;
        }

        /// <summary>
        /// 实现文件/文件夹详情
        /// </summary>
        /// <param name="fileOrDirName"></param>
        public FtpResponse Show(string fileOrDirName)
        {
            fileOrDirName = MakeAbsolutePath(fileOrDirName);
            if (_features.Has("MLST")) return SendCommand(FtpCommands.MLST(fileOrDirName));
            if (_features.Has("SIZE")) return SendCommand(new FtpCommand("SIZE", fileOrDirName));
            if (_features.Has("MDTM")) return SendCommand(new FtpCommand("MDTM", fileOrDirName));
            throw new NotSupportedException();

        }

        /// <summary>
        /// 获取文件修改时间
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public DateTime GetModifyTime(string fileName)
        {
            string statusText = SendCommand(FtpCommands.MDTM(MakeAbsolutePath(fileName))).StatusText;

            string datetime = Regex.Replace(statusText, @"^([0-9]{4})([0-9]{2})([0-9]{2})([0-9]{2})([0-9]{2})([0-9]{2})$", "$1-$2-$3 $4:$5:$6");

            if (DateTime.TryParse(datetime, out DateTime v)) return v;

            return default;
        }

        /// <summary>
        /// 修改文件修改时间
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="datetime"></param>

        public void ChangeModifyTime(string fileName, DateTime datetime)
        {
            SendCommand(FtpCommands.MFMT(MakeAbsolutePath(fileName), datetime));
        }

        /// <summary>
        /// 列出当前工作目录文件/文件夹信息
        /// </summary>
        /// <returns></returns>
        public IEnumerable<FtpEntity> List()
        {
            if (_pret && _features.Has("PRET")) SendPretListCommand();

            using Stream output = OpenPasvStream();

            bool isMlsd = SendListCommand();

            byte[] response = output.ReadAllBytes();

            ReadResponse();

            IEnumerable<string> files = Encoding.UTF8.GetString(response).Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (isMlsd) return files.Select(FtpEntityWithMlsd.Parse).Where(t => t != null).ToArray();

            return files.Select(FtpEntityWithList.Parse).Where(t => t != null).ToArray();
        }

        /// <summary>
        /// 自动选择目录显示模式
        /// </summary>
        /// <returns></returns>
        private bool SendListCommand()
        {
            if (_features.Has("MLSD"))
            {
                SendCommand(FtpCommands.MLSD());
                return true;
            }
            SendCommand(FtpCommands.LIST("-al"));
            return false;
        }

        /// <summary>
        /// PRET命令
        /// </summary>
        /// <returns></returns>
        private bool SendPretListCommand()
        {
            if (_features.Has("MLSD"))
            {
                SendCommand(FtpCommands.MLSD().Pret());
                return true;
            }
            SendCommand(FtpCommands.LIST("-al").Pret());
            return false;
        }

        /// <summary>
        /// 列出目录文件/文件夹信息
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public IEnumerable<FtpEntity> List(string path)
        {
            SendCommand(FtpCommands.CWD(MakeAbsolutePath(path)));
            return List();

        }


        /// <summary>
        /// 打开被动模式端口
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private Stream OpenPasvStream()
        {
            string statusText = SendCommand(FtpCommands.PASV).StatusText;


            Match match = Regex.Match(statusText, @"\(([0-9]+),([0-9]+),([0-9]+),([0-9]+),([0-9]+),([0-9]+)\)");
            if (!match.Success)
            {
                throw new FtpException(0, "invalid pasv endpoint: " + statusText);
            }

            IPAddress ipaddress = IPAddress.Parse($"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}.{match.Groups[4].Value}");

            if (_force_passive_ip_as_connected) ipaddress = (_endpoint as IPEndPoint).Address;

            if (_force_passive_ip != null) ipaddress = _force_passive_ip;
            int port = (int.Parse(match.Groups[5].Value) << 8) | int.Parse(match.Groups[6].Value);

            EndPoint remoteEndPoint = new IPEndPoint(ipaddress, port);

            Socket socket = new(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveTimeout = _timeout
            };
            Logging($"Try connect: {remoteEndPoint}...");
            socket.Connect(remoteEndPoint);

            Stream stream = new NetworkStream(socket, true);

            if (secure && implicitSecure)
            {
                Logging("Try tls authenticate pasv...");
                stream = new SslStream(stream, false, (a, b, c, d) => true, null);
                (stream as SslStream).AuthenticateAsClient("");
                Logging("Tls pasv authenticated");
            }
            return stream;
        }


        /// <summary>
        /// 解析DNS
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void TryResolveEndPoint()
        {
            if (_endpoint is not DnsEndPoint dns) return;

            Logging($"Try resolve hostname({dns.Host})...");
            var address = Dns.GetHostAddresses(dns.Host).OrderBy(t => t.AddressFamily).FirstOrDefault()
                ?? throw new FtpException(0, $"can not resolve host '{dns.Host}'");


            Logging($"Resolved => {address}");
            _endpoint = new IPEndPoint(address, dns.Port);
        }


        /// <summary>
        /// 读取响应信息
        /// </summary>
        /// <param name="throwException"></param>
        /// <returns></returns>
        /// <exception cref="FtpException"></exception>
        private FtpResponse ReadResponse(bool throwException = true)
        {
            List<string> response = new List<string>();
            int code;
            string statusText;
            while (true)
            {
                string line = ReadLine();
                Match match = Regex.Match(line, @"^([0-9]{3})\-");
                if (match.Success) continue;

                match = Regex.Match(line, @"^([0-9]{3}) ");
                if (match.Success)
                {
                    code = int.Parse(match.Groups[1].Value);
                    statusText = line.Substring(4);
                    if (code >= 400 && throwException) throw new FtpException(code, statusText);
                    break;
                }
                response.Add(line.TrimStart());
            }

            return new FtpResponse(code, statusText, response);
        }


        /// <summary>
        /// 发送命令
        /// </summary>
        /// <param name="command"></param>
        /// <param name="throwException"></param>
        /// <returns></returns>
        private FtpResponse SendCommand(FtpCommand command, bool throwException = true)
        {
            return SendCommand(command.GetCommand(), throwException);
        }

        /// <summary>
        /// 发送命令
        /// </summary>
        /// <param name="command"></param>
        /// <param name="throwException"></param>
        /// <returns></returns>
        private FtpResponse SendCommand(byte[] command, bool throwException = true)
        {

            if (_socket == null || !IsAvailable) ReLogin();

            if (printLog)
            {
                string line = Encoding.ASCII.GetString(command);
                if (line.StartsWith("PASS ")) line = "PASS **********\r\n";
                Logging(line);
            }

            _stream.Write(command, 0, command.Length);

            return ReadResponse(throwException);
        }

        /// <summary>
        /// 判断当前Socket是否有效
        /// </summary>
        private bool IsAvailable
        {
            get
            {
                try
                {
                    _ = _socket.RemoteEndPoint;
                    return true;
                }
                catch (SocketException)
                {
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }
        }


        /// <summary>
        /// 预读取数据
        /// </summary>
        private void FillPreBuffer()
        {
            int received = _stream.Read(_pre_buffer, 0, _pre_buffer.Length);
            if (received == 0)
            {
                _offset = 0;
                _length = 0;
                return;
            }

            _offset = 0;
            _length = received;
        }

        private int ReadByte()
        {
            if (_length == 0 || _offset == _length) FillPreBuffer();

            if (_length == 0) return -1;

            return _pre_buffer[_offset++];
        }

        /// <summary>
        /// 读取行
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private string ReadLine()
        {
            int offset = 0;
            int length = _buffer.Length;
            while (offset < length)
            {
                int chr = ReadByte();
                if (chr == -1) break;

                if (chr == '\n')
                {
                    _buffer[offset++] = (byte)chr;
                    string line = Encoding.UTF8.GetString(_buffer, 0, offset);
                    Logging(line, "s", "c");
                    return line.TrimEnd('\r', '\n');
                }

                _buffer[offset++] = (byte)chr;
            }
            throw new FtpException(0, "end of stream");
        }

        protected virtual void Logging(string message, string from = "c", string to = "s")
        {
            if (!message.EndsWith("\r") && !message.EndsWith("\n")) message += "\r\n";
            if (printLog) Console.Write($"{from} -> {to}: {message}");
        }

        public void Dispose()
        {
            if (_socket == null || !IsAvailable) return;
            Quit();
        }
    }
}
