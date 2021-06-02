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

        public Point(int x_, int y_)
        {
            x = x_;
            y = y_;
        }

        public Point(Hex h)
        {
            x = h.x + (h.y + 1) / 2;
            y = h.y;
        }

        public Point() {}        

        public bool InField => y >= 0 && y < 11 && x >= 0 && x < 15;
    }

    public class Hex
    {
        public int x;
        public int y;
        public int z;

        public Hex(int x_, int y_, int z_)
        {
            x = x_;
            y = y_;
            z = z_;
        }

        public Hex(int x_, int y_)
        {
            x = x_ - (y_ + 1) / 2;
            y = y_;
            z = -(x + y);
        }

        public Hex(Point p)
        {
            x = p.x - (p.y + 1) / 2;
            y = p.y;
            z = -(x + y);
        }

        public override bool Equals(Object obj)
        {
            Hex hex = (Hex)obj;
            return (hex.x == this.x && hex.y == this.y && hex.z == this.z);
        }

        public bool InField => y >= 0 && y < 11 && x + (y + 1) / 2 >= 0 && x + (y + 1) / 2 < 15;
    }

    public class Objects
    {
        public string imageLink;
        public int speed;
        public bool doubled;
        public bool flying;
        public List<Hex> canMove = new List<Hex>();

        public Objects(string imageLink_, int speed_, bool doubled_, bool flying_)
        {
            imageLink = imageLink_;
            speed = speed_;
            doubled = doubled_;
            flying = flying_;
        }
    }

    public class Field
    {
        Objects[,] Base = new Objects[15, 11];

        public Objects this[int x, int y, int z]
        {
            get => Base[x + (y + 1) / 2, y];
            set => Base[x + (y + 1) / 2, y] = value;
        }

        public Objects this[Hex h]
        {
            get => this[h.x, h.y, h.z];
            set => this[h.x, h.y, h.z] = value;
        }

        public Objects this[int x, int y]
        {
            get => Base[x, y];
            set => Base[x, y] = value;
        }

        public Objects this[Point p]
        {
            get => this[p.x, p.y];
            set => this[p.x, p.y] = value;
        }

        public void CanMove()
        {
            for (int i = 0; i < 11; i++)
                for (int j = 0; j < 15; j++)
                    if (this[j, i] != null)
                    {
                        this[j, i].canMove.Clear();
                        List<Hex> hexes = new List<Hex>() { new Hex(j, i) };
                        if (this[j, i].doubled)
                            hexes.Add(new Hex(j - 1, i));
                        Search(this[j, i], hexes, this[j, i].speed);
                        if (this[j, i].doubled)
                            Check(this[j, i]);
                    }
        }

        public void Check(Objects obj)
        {
            for (int i = 0; i < obj.canMove.Count;)
                if (!obj.canMove.Contains(new Hex(new Point(obj.canMove[i]).x + 1, new Point(obj.canMove[i]).y)) && !obj.canMove.Contains(new Hex(new Point(obj.canMove[i]).x - 1, new Point(obj.canMove[i]).y)))
                    obj.canMove.Remove(obj.canMove[i]);
                else
                    i++;
        }

        public void Search(Objects obj, List<Hex> Hexes, int speed)
        {
            List<Hex> newHexes = new List<Hex>();
            foreach (Hex h in Hexes)
            {
                if (this[h] == null)
                    obj.canMove.Add(h);
                if (speed > 0)
                {
                    Hex[] hexes = new Hex[]
                    {
                        new Hex(h.x + 1, h.y - 1, h.z),
                        new Hex(h.x + 1, h.y, h.z - 1),
                        new Hex(h.x - 1, h.y + 1, h.z),
                        new Hex(h.x, h.y + 1, h.z - 1),
                        new Hex(h.x - 1, h.y, h.z + 1),
                        new Hex(h.x, h.y - 1, h.z + 1)
                    };

                    foreach (Hex h2 in hexes)
                        if (h2.InField && (this[h2] == null || obj.flying) && !Hexes.Contains(h2) && !newHexes.Contains(h2))
                            newHexes.Add(h2);
                }
            }
            if (newHexes.Count > 0)
                Search(obj, newHexes, speed - 1);
        }
    }

    public class Startup
    {
        List<WebSocket> players = new List<WebSocket>();
        int turn = 0;
        bool kostil = true;
        bool free = true;
        int cn = 0;
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
                //ReceiveBufferSize = 4 * 1024
            };
            app.UseWebSockets(webSocketOptions);
            #endregion
#endif
            #region AcceptWebSocket
            app.Use(async (context, next) =>
            {
                // /game/2930nfin89fmdskf340fmsmkdbgn934fdmkf/socket
                // 2930nfin89fmdskf340fmsmkdbgn934fdmkf => [s1, s2, s3]
                // ,dsmfkdsnfkjbdsfdsklf => [s4, s5, s6]
                if (context.Request.Path == "/ws")
                {
                    if (context.WebSockets.IsWebSocketRequest && players.Count < 2)
                    {
                        kostil = !kostil;
                        if (kostil)
                        {
                            players.Add(await context.WebSockets.AcceptWebSocketAsync());
                            await Game(context, players.Last(), players.Count - 1);
                        }
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

#region Main
        private async Task Game(HttpContext context, WebSocket webSocket, int n)
        {
            Point p;
            //HexArray hexArray = new HexArray();
            //hexArray[0, 0, 0] = new Objects(3, "https://i.ibb.co/rGJc40v/Zealot.png", false);
            
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
            WebSocketReceiveResult result = new WebSocketReceiveResult(1024 * 4, WebSocketMessageType.Text, true);
            await players[n].SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(
                        n, typeof(int))),
                        result.MessageType, result.EndOfMessage, CancellationToken.None);         
            
            while (true)
            {
                if (turn == n && free)
                {
                    free = false;
                    cn++;

                    Console.WriteLine("cn " + cn);
                    
                    Console.WriteLine("turn was " + turn);
                    turn = turn == 0 ? 1 : 0;
                    Console.WriteLine("turn now " + turn);
                    Console.WriteLine("game " + n);
                    
                    result = await players[n].ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    Console.WriteLine(result.MessageType);
                    //Console.WriteLine("endpoint = " + context.WebSockets.);
                    
                    //result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    
                    Console.WriteLine("got " + n);
                    if (result.CloseStatus.HasValue) 
                    {
                        Console.WriteLine("break");
                        break;
                    }
                    p = (Point)JsonSerializer.Deserialize(new ReadOnlySpan<byte>(buffer, 0, result.Count), typeof(Point));
                    //p.x++;
                    Console.WriteLine(p.x + "; " + p.y);
                    //hexArray[p.x, p.y] = new Objects(2, "https://i.ibb.co/j8qr53X/pess.png", true);
                    /*await webSocket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(
                        new Connector[1]{new Connector(hexArray[0, 0, 0], 0, 0, 0)}, typeof(Connector[]))),
                        result.MessageType, result.EndOfMessage, CancellationToken.None);
                        Console.WriteLine("sent");*/
                    foreach (WebSocket player in players)
                    {
                        await player.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(
                                                p, typeof(Point))),
                                                result.MessageType, result.EndOfMessage, CancellationToken.None);
                    }
                    Console.WriteLine("sent");
                    free = true;
                }
            }
            Console.WriteLine("closed");
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
#endregion
    }
}