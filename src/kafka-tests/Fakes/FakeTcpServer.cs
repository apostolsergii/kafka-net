﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace kafka_tests.Helpers
{
    public class FakeTcpServer : IDisposable
    {
        public delegate void BytesReceivedDelegate(byte[] data);
        public event BytesReceivedDelegate OnBytesReceived;

        private readonly SemaphoreSlim _serverCloseSemaphore = new SemaphoreSlim(0);
        private NetworkStream _stream = null;
        private int _clientCounter = 0;

        public async Task Start(int port)
        {
            var token = new CancellationTokenSource();
            var listener = new TcpListener(IPAddress.Any, port);
            try
            {
                listener.Start();

                AcceptClientsAsync(listener, token.Token);

                await _serverCloseSemaphore.WaitAsync();
            }
            finally
            {
                token.Cancel();
                listener.Stop();
            }
        }

        public void End()
        {
            _serverCloseSemaphore.Release();
        }

        public Task SendDataAsync(byte[] data)
        {
            while (_stream == null) { Thread.Sleep(100); }
            return _stream.WriteAsync(data, 0, data.Length);
        }

        public Task SendDataAsync(string data)
        {
            var msg = Encoding.ASCII.GetBytes(data);
            return SendDataAsync(msg);
        }

        public void DropConnection()
        {
            throw new NotImplementedException();
        }

        private async Task AcceptClientsAsync(TcpListener listener, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);

                HandleClientAsync(client, token);
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            var clientIndex = Interlocked.Increment(ref _clientCounter);

            try
            {
                Console.WriteLine("FakeTcpServer Connected client: {0}", clientIndex);
                using (client)
                {
                    var buffer = new byte[4096];
                    _stream = client.GetStream();

                    while (!token.IsCancellationRequested)
                    {
                        var bytesReceived = await _stream.ReadAsync(buffer, 0, buffer.Length, token);
                        if (bytesReceived > 0)
                        {
                            if (OnBytesReceived != null) OnBytesReceived(buffer.Take(bytesReceived).ToArray());
                        }
                    }
                }
            }
            finally
            {
                Console.WriteLine("FakeTcpServer Disconnected client: {0} ", clientIndex);
                Interlocked.Decrement(ref _clientCounter);
            }
        }

        public void Dispose()
        {
            _serverCloseSemaphore.Release();
        }
    }

    
}