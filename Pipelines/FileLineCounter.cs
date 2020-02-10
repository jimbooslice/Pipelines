using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Pipelines
{
    public class FileLineCounter
    {
        public ChannelReader<string> GetFilesRecursively(string root, CancellationToken token = default)
        {
            var output = Channel.CreateUnbounded<string>();
            //anonymous local function
            async Task WalkDir(string path)
            {
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }
                foreach (var file in Directory.GetFiles(path))
                {
                    await output.Writer.WriteAsync(file, token).ConfigureAwait(false);
                }
                //projects result of GetDirectories() onto the local function
                //as a list of Tasks
                //same as .Select(directory => WalkDir(directory))
                //recursive, DFS
                var tasks = Directory.GetDirectories(path).Select(WalkDir);
                await Task.WhenAll(tasks.ToArray());
            }

            Task.Run(async () =>
            {
                try
                {
                    await WalkDir(root).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Cancelled");
                }
                finally
                {
                    output.Writer.Complete();
                }
            });
            return output;
        }

        public ChannelReader<FileInfo> FilterByExtension(ChannelReader<string> input, HashSet<string> exts)
        {
            var output = Channel.CreateUnbounded<FileInfo>();

            Task.Run(async () =>
            {
                await foreach (var file in input.ReadAllAsync())
                {
                    var fileInfo = new FileInfo(file);
                    if (exts.Contains(fileInfo.Extension))
                    {
                        await output.Writer.WriteAsync(fileInfo);
                    }
                    
                }
                output.Writer.Complete();
            });
            return output;
        }

        public (ChannelReader<(FileInfo file, int lines)>, ChannelReader<string> errors) GetLineCount(
            ChannelReader<FileInfo> input)
        {
            var output = Channel.CreateUnbounded<(FileInfo, int)>();
            var errors = Channel.CreateUnbounded<string>();
            Task.Run(async () =>
            {
                await foreach (var file in input.ReadAllAsync())
                {
                    //var lines = await CountLinesAsync(file).ConfigureAwait(false);
                    var lines = CountLines(file);
                    if (lines == 0)
                    {
                        await errors.Writer.WriteAsync($"[Error] Empty file {file}");
                    }
                    else
                    {
                        await output.Writer.WriteAsync((file, lines));
                    }
                    
                    
                }
                output.Writer.Complete();
                errors.Writer.Complete();
            });
            return (output,errors);
        }

        private async Task<int> CountLinesAsync(FileInfo file)
        {
            using var sr = new StreamReader(file.FullName);
            var content = string.Empty;
            var lines = 0;
            content = await sr.ReadToEndAsync();
            lines = content.Split('\n').Length;
            // while (await sr.ReadLineAsync().ConfigureAwait(false) != null)
            // {
            //     lines++;
            // }
            return lines;
        }
        private int CountLines(FileInfo file)
        {
            using var sr = new StreamReader(file.FullName);
            var lines = 0;
            var content= sr.ReadToEnd();
            lines = content.Split('\n').Length;
            // while ( sr.ReadLine() != null)
            // {
            //     lines++;
            // }
            return lines;
        }
        
        public  ChannelReader<(FileInfo file, int lines)> CountLinesAndMergeAsync(IList<ChannelReader<FileInfo>> inputs)
        {
            var output = Channel.CreateUnbounded<(FileInfo file, int lines)>();
            Task.Run(async () =>
            {
                async Task Redirect(ChannelReader<FileInfo> input)
                {
                    await foreach (var file in input.ReadAllAsync())
                    {
                        //await output.Writer.WriteAsync((file, fn(file)));
                        await output.Writer.WriteAsync(
                            (file, await CountLinesAsync(file))
                        );
                    }
                }

                await Task.WhenAll(inputs.Select(Redirect).ToArray());
                output.Writer.Complete();
            });
            return output;
        }
        public ChannelReader<(FileInfo file, int lines)> CountLinesAndMerge(IList<ChannelReader<FileInfo>> inputs)
        {
            var output = Channel.CreateUnbounded<(FileInfo file, int lines)>();
            Task.Run(async () =>
            {
                async Task Redirect(ChannelReader<FileInfo> input)
                {
                    await foreach (var file in input.ReadAllAsync())
                    {
                        //await output.Writer.WriteAsync((file, fn(file)));
                        await output.Writer.WriteAsync(
                            (file, CountLines(file))
                            );
                    }
                }

                await Task.WhenAll(inputs.Select(Redirect).ToArray());
                output.Writer.Complete();
            });
            return output;
        }
        public IList<ChannelReader<T>> Split<T>(ChannelReader<T> input, int n)
        {
            var outputs = new Channel<T>[n];
            
            for (var i = 0; i < n; i++)
            {
                outputs[i] = Channel.CreateUnbounded<T>();
            }

            Task.Run(async () =>
            {
                var index = 0;
                await foreach (var item in input.ReadAllAsync())
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

        ChannelReader<T> Merge<T>(params ChannelReader<T>[] inputs)
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

                await Task.WhenAll(inputs.Select(Redirect).ToArray());
                output.Writer.Complete();
            });
            return output;
        }
    }
}