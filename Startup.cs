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

    public class Connector
    {
        public Objects obj {get; set;}
        public int x {get; set;}
        public int y {get; set;}
        public int turn {get; set;}
        public Connector(Objects obj_, int x_, int y_, int z_, int turn_)
        {
            obj = obj_;
            y = x_;
            x = y - y_;
            turn = turn_;
        }

        public Connector(Objects obj_, int x_, int y_, int turn_)
        {
            obj = obj_;
            x = x_;
            y = y_;
            turn = turn_;
        }
    }
    
    public class Objects
    {
        public int speed {get; set;}
        public string img {get; set;}
        public bool doubleCell {get; set;}
        public List<Point> canMove {get; set;}

        public Objects(int speed_, string img_, bool doubleCell_)
        {
            speed = speed_;
            img = img_;
            doubleCell = doubleCell_;
            canMove = new List<Point>();
        }
    }

    public class HexArray
    {
        public Objects[][][] array {get; set;}

        public HexArray()
        {
            array = new Objects[30][][];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = new Objects[30][];  
                for (int j = 0; j < array[i].Length; j++)
                {
                    array[i][j] = new Objects[30];
                }
            }
        }

        public Objects this[int x, int y, int z]
        {
            get => array[x + 14][y + 14][z + 14];
            set => array[x + 14][y + 14][z + 14] = value; 
        }

        public Objects this[int x, int y]
        {
            get => this[y, x - y, -(x + y)];
            //set => array[y][x - y][-(x + y)] = value;
            set
            {
                this[y, x - y, -(x + y)] = value;
                Console.WriteLine((y) + "; " + (x - y) + "; " + (-(x + y)));
            }
        }
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
            
            HexArray hexArray = new HexArray();
            hexArray[0, 0, 0] = new Objects(3, "https://i.ibb.co/rGJc40v/Zealot.png", false);
            
            /*hexArray[0, 0, 0].speed = 3;
            hexArray[0, 0, 0].img = "https://i.ibb.co/rGJc40v/Zealot.png";
            hexArray[0, 0, 0].doubleCell = false;
            hexArray[0, 0, 0].canMove = new List<Point>();*/
            //Objects obj = new Objects();
            
            /*obj.speed = 3;
            obj.img = "https://i.ibb.co/rGJc40v/Zealot.png";
            obj.doubleCell = false;
            obj.canMove = new List<Point>();*/
            /*objects[4, 9] = new Objects(4, 9, 5, "https://i.ibb.co/263MvmM/Angel.png", false);
            objects[11, 7] = new Objects(11, 7, 3, "https://i.ibb.co/s5CCHZj/Royal-Griffin.png", true);*/
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result;

            int turn = 0;
            
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
                
                hexArray[p.x, p.y] = new Objects(2, "https://i.ibb.co/j8qr53X/pess.png", false);
                Console.WriteLine("fdghajuibdgfeabgidbasgdsbtgrou");
                /*await webSocket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(
                    new Connector[1]{new Connector(hexArray[0, 0, 0], 0, 0, 0)}, typeof(Connector[]))),
                    result.MessageType, result.EndOfMessage, CancellationToken.None);
                    Console.WriteLine("sent");*/
                    await webSocket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(
                    new Connector(hexArray[p.x, p.y], p.x, p.y, turn), typeof(Connector))),
                    result.MessageType, result.EndOfMessage, CancellationToken.None);
                    turn = turn == 0 ? 1 : 0;
                    Console.WriteLine("sent");
            }
            Console.WriteLine("closed");
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
#endregion
    }
}