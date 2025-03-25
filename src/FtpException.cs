
namespace Zhger.Net.Ftp
{

    public class FtpException : Exception
    {
        private readonly int code;

        public FtpException( int code, string message, Exception innerException) : base(message, innerException)
        {
            this.code = code;
        }
        public FtpException(int code, string message) : base(message)
        {
            this.code = code;
        }

        public override string Message => code + " " + base.Message;

        public int StatusCode => code;

        public string StatusText => base.Message;
    }
}
