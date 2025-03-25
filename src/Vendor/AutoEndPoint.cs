using System;
using System.Net;
using System.Net.Sockets;

namespace Zhger.Net.Ftp.Vendor
{

    /// <summary>
    /// 一个IP终结点
    /// </summary>
    internal class AutoEndPoint
    {
        private readonly EndPoint _endpoint = null;
        private string _ipAddress = "0.0.0.0";
        private int _port = 0;

        public string HostOrIpAddress { get => _ipAddress; set { _ipAddress = value; } }
        public int Port { get => _port; set => _port = value; }
        public AutoEndPoint(string hostOrIpAddress, int port)
        {
            _ipAddress = hostOrIpAddress;
            _port = port;

            if (IPAddress.TryParse(_ipAddress, out IPAddress ipAddress))
            {
                _endpoint = new IPEndPoint(ipAddress, _port);
                return;
            }
            _endpoint = new DnsEndPoint(_ipAddress, _port, AddressFamily.InterNetwork);
        }
        public AutoEndPoint(string hostOrIpAddressAndPort)
        {
            AddressFamily addressFamily = AddressFamily.InterNetwork;
            string _host = "0.0.0.0";
            string _port = "0";
            int idx = hostOrIpAddressAndPort.IndexOf('[');
            if (idx > 0) throw new ArgumentException("hostOrIpAddressAndPort");
            if (idx == 0)
            {
                //ipv6
                idx = hostOrIpAddressAndPort.IndexOf(']');
                if (idx == -1) throw new ArgumentException("hostOrIpAddressAndPort");
                _host = hostOrIpAddressAndPort.Substring(0, idx + 1);
                if (idx < hostOrIpAddressAndPort.Length - 1)
                {
                    _port = hostOrIpAddressAndPort.Substring(idx + 1);
                    if (_port[0] == ':') _port = _port.TrimStart(':');
                }
                addressFamily = AddressFamily.InterNetworkV6;
            }
            else
            {
                hostOrIpAddressAndPort = hostOrIpAddressAndPort.TrimEnd(':');
                idx = hostOrIpAddressAndPort.IndexOf(':');
                if (idx == -1)
                {
                    _host = hostOrIpAddressAndPort;
                }
                else if (idx == 0)
                {
                    _port = hostOrIpAddressAndPort.Substring(1);
                }
                else
                {
                    _host = hostOrIpAddressAndPort.Substring(0, idx);
                    _port = hostOrIpAddressAndPort.Substring(idx + 1);
                }
            }
            if (!string.IsNullOrEmpty(_host)) _ipAddress = _host;
            if (int.TryParse(_port, out int port)) this._port = port;


            if (IPAddress.TryParse(_ipAddress, out IPAddress ipAddress))
            {
                _endpoint = new IPEndPoint(ipAddress, this._port);
                return;
            }
            _endpoint = new DnsEndPoint(_ipAddress, this._port, addressFamily);
        }
        public AutoEndPoint() { }
        public AutoEndPoint(EndPoint endpoint)
        {
            _endpoint = endpoint;
            if (endpoint is IPEndPoint ipEndPoint)
            {
                _ipAddress = ipEndPoint.Address.ToString();
                _port = ipEndPoint.Port;
                return;
            }
            if (endpoint is DnsEndPoint dnsEndPoint)
            {
                _ipAddress = dnsEndPoint.Host;
                _port = dnsEndPoint.Port;
                return;
            }
            throw new NotSupportedException("Only IpEndPoint Or DnsEndPoint Supported!");
        }
        public EndPoint GetEndpoint() => _endpoint;

        public static implicit operator EndPoint(AutoEndPoint endpoint) => endpoint._endpoint;
        public static implicit operator AutoEndPoint(EndPoint endpoint) => new(endpoint);

        public override string ToString() => _ipAddress + ":" + _port;
    }
}
