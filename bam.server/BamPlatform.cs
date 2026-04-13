using Bam.Protocol.Server;
using Microsoft.AspNetCore.Builder;

namespace Bam.Server
{
    /// <summary>
    /// Provides static methods for creating and managing BAM server instances, including named servers
    /// and WebApplication-based servers. Tracks all created servers and stops them on domain unload.
    /// </summary>
    public class BamPlatform
    {
        static BamPlatform()
        {
            AppDomain.CurrentDomain.DomainUnload += async (o, a) => await StopServersAsync();
            Servers = new HashSet<IManagedServer>();
        }


        /// <summary>
        /// Gets the set of all managed server instances created by this platform.
        /// </summary>
        public static HashSet<IManagedServer> Servers { get; }

        internal static async Task<T> CreateManagedServerAsync<T>(Func<T> initializer) where T : IManagedServer
        {
            return await Task.Run(() =>
            {
                T server = initializer();
                Servers.Add(server);
                return server;
            });
        }

        /// <summary>
        /// Gets a deterministic integer value between 1024 and 65535 for the specified string.  Returns
        /// the same value for repeated calls with the same string.
        /// </summary>
        /// <param name="name">The string value to hash into a port number.</param>
        /// <returns>An integer port number between 1024 and 65535, deterministic for the given name.</returns>
        public static int GetUnprivilegedPortForName(string name)
        {
            return name.ToHashIntBetween(HashAlgorithms.SHA256, 1024, 65535);
        }

        /// <summary>
        /// Creates a named server.  The server name is used to logically identify the server and should not be confused with the hostname the server responds to.
        /// </summary>
        /// <param name="name">The logical server name, also used to derive the port via name-based hashing.</param>
        /// <returns>A task that resolves to the created <see cref="BamServer"/>.</returns>
        public static async Task<BamServer> CreateNamedServerAsync(string name)
        {
            BamServerOptions options = new BamServerOptions();
            options.UseNameBasedPort = true;
            options.HttpHostBinding = new ManagedServerHostBinding(name);
            return await CreateServerAsync(options);
        }

        /// <summary>
        /// Create a BamServer that listens for requests to "localhost" on a random port from 8080 to 65535.
        /// </summary>
        /// <returns>BamServer</returns>
        public static async Task<BamServer> CreateServerAsync()
        {
            return await CreateServerAsync(RandomNumber.Between(8079, 65535));
        }

        /// <summary>
        /// Create a BamServer that listens for request to "localhost" on the specified port.
        /// </summary>
        /// <param name="port">The port to listen on.</param>
        /// <returns>A task that resolves to the created <see cref="BamServer"/>.</returns>
        public static async Task<BamServer> CreateServerAsync(int port)
        {
            BamServerOptions options = new BamServerOptions();
            options.TcpPort = port;
            options.HttpHostBinding = new HostBinding(port);
            return await CreateServerAsync(options);
        }

        /// <summary>
        /// Creates a BamServer with the specified options.
        /// </summary>
        /// <param name="options">The server options including host binding and configuration.</param>
        /// <returns>A task that resolves to the created <see cref="BamServer"/>.</returns>
        public static async Task<BamServer> CreateServerAsync(BamServerOptions options)
        {
            return await Task.Run(() =>
            {
                BamServer bamAppServer = new BamServer(options);
                Servers.Add(bamAppServer);
                return bamAppServer;
            });
        }

        /// <summary>
        /// Creates a <see cref="WebApplicationBamServer"/> with a name-derived port.
        /// </summary>
        /// <param name="name">The logical server name, also used to derive the port.</param>
        /// <returns>A task that resolves to the created <see cref="WebApplicationBamServer"/>.</returns>
        public static async Task<WebApplicationBamServer> CreateWebApplicationServerAsync(string name)
        {
            int port = GetUnprivilegedPortForName(name);
            return await CreateWebApplicationServerAsync(name, port);
        }

        /// <summary>
        /// Creates a <see cref="WebApplicationBamServer"/> with the specified name and port.
        /// </summary>
        /// <param name="name">The logical server name.</param>
        /// <param name="port">The port to listen on.</param>
        /// <returns>A task that resolves to the created <see cref="WebApplicationBamServer"/>.</returns>
        public static async Task<WebApplicationBamServer> CreateWebApplicationServerAsync(string name, int port)
        {
            BamServerOptions options = new BamServerOptions();
            options.ServerName = name;
            options.HttpHostBinding = new HostBinding(port);
            return await CreateManagedServerAsync(() =>
                new WebApplicationBamServer(options));
        }

        /// <summary>
        /// Stops all managed servers that have been created by this platform.
        /// </summary>
        /// <returns>A task representing the asynchronous stop operation.</returns>
        public static async Task StopServersAsync()
        {
            await Task.Run(() =>
            {
                Task.WaitAll(Servers.EachAsync(s => s.Stop()));
            });
        }
    }
}