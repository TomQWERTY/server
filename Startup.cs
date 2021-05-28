#define UseOptions // or NoOptions
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EchoApp
{
    public class Point
    {
        public int x {get; set;}
        public int y {get; set;}

        /*public Point(int x_, int y_)
        {
            x = x_;
            y = y_;
        }*/
    }

    public class Objects
    {
        public int speed {get; set;}
        public string img {get; set;}
        public bool doubleCell {get; set;}
        public List<Point> canMove {get; set;}

        /*public Objects(int x, int y, int speed_, string image, bool double_)
        {
            speed = speed_;
            img = image;
            doubleCell = double_;
            canMove = new List<Point>();
        }*/

    }

    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            //loggerFactory.AddConsole(LogLevel.Debug);
            //loggerFactory.AddDebug(LogLevel.Debug);

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
                        context.Response.StatusCode = (int) HttpStatusCode.BadRequest;
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
        private async Task Echo(HttpContext context, WebSocket webSocket)
        {
            Point p;
            Objects[,] objects = new Objects[15, 11];
            objects[10, 10] = new Objects(/*3, 4, 3, "https://i.ibb.co/rGJc40v/Zealot.png", false*/);
            objects[10, 10].speed = 3;
            objects[10, 10].img = "https://i.ibb.co/rGJc40v/Zealot.png";
            objects[10, 10].doubleCell = false;
            objects[10, 10].canMove = new List<Point>();
            /*objects[4, 9] = new Objects(4, 9, 5, "https://i.ibb.co/263MvmM/Angel.png", false);
            objects[11, 7] = new Objects(11, 7, 3, "https://i.ibb.co/s5CCHZj/Royal-Griffin.png", true);*/
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result;
            while (true)
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                Console.WriteLine("got");
                if (result.CloseStatus.HasValue) 
                {
                    Console.WriteLine("break");
                    break;
                }
                /*for (int i = 0; i < result.Count; i++)
                {
                    Console.Write(Convert.ToChar(buffer[i]));
                }*/
                p = (Point)JsonSerializer.Deserialize(new ReadOnlySpan<byte>(buffer, 0, result.Count), typeof(Point));
                //p.x++;
                
                await webSocket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(p, typeof(Point))),
                    result.MessageType, result.EndOfMessage, CancellationToken.None);
                    Console.WriteLine("sent");
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
#endregion
    }
}