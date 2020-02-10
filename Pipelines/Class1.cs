using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Pipelines
{
    public class PipelinesTest
    {
        public static IList<ChannelReader<T>> Split<T>(ChannelReader<T> ch, int n)
        {
            var outputs = new Channel<T>[n];
            //init each Channel[n]
            for (int i = 0; i < n; i++)
            {
                outputs[i] = Channel.CreateUnbounded<T>();
            }

            Task.Run(async () =>
            {
                var index = 0;
                await foreach (var item in ch.ReadAllAsync())
                {
                    await outputs[index].Writer.WriteAsync(item);
                    index = (index + 1) % n;
                }

                foreach (var ch in outputs)
                {
                    ch.Writer.Complete();
                }
            });
            return outputs.Select(ch => ch.Reader).ToArray();
        }
        public static ChannelReader<T> Merge<T>(params ChannelReader<T>[] inputs)
        {
            var output = Channel.CreateUnbounded<T>();
            Task.Run(async () =>
            {
                async Task Redirect(ChannelReader<T> input)
                {
                    await foreach (var item in input.ReadAllAsync())
                    {
                        await output.Writer.WriteAsync(item);
                    }
                }

                await Task.WhenAll(inputs.Select(i => Redirect(i)).ToArray());
                output.Writer.Complete();
            });
            return output;
        }
        public static ChannelReader<T> MergeLimited<T>(ChannelReader<T> first, ChannelReader<T> second)
        {
            var output = Channel.CreateUnbounded<T>();
            Task.Run(async () =>
            {
                await foreach (var item in first.ReadAllAsync())
                {
                    await output.Writer.WriteAsync(item);
                }
            });
            Task.Run(async () =>
            {
                await foreach (var item in second.ReadAllAsync())
                {
                    await output.Writer.WriteAsync(item);
                }
            });
            return output;
        }
        public static ChannelReader<string> CreateMessenger(string msg, int count, CancellationToken token = default)
        {
            var ch = Channel.CreateUnbounded<string>();
            var rnd = new Random();
            Task.Run(async () =>
            {
                for (int i = 0; i < count; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        await ch.Writer.WriteAsync($"Writer {msg} cancelled");
                        break;
                    }
                    await ch.Writer.WriteAsync($"{msg} {i}");
                    await Task.Delay(TimeSpan.FromSeconds(rnd.Next(0,3)));

                }

                ch.Writer.Complete();
            });
            return ch.Reader;
        }
        
        public async Task RunProducerConsumerChannel()
        {
            var ch = Channel.CreateBounded<string>(10);

            var consumer = Task.Run(async () =>
            {
                while (await ch.Reader.WaitToReadAsync())
                    Console.WriteLine(await ch.Reader.ReadAsync());
            });
            var producer = Task.Run(async () =>
            {
                var rnd = new Random();
                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(TimeSpan.FromSeconds(rnd.Next(3)));
                    await ch.Writer.WriteAsync($"Message {i}");
                }
                ch.Writer.Complete();
            });

            await Task.WhenAll(producer, consumer);
        }
        public async Task RunChannels()
        {
            Channel<string> ch = Channel.CreateUnbounded<string>();
            await ch.Writer.WriteAsync("First string message");
            await ch.Writer.WriteAsync("Second string message");
            ch.Writer.Complete();
            
            // Basic example on how to read from a channel 
            // while (await ch.Reader.WaitToReadAsync())
            // {
            //     Console.WriteLine(await ch.Reader.ReadAsync());
            // }
            
            //Async IEnumerable
            await foreach (var item in ch.Reader.ReadAllAsync())
            {
                Console.WriteLine(item);
            }
        }
        
    }
}
