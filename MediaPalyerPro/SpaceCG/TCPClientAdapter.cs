using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Modbus.IO;

namespace MediaPalyerPro.SpaceCG
{
	public class TCPClientAdapter : IStreamResource, IDisposable
	{
		private HPSocket.Tcp.TcpClient _TcpClient;

		private Queue<byte> queue = new Queue<byte>(10240);

		public TCPClientAdapter(string address, ushort port)
		{
			_TcpClient = new HPSocket.Tcp.TcpClient();
			_TcpClient.Address = address;
			_TcpClient.Port = port;
			_TcpClient.Async = true;
            _TcpClient.OnClose += _TcpClient_OnClose;
            _TcpClient.OnConnect += _TcpClient_OnConnect;
            _TcpClient.OnReceive += _TcpClient_OnReceive;
		}

        private HPSocket.HandleResult _TcpClient_OnReceive(HPSocket.IClient sender, byte[] data)
        {
			foreach (var b in data)
				queue.Enqueue(b);

			return HPSocket.HandleResult.Ok;
		}

        private HPSocket.HandleResult _TcpClient_OnConnect(HPSocket.IClient sender)
        {
            Console.WriteLine("TcpClient onConnect ... ");
			return HPSocket.HandleResult.Ok;
        }

        private HPSocket.HandleResult _TcpClient_OnClose(HPSocket.IClient sender, HPSocket.SocketOperation socketOperation, int errorCode)
        {
			Console.WriteLine("TcpClient OnClose ... ");
			Task.Run(() =>
			{
				Console.WriteLine("TcpClient Reconnect ... ");
				System.Threading.Thread.Sleep(2000);
				try
				{
					_TcpClient?.Connect();
				}
				catch (Exception) { }
			});
			return HPSocket.HandleResult.Ok;
        }

		public int InfiniteTimeout
		{
			get
			{
				return -1;
			}
		}

		public int ReadTimeout
		{
			get
			{
				return 0;
			}
			set
			{
				
			}
		}

		public int WriteTimeout
		{
			get
			{
				return 0;
			}
			set
			{
				
			}
		}

		public void Write(byte[] buffer, int offset, int size)
		{
			if(_TcpClient?.IsConnected == true)
				_TcpClient.Send(buffer, offset, size);
		}

		public int Read(byte[] buffer, int offset, int size)
		{
			if (_TcpClient?.IsConnected == false) return 0;

            Console.WriteLine($"Offset: {offset}  Size: {size}");

			

			return 0;
		}

		public void DiscardInBuffer()
		{
			
		}

		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				this._TcpClient?.Dispose();
				this._TcpClient = null;
			}
		}

		
	}
}
