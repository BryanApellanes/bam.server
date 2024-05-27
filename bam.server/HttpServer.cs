/*
	Copyright © Bryan Apellanes 2015  
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using Bam;
using Bam.Logging;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Bam.Server;

namespace Bam.Server
{
    public class HttpServer : IDisposable
    {
        private static readonly ConcurrentDictionary<HostBinding, HttpServer> _listening = new ConcurrentDictionary<HostBinding, HttpServer>();
        private readonly HttpListener _listener;
        private readonly Thread _handlerThread;
        private readonly ILogger _logger;

        public HttpServer(Action<HttpListenerContext> requestHandler, ILogger? logger = null)
        {
            _logger = logger ?? Log.Default;

            _listener = new HttpListener();
            _handlerThread = new Thread(HandleHttpListenerContextRequests);

            _hostPrefixes = new HashSet<HostBinding>();

            ProcessHttpContextListenerRequest = requestHandler;
        }

        HashSet<HostBinding> _hostPrefixes;
        public HostBinding[] HostPrefixes
        {
            get => _hostPrefixes.ToArray();
            set => _hostPrefixes = new HashSet<HostBinding>(value);
        }

        /// <summary>
        /// Gets or sets a value that indicates whether to attempt to stop
        /// other HttpServers that are listening on the same port and 
        /// hostname.
        /// </summary>
        public bool Usurped
        {
            get;
            set;
        }

        public void Start()
        {
            Start(HostPrefixes);
        }

        public void Start(params HostBinding[] hostPrefixes)
        {
            Start(Usurped, hostPrefixes);
        }

        static object _startLock = new object();
        public void Start(bool usurped, params HostBinding[] hostPrefixes)
        {
            if (hostPrefixes.Length == 0)
            {
                hostPrefixes = HostPrefixes;
            }
            lock (_startLock)
            {
                hostPrefixes.Each(hp =>
                {
                    if (!_listening.ContainsKey(hp))
                    {
                        AddHostBinding(hp);
                    }
                    else if (usurped && _listening.ContainsKey(hp))
                    {
                        _listening[hp].Stop();
                        _listening.TryRemove(hp, out _);
                        AddHostBinding(hp);
                    }
                    else
                    {
                        _logger.AddEntry("HttpServer: Another HttpServer is already listening for host {0}", LogEventType.Warning, hp.ToString());
                    }
                });

                _listener.Start();
                _handlerThread.Start();
            }
        }

        private void AddHostBinding(HostBinding hp)
        {
            _listening.TryAdd(hp, this);
            string path = hp.ToString();
            _logger.AddEntry("HttpServer: {0}", path);
            _listener.Prefixes.Add(path);
        }

        public void Dispose()
        {
            IsDisposed = true;
            Stop();
        }

        public bool IsDisposed { get; private set; }

        public bool IsListening => _listener.IsListening;

        public void Stop()
        {
            try
            {
                _listener.Stop();
                _logger.AddEntry("HttpServer listener stopped");
            }
            catch (Exception ex)
            {
                _logger.AddEntry("Error stopping HttpServer: {0}", ex, ex.Message);
            }

            foreach (HostBinding hp in _listening.Keys)
            {
                try
                {
                    if (_listening[hp] == this)
                    {
                        if (_listening.TryRemove(hp, out HttpServer? server))
                        {
                            server?.Stop();
                        }
                    }
                }
                catch { }
            }

            try
            {
                if (_handlerThread.ThreadState == ThreadState.Running || _handlerThread.ThreadState == ThreadState.WaitSleepJoin)
                {
                    _handlerThread.Interrupt();
                    _handlerThread.Join(5000);
                }
            }
            catch { }
        }

        private void HandleHttpListenerContextRequests()
        {
            while (_listener != null && _listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = _listener.GetContext();
                    Task.Run(() => ProcessHttpContextListenerRequest(context));
                }
                catch { }
            }
        }

        public Action<HttpListenerContext> ProcessHttpContextListenerRequest;
    }
}
