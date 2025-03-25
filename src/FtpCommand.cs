
namespace Zhger.Net.Ftp
{
    public class FtpCommand
    {
        private string cmd;
        private string[] arguments;

        public FtpCommand(string cmd, params string[] arguments)
        {
            this.cmd = cmd;
            this.arguments = arguments.Where(t=>!string.IsNullOrEmpty(t)).ToArray();
        }

        public string Name => cmd;

        public string this[int index]
            => index >= arguments.Length ? null : arguments[index];

        public override string ToString()
        {
            return (arguments.Length == 0 ? cmd : $"{cmd} {string.Join(" ", arguments)}") + "\r\n";
        }

        public byte[] GetCommand()
        {
            return GetCommand(Encoding.UTF8);
        }


        public byte[] GetCommand(Encoding encoding)
        {
            return encoding.GetBytes(ToString());
        }

        public FtpCommand Pret()
        {
            var t = arguments.ToList();
            t.Insert(0, cmd);

            return new FtpCommand("PRET", t.ToArray());
        }
    }

    internal static class FtpCommands
    {
        private static FtpCommand mlsd = null;
        private static FtpCommand list = null;
        public static FtpCommand USER(string name) => new("USER", name);

        public static FtpCommand PASS(string password) => new("PASS", password);

        /// <summary>
        /// 修改工作目录
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        public static FtpCommand CWD(string dir) => new("CWD", dir);

        /// <summary>
        /// 列出文件夹详情
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        public static FtpCommand MLSD(string dir) => new("MLSD", dir);

        /// <summary>
        /// 创建文件夹
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        public static FtpCommand MKD(string dir) => new("MKD", dir);

        /// <summary>
        /// 删除文件夹
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        public static FtpCommand RMD(string dir) => new("RMD", dir);

        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static FtpCommand DELE(string fileName) => new("DELE", fileName);

        /// <summary>
        /// 获取文件修改时间
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static FtpCommand MDTM(string fileName) => new("MDTM", fileName);

        /// <summary>
        /// 显示文件、文件夹详情
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>

        public static FtpCommand MLST(string fileName) => new("MLST", fileName);

        /// <summary>
        /// 列出文件夹
        /// </summary>
        /// <returns></returns>
        public static FtpCommand MLSD() => mlsd ??= new("MLSD");

        /// <summary>
        /// 列出文件夹，优先使用MLSD
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static FtpCommand LIST(string options) => new("LIST", options ?? "-al");

        /// <summary>
        /// 列出文件夹，优先使用MLSD
        /// </summary>
        /// <returns></returns>
        public static FtpCommand LIST() => list ??= new("LIST");

        /// <summary>
        /// 保存文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static FtpCommand STOR(string fileName) => new("STOR", fileName);


        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static FtpCommand RETR(string fileName) => new("RETR", fileName);


        /// <summary>
        /// 设置文件下载开始位置
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public static FtpCommand REST(long position) => new("REST", position.ToString());

        /// <summary>
        /// 修改文件最后修改时间
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static FtpCommand MFMT(string fileName, DateTime dateTime) => new("MFMT", dateTime.ToString("yyyyMMddHHmmss"), fileName);


        /// <summary>
        /// 二进制模式
        /// </summary>
        public static readonly byte[] TypeI = new FtpCommand("TYPE", "I").GetCommand();

        /// <summary>
        /// 服务器特性
        /// </summary>
        public static readonly byte[] FEAT = new FtpCommand("FEAT").GetCommand();

        /// <summary>
        /// 被动模式
        /// </summary>
        public static readonly byte[] PASV = new FtpCommand("PASV").GetCommand();

        /// <summary>
        /// NOOP
        /// </summary>
        public static readonly byte[] NOOP = new FtpCommand("NOOP").GetCommand();

        /// <summary>
        /// 显示工作目录
        /// </summary>
        public static readonly byte[] PWD = new FtpCommand("PWD").GetCommand();


        /// <summary>
        /// 显示系统类型
        /// </summary>
        public static readonly byte[] SYST = new FtpCommand("SYST").GetCommand();

        /// <summary>
        /// 启用UTF8
        /// </summary>
        public static readonly byte[] OPTS_UTF8_ON = new FtpCommand("OPTS", "UTF8", "ON").GetCommand();


        /// <summary>
        /// 退出登录
        /// </summary>
        public static readonly byte[] QUIT = new FtpCommand("QUIT").GetCommand();

        /// <summary>
        /// TLS
        /// </summary>
        public static readonly byte[] AUTH_TLS = new FtpCommand("AUTH", "TLS").GetCommand();

    }
}

