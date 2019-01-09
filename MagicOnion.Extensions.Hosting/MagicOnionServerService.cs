using System;
using Grpc.Core;
using MagicOnion.Server;
using MagicOnion;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MagicOnion.Extensions.Hosting
{
    sealed class MagicOnionServerServiceOptions
    {
        public Assembly[] SearchAssemblies;
        public IEnumerable<Type> Types;
        public MagicOnionOptions MagicOnionOptions;
        public IEnumerable<ChannelOption> ChannelOptions;
        public IEnumerable<ServerPort> Ports;

    }

    sealed class MagicOnionServerService : IHostedService
    {
        MagicOnionServiceDefinition _ServiceDefinition;
        IEnumerable<ServerPort> _Ports;
        IEnumerable<ChannelOption> _ChannelOptions;
        public MagicOnionServerService(MagicOnionServerServiceOptions options)
        {
            if (options.SearchAssemblies != null)
            {
                _ServiceDefinition = MagicOnionEngine.BuildServerServiceDefinition(options.SearchAssemblies, options.MagicOnionOptions);
            }
            else if (options.Types != null)
            {
                _ServiceDefinition = MagicOnionEngine.BuildServerServiceDefinition(options.Types, options.MagicOnionOptions);
            }
            else
            {
                if (options.MagicOnionOptions != null)
                {
                    _ServiceDefinition = MagicOnionEngine.BuildServerServiceDefinition(options.MagicOnionOptions);
                }
                else
                {
                    _ServiceDefinition = MagicOnionEngine.BuildServerServiceDefinition();
                }
            }
            _Ports = options.Ports;
            _ChannelOptions = options.ChannelOptions;
        }
        global::Grpc.Core.Server _Server;
        // object ServerLockObject = new object();
        // SemaphoreSlim _ServerLockObject = new SemaphoreSlim(1, 1);
        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartTask(cancellationToken);
            return Task.CompletedTask;
        }

        void StartTask(CancellationToken token)
        {
            if (_Server != null)
            {
                // already running
                return;
            }
            var newServer = new global::Grpc.Core.Server(_ChannelOptions)
            {
                Services = { _ServiceDefinition },
            };
            // if another server is set in another thread, just leave it.
            if (null == Interlocked.CompareExchange(ref _Server, newServer, null))
            {
                foreach (var port in _Ports)
                {
                    _Server.Ports.Add(port);
                }
                _Server.Start();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            do
            {
                var tmp = _Server;
                if (tmp == Interlocked.CompareExchange(ref _Server, null, tmp))
                {
                    if (tmp != null)
                    {
                        await tmp.ShutdownAsync().ConfigureAwait(false);
                    }
                }
            } while (_Server != null);
        }
    }
    public static class MagicOnionServerServiceExtension
    {
        public static IHostBuilder UseMagicOnion(this IHostBuilder hostBuilder,
            IEnumerable<ServerPort> ports,
            IEnumerable<Type> types = null,
            Assembly[] searchAssemblies = null,
            MagicOnionOptions options = null,
            IEnumerable<ChannelOption> channelOptions = null)
        {
            return hostBuilder.ConfigureServices((ctx, services) =>
            {
                services.AddTransient<IHostedService, MagicOnionServerService>(serviceProvider =>
                {
                    return new MagicOnionServerService(
                        new MagicOnionServerServiceOptions()
                        {
                            ChannelOptions = channelOptions,
                            MagicOnionOptions = options,
                            Ports = ports,
                            SearchAssemblies = searchAssemblies,
                            Types = types
                        }
                    );
                });
            });
        }
    }

}