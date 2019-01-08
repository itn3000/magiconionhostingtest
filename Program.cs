using System;
using System.Reflection;
using System.Collections.Generic;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using MagicOnion.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;

namespace magiconiontest
{
    public interface IMyService : IService<IMyService>
    {
        UnaryResult<int> SumAsync(int x, int y);
    }
    public class MyServiceImpl : ServiceBase<IMyService>, IMyService
    {
        public async UnaryResult<int> SumAsync(int x, int y)
        {
            Logger.Debug($"recv: {x}, {y}");
            return x + y;
        }
    }

    sealed class MagicOnionServerOptions
    {
        public Assembly[] SearchAssemblies;
        public IEnumerable<Type> Types;
        public MagicOnionOptions MagicOnionOptions;
        public IEnumerable<ChannelOption> ChannelOptions;
        public IEnumerable<ServerPort> Ports;

    }

    sealed class MagicOnionServerTask : IHostedService
    {
        MagicOnionServiceDefinition _ServiceDefinition;
        IEnumerable<ServerPort> _Ports;
        IEnumerable<ChannelOption> _ChannelOptions;
        public MagicOnionServerTask(MagicOnionServerOptions options)
        {
            if (options.SearchAssemblies != null)
            {
                    _ServiceDefinition = MagicOnionEngine.BuildServerServiceDefinition(options.SearchAssemblies, options.MagicOnionOptions);
            }
            else if(options.Types != null)
            {
                _ServiceDefinition = MagicOnionEngine.BuildServerServiceDefinition(options.Types, options.MagicOnionOptions);
            }
            else
            {
                _ServiceDefinition = MagicOnionEngine.BuildServerServiceDefinition(options.MagicOnionOptions);
            }
            _Ports = options.Ports;
            _ChannelOptions = options.ChannelOptions;
        }
        global::Grpc.Core.Server _Server;
        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartTask(cancellationToken);
            return Task.CompletedTask;
        }

        void StartTask(CancellationToken token)
        {
            _Server = new global::Grpc.Core.Server(_ChannelOptions)
            {
                Services = { _ServiceDefinition },
            };
            foreach (var port in _Ports)
            {
                _Server.Ports.Add(port);
            }
            _Server.Start();
        }


        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _Server.ShutdownAsync();
        }
    }
    static class MagicOnionServerTaskExtension
    {
        public static IHostBuilder UseMagicOnion(this IHostBuilder hostBuilder, IEnumerable<ServerPort> ports, MagicOnionOptions options = null, IEnumerable<ChannelOption> channelOptions = null)
        {
            return hostBuilder.ConfigureServices((ctx, services) =>
            {
                services.AddHostedService<MagicOnionServerTask>();
            });
        }
    }
    class MagicOnionClientTask : IHostedService
    {
        Task _ServiceTask = null;
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ServiceTask = StartTask(cancellationToken);
            return Task.CompletedTask;
        }

        async Task StartTask(CancellationToken ct)
        {
            var channel = new Channel("localhost", 10012, ChannelCredentials.Insecure);
            var client = MagicOnionClient.Create<IMyService>(channel);
            for (int i = 0; i < 10 && !ct.IsCancellationRequested; i++)
            {
                var ret = await client.SumAsync(i, i * 2);
                Console.WriteLine($"{i}, ret = {ret}");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _ServiceTask;
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureServices((ctx, services) =>
                {
                    services.AddHostedService<MagicOnionClientTask>()
                        .AddHostedService<MagicOnionServerTask>()
                        ;
                })
                .Build();
            host.Run();
        }
    }
}
