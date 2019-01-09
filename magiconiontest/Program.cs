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
using MagicOnion.Extensions.Hosting;

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
    class Program
    {
        static async Task Main(string[] args)
        {
            using (var host = new HostBuilder()
                .UseMagicOnion(
                    new ServerPort[] { new ServerPort("localhost", 10012, ServerCredentials.Insecure) },
                    searchAssemblies: new Assembly[] { Assembly.GetEntryAssembly() },
                    options: new MagicOnionOptions())
                .Build())
            {
                host.Start();
                // client task
                var channel = new Channel("localhost", 10012, ChannelCredentials.Insecure);
                var client = MagicOnionClient.Create<IMyService>(channel);
                for (int i = 0; i < 10; i++)
                {
                    var ret = await client.SumAsync(i, i * 2);
                    Console.WriteLine($"{i}, ret = {ret}");
                }
                // shutdown service when client task has done
                Console.WriteLine($"shutdown task");
                await host.StopAsync();
                // test for safe if multiple stop call.
                await host.StopAsync();
                Console.WriteLine($"all task done");
            }
        }
    }
}
