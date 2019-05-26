#define UseOptions // or NoOptions or UseOptionsAO
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Debug;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace EchoApp
{
  public class Startup
  {
    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices(IServiceCollection services)
    {
      services.AddLogging(builder =>
      {
        builder.AddConsole()
                  .AddDebug()
                  .AddFilter<ConsoleLoggerProvider>(category: null, level: LogLevel.Debug)
                  .AddFilter<DebugLoggerProvider>(category: null, level: LogLevel.Debug);
      });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
    {
      if (env.IsDevelopment())
      {
        app.UseDeveloperExceptionPage();
      }

#if NoOptions
      #region UseWebSockets
            app.UseWebSockets();
      #endregion
#endif

#if UseOptions
      #region UseWebSocketsOptions
      var webSocketOptions = new WebSocketOptions()
      {
        KeepAliveInterval = TimeSpan.FromSeconds(120),
        ReceiveBufferSize = 4 * 1024
      };

      app.UseWebSockets(webSocketOptions);
      #endregion
#endif

#if UseOptionsAO
      #region UseWebSocketsOptionsAO
            var webSocketOptions = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 4 * 1024
            };
            webSocketOptions.AllowedOrigins.Add("https://client.com");
            webSocketOptions.AllowedOrigins.Add("https://www.client.com");

            app.UseWebSockets(webSocketOptions);
      #endregion
#endif

      #region AcceptWebSocket
      app.Use(async (context, next) =>
      {
        if (context.Request.Path == "/ws")
        {
          if (context.WebSockets.IsWebSocketRequest)
          {
            WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await Echo(context, webSocket);
          }
          else
          {
            context.Response.StatusCode = 400;
          }
        }
        else
        {
          await next();
        }

      });
      #endregion
      app.UseFileServer();
    }

    #region Echo
    private async Task Echo(HttpContext context, WebSocket _webSocket)
    {
      const string id = "3c9aecd2268f";

      var client = new DockerClientConfiguration(new Uri("http://233.233.233.233:2375")).CreateClient();

      var execCreateResp = await client.Containers.ExecCreateContainerAsync(
          id,
          new Docker.DotNet.Models.ContainerExecCreateParameters()
          {
            AttachStderr = true,
            AttachStdin = true,
            AttachStdout = true,
            Cmd = new string[] {"/bin/sh",
            "-c",
            "TERM=xterm-256color; export TERM; [ -x /bin/bash ] && ([ -x /usr/bin/script ] && /usr/bin/script -q -c \"/bin/bash\" /dev/null || exec /bin/bash) || exec /bin/sh"},
            Detach = false,
            Tty = true,
            Privileged = true
          });


      using (var stream = await client.Containers.StartAndAttachContainerExecAsync(execCreateResp.ID, false, default(CancellationToken)))
      {
        // Get Info of Exec Instance
        var execInspectResp = await client.Containers.InspectContainerExecAsync(execCreateResp.ID, default(CancellationToken));
        var pid = execInspectResp.Pid;


        // Read from Docker to WS
        var tRead = Task.Run(async () =>
        {
          var dockerBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);
          try
          {
            while (true)
            {
              // Clear buffer
              Array.Clear(dockerBuffer, 0, dockerBuffer.Length);
              var dockerReadResult = await stream.ReadOutputAsync(dockerBuffer, 0, dockerBuffer.Length, default(CancellationToken));

              if (dockerReadResult.EOF)
                break;


              if (dockerReadResult.Count > 0)
              {
                bool endOfMessage = true;
                await _webSocket.SendAsync(new ArraySegment<byte>(dockerBuffer, 0, dockerReadResult.Count), WebSocketMessageType.Text, endOfMessage, CancellationToken.None);
              }
              else
                break;
            }
          }
          catch (Exception ex)
          {
            // _logger.LogError(ex, "Failure during Read from Docker Exec to WebSocket");
          }
          System.Buffers.ArrayPool<byte>.Shared.Return(dockerBuffer);
        });


        // Write WS to Docker                             
        var tWrite = Task.Run(async () =>
        {
          WebSocketReceiveResult wsReadResult = null;
          // Read only small amount of chars at once (performance)!
          var wsBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(10);
          try
          {
            while (true)
            {
              // Clear buffer
              Array.Clear(wsBuffer, 0, wsBuffer.Length);
              wsReadResult = await _webSocket.ReceiveAsync(new ArraySegment<byte>(wsBuffer), CancellationToken.None);
              await stream.WriteAsync(wsBuffer, 0, wsBuffer.Length, default(CancellationToken));
              if (wsReadResult.CloseStatus.HasValue)
              {
                // _logger.LogInformation($"Stop Container Console (env-id: {environment.Id}, pid: {pid}");
                var killSequence = Encoding.ASCII.GetBytes($"exit{Environment.NewLine}");
                await stream.WriteAsync(killSequence, 0, killSequence.Length,
                default(CancellationToken));
                break;
              }
            }
          }
          catch (Exception ex)
          {
            Console.WriteLine(ex);
            // _logger.LogError(ex, "Failure during Write to Docker Exec from WebSocket");
          }
          System.Buffers.ArrayPool<byte>.Shared.Return(wsBuffer);
        });

        await tRead;
        await tWrite;
      }

    }
    #endregion
  }
}
