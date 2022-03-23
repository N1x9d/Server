using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace Server
{
	public struct soketInfo
    {
		public string ip;
		public string mesage;

       

        public soketInfo(string ip, string mesage)
        {
			this.ip = ip.Substring(0, ip.LastIndexOf(':')); 
            
            this.mesage = mesage;
        }
    }
    class Program
    {
		static List<soketInfo> histori = new List<soketInfo>();
		static void Main(string[] args)
        {
			
			string pubIp = new System.Net.WebClient().DownloadString("https://api.ipify.org");
			Console.WriteLine("Server global IP "+ pubIp);
			Console.Write("Server local IP ");
			var host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (var ip in host.AddressList)
			{
				if (ip.AddressFamily == AddressFamily.InterNetwork)
				{
					Console.WriteLine(ip.ToString());
				}
			}
			Process process = new Process();

			string directory = Directory.GetCurrentDirectory();
			string script = "main.py";

			var startInfo = new ProcessStartInfo("python");
			startInfo.WorkingDirectory = directory;
			startInfo.Arguments = script;
			startInfo.UseShellExecute = false;
			startInfo.CreateNoWindow = true;
			startInfo.RedirectStandardError = true;
			startInfo.RedirectStandardOutput = true;

			process.StartInfo = startInfo;

			//process.Start();
			Thread myThread = new Thread(new ThreadStart(ResponseServer));
			myThread.Start(); // запускаем поток
			TcpListener listener = new TcpListener(IPAddress.Any, 20000);
			listener.Start();

			while (true)
			{
				TcpClient client = listener.AcceptTcpClient();
				using (NetworkStream inputStream = client.GetStream())
				{
					
					using (BinaryReader reader = new BinaryReader(inputStream))
					{
						string filename = reader.ReadString();
						long lenght = reader.ReadInt64();
						using (FileStream outputStream = File.Open(Path.Combine( filename), FileMode.Create))
						{
							long totalBytes = 0;
							int readBytes = 0;
							byte[] buffer = new byte[2048];

							do
							{
								readBytes = inputStream.Read(buffer, 0, buffer.Length);
								outputStream.Write(buffer, 0, readBytes);
								totalBytes += readBytes;
							} while (client.Connected && totalBytes < lenght);
							Console.WriteLine("Принят файл " + filename + " Размер " + totalBytes);
							outputStream.Close();
							using (	var cl = new RequestSocket())
							{
								cl.Connect("tcp://localhost:5555");
								Console.WriteLine(filename);
									cl.SendFrame(filename);
									var message = cl.ReceiveFrameString();
									histori.Add(new soketInfo(client.Client.RemoteEndPoint.ToString(),message));
									Console.WriteLine("Received {0}", message);
									
							}
						}
					}

				}
				client.Close();

			}
		}

		private static void ResponseServer()
        {
			IPEndPoint ipPoint = new IPEndPoint(IPAddress.Any, 5558);

			// создаем сокет
			Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			try
			{
				// связываем сокет с локальной точкой, по которой будем принимать данные
				listenSocket.Bind(ipPoint);

				// начинаем прослушивание
				listenSocket.Listen(10);

				while (true)
				{
					Socket handler = listenSocket.Accept();
					
					// получаем сообщение
					StringBuilder builder = new StringBuilder();
					int bytes = 0; // количество полученных байтов
					byte[] data = new byte[256]; // буфер для получаемых данных

					do
					{
						bytes = handler.Receive(data);
						builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
					}
					while (handler.Available > 0);
					IPEndPoint  clientep = (IPEndPoint)handler.RemoteEndPoint;
					var a = histori.Where(c => clientep.ToString().Contains(c.ip)).ToArray();
					string message="errore";
					if (a.Length!=0)
                        message = a.Last().mesage;
					
					data = Encoding.Unicode.GetBytes(message);
					handler.Send(data);
					// закрываем сокет
					handler.Shutdown(SocketShutdown.Both);
					handler.Close();
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}
	}
}
