using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Modbus.IO;

namespace MediaPalyerPro.SpaceCG
{
	public class TCPClientAdapter : IStreamResource, IDisposable
	{
		private ushort port;
		private string address;
		private bool _disposed = false;
		private ConnectStatus _ConnectStatus = ConnectStatus.Ready;

		public TcpClient _TcpClient { get; private set; }

		private enum ConnectStatus
        {
			Ready = 0,
			ConnectSuccess,
			Connecting,
			ConnectFailed,
		}

		public TCPClientAdapter(string address, ushort port)
		{
			this.port = port;
			this.address = address;

			Connect(address, port);
		}

		public async void Connect(string address, ushort remotePort)
		{
			Console.WriteLine($"Connect::{ _ConnectStatus }");
			if (_disposed) return;
			if (_TcpClient != null && _TcpClient.Connected) return;
			if (_TcpClient != null && _ConnectStatus == ConnectStatus.Connecting) return;

			if (_TcpClient == null) _TcpClient = new TcpClient();
			//_TcpClient.LingerState = new LingerOption(false, 1);

			try
			{
				_ConnectStatus = ConnectStatus.Connecting;
				await _TcpClient.ConnectAsync(address, port);
#if false
				IAsyncResult asyncresult = _TcpClient.BeginConnect(address, port, null, null);
				asyncresult.AsyncWaitHandle.WaitOne(3000);
				if (!asyncresult.IsCompleted)
				{
					Close();
					Thread.Sleep(3000);
					Connect(address, port);
					Console.WriteLine("Cannot to Connect Server");
					return;
				}
#endif
			}
			catch (Exception ex)
			{
                Console.WriteLine(ex);
				_ConnectStatus = ConnectStatus.ConnectFailed;
				Close();
				Thread.Sleep(5000);
				Connect(address, port);
				return;
			}

			if (IsOnline)
			{
				_ConnectStatus = ConnectStatus.ConnectSuccess;
				return;
			}
			else
            {
				_ConnectStatus = ConnectStatus.ConnectFailed;
				Close();
				Thread.Sleep(5000);
				Connect(address, port);
			}
		}
		public void Close()
		{
			Console.WriteLine($"Close::{ _ConnectStatus }");
			if (_TcpClient == null) return;
			if (_ConnectStatus == ConnectStatus.Connecting) return;

			_ConnectStatus = ConnectStatus.Ready;
			try
			{
				_TcpClient?.Dispose();
				_TcpClient = null;
			}
			catch (Exception ex)
			{
			}
		}

		private void Connect()
		{
			if (_ConnectStatus != ConnectStatus.Connecting && _ConnectStatus != ConnectStatus.ConnectSuccess)
			{
				Close();
			}
			Connect(address, port);
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
				return (int)(_TcpClient?.GetStream().ReadTimeout);
			}
			set
			{
				if(_TcpClient?.GetStream() != null)
					_TcpClient.GetStream().ReadTimeout = value;
			}
		}

		public int WriteTimeout
		{
			get
			{
				return (int)(this._TcpClient?.GetStream().WriteTimeout);
			}
			set
			{
				if(this._TcpClient?.GetStream() != null)
					this._TcpClient.GetStream().WriteTimeout = value;
			}
		}

		public void Write(byte[] buffer, int offset, int size)
		{
			Console.WriteLine($"Write......{_ConnectStatus} {IsOnline}");
			if (_ConnectStatus == ConnectStatus.Connecting) return;

			try
			{
				if (IsOnline)
				{
					this._TcpClient?.GetStream().Write(buffer, offset, size);
				}
				else
				{
					_ConnectStatus = ConnectStatus.ConnectFailed;
                    Console.WriteLine("Write......NO Exception");
					Connect();
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Write......Exception {ex.Message}");

				_ConnectStatus = ConnectStatus.ConnectFailed;
				Connect();
			}
		}

		public int Read(byte[] buffer, int offset, int size)
		{
			if (_ConnectStatus == ConnectStatus.Connecting) return 0;
			Console.WriteLine($"Read......{_ConnectStatus}");

			try
			{
				if (IsOnline)
				{
					Console.WriteLine($"Read...... Stream.... {offset} {size}");
					if (offset < 0) offset = 0;
					if (size >= 64) size = 4;

					_TcpClient.GetStream().ReadTimeout = 3000;

					int len = (int)(this._TcpClient.GetStream().Read(buffer, offset, size));
					Console.WriteLine($"Read......Size: {len}");
					return len;
				}
				else
				{
					_ConnectStatus = ConnectStatus.ConnectFailed;
					Console.WriteLine($"Read......ON ONline");
					Connect();
					return 0;
				}
			}
			catch(IOException ex)
            {
				Console.WriteLine($"Read......ConnectException");
                Console.WriteLine(ex);

				//_ConnectStatus = ConnectStatus.ConnectFailed;
				//Connect();
            }
			catch(Exception ex)
            {
                Console.WriteLine(ex);
            }

			return 0;
		}

		public void DiscardInBuffer()
		{
			try
			{
				if (IsOnline) this._TcpClient?.GetStream().Flush();
			}
			catch (Exception ex)
			{
				_ConnectStatus = ConnectStatus.ConnectFailed;
				Connect();
			}
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

			_disposed = true;
		}

		public bool IsOnline
        {
			get
            {
				if (_TcpClient?.Client == null) return false;
				return _TcpClient.Client.Connected;
				//return !((_TcpClient.Client.Poll(1000, SelectMode.SelectRead) && (_TcpClient.Client.Available == 0)) || !_TcpClient.Client.Connected);
			}
        }
		
	}
}
