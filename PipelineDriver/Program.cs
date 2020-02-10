using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Pipelines;

namespace PipelineDriver
{
    class Program
    {
        private static HashSet<string> allowedExtensions;
        static async Task Main(string[] args)
        {
            allowedExtensions = new HashSet<string>()
            {
                ".ts", ".js",".cs",
            };
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            var fileLineCounter = new FileLineCounter();
            var sw = new Stopwatch();
            sw.Start();
           var fileGen = fileLineCounter
               .GetFilesRecursively(@"C:\Users\JimmyMsk\source\repos\MATS\Monitor\"
                   , cts.Token
                   );
           var sourceCodeFiles = fileLineCounter
               .FilterByExtension(fileGen, allowedExtensions);
           var split = fileLineCounter.Split(sourceCodeFiles, 5);
           var counter = fileLineCounter
               .CountLinesAndMerge(split);
           var totalLines = 0;
           await foreach (var item in counter.ReadAllAsync().ConfigureAwait(false))
           {
             //  Console.WriteLine($"{item.file.FullName}: {item.lines}");
               totalLines += item.lines;
           }
           
           
           sw.Stop();
           Console.WriteLine($"{totalLines} counted in {sw.Elapsed}");
           // await foreach (var err in errors.ReadAllAsync())
           // {
           //     Console.WriteLine(err);
           // }
           // var msger = PipelinesTest.CreateMessenger("Joe", 5);
           // var msger2 = PipelinesTest.CreateMessenger("ann", 10);
           //
           // var chMergeAny = PipelinesTest.Merge(msger, msger2);
           // await foreach (var item in chMergeAny.ReadAllAsync())
           // {
           //     Console.WriteLine(item);
           // }
           // var chMerge = PipelinesTest.Merge(msger, msger2);
           // await foreach (var item in chMerge.ReadAllAsync())
           // {
           //     Console.WriteLine(item);   
           // }

           //conventional way will exception when msger.Count != msger.Count
           //because one will finish befor the other
           // while (await msger.WaitToReadAsync() || await msger2.WaitToReadAsync())
           // {
           //     Console.WriteLine(await msger.ReadAsync());
           //     Console.WriteLine(await msger2.ReadAsync());
           // }
           // await foreach (var item in msger.ReadAllAsync())
           // {
           //     Console.WriteLine(item);
           // }
           //await ch.RunProducerConsumerChannel();
        }

        public static async Task FileCountPipelineBasic()
        {
            allowedExtensions = new HashSet<string>()
            {
                ".ts", ".js",".cs",
            };
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            var fileLineCounter = new FileLineCounter();
           
            var fileGen = fileLineCounter
                .GetFilesRecursively(@"C:\Users\JimmyMsk\source\repos\MATS\Monitor\", cts.Token);
            var sourceCodeFiles = fileLineCounter
                .FilterByExtension(fileGen, allowedExtensions);
            var (counter, errors) = fileLineCounter
                .GetLineCount(sourceCodeFiles);
            var totalLines = 0;
            await foreach (var item in counter.ReadAllAsync().ConfigureAwait(false))
            {
                //  Console.WriteLine($"{item.file.FullName}: {item.lines}");
                totalLines += item.lines;
            }
           
            Console.WriteLine($"Total: {totalLines}");
        }
        public static async Task MsgWithCancellation()
        {
            var ch = new PipelinesTest();
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            var joe = PipelinesTest.CreateMessenger("joe", 10, cts.Token);

            await foreach (var item in joe.ReadAllAsync())
            {
                Console.WriteLine(item);
            }
        }

        public static async Task SplitAndMerge()
        {
            var ch = new PipelinesTest();
            var joe = PipelinesTest.CreateMessenger("joe", 10);
            var readers = PipelinesTest.Split(joe, 3);
            var tasks = new List<Task>();
            for (int i = 0; i < readers.Count; i++)
            {
                var reader = readers[i];
                var index = i;
                tasks.Add(Task.Run(async () =>
                {
                    await foreach (var item in reader.ReadAllAsync())
                    {
                        Console.WriteLine($"Reader {index}: {item}");
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }
    }
}