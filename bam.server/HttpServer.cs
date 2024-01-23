/*
	Copyright © Bryan Apellanes 2015  
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using Bam.Net;
using Bam.Net.Logging;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Bam.Server
{
    public class HttpServer : IDisposable
    {
        private static ConcurrentDictionary<HostPrefix, HttpServer> _listening = new ConcurrentDictionary<HostPrefix, HttpServer>();
        private readonly HttpListener _listener;
        private readonly Thread _handlerThread;
        private ILogger _logger;

        public HttpServer(Action<HttpListenerContext> handler, ILogger? logger = null)
        {
            _logger = logger ?? Log.Default;

            _listener = new HttpListener();
            _handlerThread = new Thread(HandleRequests);

            _hostPrefixes = new HashSet<HostPrefix>();


            ProcessRequest = handler;
        }

        HashSet<HostPrefix> _hostPrefixes;
        public HostPrefix[] HostPrefixes
        {
            get
            {
                return _hostPrefixes.ToArray();
            }
            set
            {
                _hostPrefixes = new HashSet<HostPrefix>(value);
            }
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

        public void Start(params HostPrefix[] hostPrefixes)
        {
            Start(Usurped, hostPrefixes);
        }

        static object _startLock = new object();
        public void Start(bool usurped, params HostPrefix[] hostPrefixes)
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
                        AddHostPrefix(hp);
                    }
                    else if (usurped && _listening.ContainsKey(hp))
                    {
                        _listening[hp].Stop();
                        _listening.TryRemove(hp, out _);
                        AddHostPrefix(hp);
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

        private void AddHostPrefix(HostPrefix hp)
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

            foreach (HostPrefix hp in _listening.Keys)
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

        private void HandleRequests()
        {
            while (_listener != null && _listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = _listener.GetContext();
                    Task.Run(() => ProcessRequest(context));
                }
                catch { }
            }
        }

        public Action<HttpListenerContext> ProcessRequest;
    }
}
