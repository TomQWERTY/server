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
        public int x { get; set; }
        public int y { get; set; }

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

        public Point() { }

        public override bool Equals(Object obj)
        {
            Point point = (Point)obj;
            return (point.x == this.x && point.y == this.y);
        }

        public override int GetHashCode()
        {
            return x ^ y;
        }

        //public bool InField => y >= 0 && y < 11 && x >= 0 && x < 15;
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

        public override int GetHashCode()
        {
            return x ^ y;
        }

        public bool InField => y >= 0 && y < 11 && x + (y + 1) / 2 >= 0 && x + (y + 1) / 2 < 15;
    }

    public class Objects
    {
        public string imageLink { get; set; }
        public int speed { get; set; }
        public bool doubled { get; set; }
        public bool flying { get; set; }
        public List<Point> canMove { get; set; }
        public int playerId { get; set; }
        public bool waiting { get; set; }

        public Objects(string imageLink_, int speed_, bool doubled_, bool flying_, int playerId_, bool waiting_)
        {
            imageLink = imageLink_;
            speed = speed_;
            doubled = doubled_;
            flying = flying_;
            canMove = new List<Point>();
            playerId = playerId_;
            waiting = waiting_;
        }
    }

    public class Field
    {
        public Objects[][] Base { get; set; }

        public Field()
        {
            Base = new Objects[15][];
            for (int i = 0; i < 15; i++)
            {
                Base[i] = new Objects[11];
            }
        }

        public Objects this[int x, int y, int z]
        {
            get => Base[x + (y + 1) / 2][y];
            set => Base[x + (y + 1) / 2][y] = value;
        }

        public Objects this[Hex h]
        {
            get => this[h.x, h.y, h.z];
            set => this[h.x, h.y, h.z] = value;
        }

        public Objects this[int x, int y]
        {
            get => Base[x][y];
            set => Base[x][y] = value;
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
                        Search(this[j, i], new List<Hex>() { new Hex(j, i) }, this[j, i].speed);
                    }
        }

        public void Search(Objects obj, List<Hex> Hexes, int speed)
        {
            List<Hex> newHexes = new List<Hex>();
            foreach (Hex h in Hexes)
            {
                if (this[h] == null && !obj.canMove.Contains(new Point(h)))
                    obj.canMove.Add(new Point(h));
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

    public class Player
    {
        public WebSocket connection { get; set; }
        public List<Objects> squad { get; set; }
        public int id { get; set; }
        public Player(WebSocket connection_, List<Objects> squad_)
        {
            connection = connection_;
            squad = squad_;
        }
    }

    public class Connector
    {
        public string conType { get; set; }

        public Connector() { }
    }

    public class ConnectorMove : Connector
    {
        public Point point { get; set; }

        public ConnectorMove(Point p)
        {
            conType = "Move";
            point = p;
        }

        public ConnectorMove() { }
    }

    public class ConnectorInitialState : Connector
    {
        public Objects[][] Base { get; set; }
        public int turn { get; set; }

        public ConnectorInitialState(Objects[][] Base_, int turn_)
        {
            conType = "InitialState";
            Base = Base_;
            turn = turn_;
        }
    }

    public class ConnectorTurn : Connector
    {
        public Point activeSquad { get; set; }
        public bool isHoding { get; set; }

        public ConnectorTurn(Point activeSquad_, bool isHoding_)
        {
            conType = "Turn";
            activeSquad = activeSquad_;
            isHoding = isHoding_;
        }
    }

    public class ConnectorWait : Connector
    {
        public ConnectorWait()
        {
            conType = "Wait";
        }
    }

    public class ConnectorHold : Connector
    {
        public ConnectorHold()
        {
            conType = "Hold";
        }
    }

    public class Startup
    {
        WebSocketReceiveResult result = new WebSocketReceiveResult(1024 * 4, WebSocketMessageType.Text, true);
        List<Player> players = new List<Player>();
        int turn = -1;
        bool kostil = true;
        bool free = true;
        int cn = 0;
        List<Objects> queue = new List<Objects>();
        Field field;
        List<Point> hoding = new List<Point>();
        List<Point> waiting = new List<Point>();

        public void StartRound()
        {
            for (int i = 0; i < 15; i++)
            {
                for (int j = 0; j < 11; j++)
                {
                    if (field[i, j] != null)
                    {
                        hoding.Add(new Point(i, j));
                    }
                }
            }
        }

        public void SortHoding()
        {
            for (int i = 0; i < hoding.Count - 1;)
            {
                int maxSpeed = 0;
                for (int j = i; j < hoding.Count; j++)
                    if (field[hoding[j].x, hoding[j].y].speed > maxSpeed)
                        maxSpeed = field[hoding[j].x, hoding[j].y].speed;
                for (int j = i; j < hoding.Count; j++)
                    if (field[hoding[j].x, hoding[j].y].speed == maxSpeed)
                    {
                        Point swap = hoding[i];
                        hoding[i] = hoding[j];
                        hoding[j] = swap;
                        i++;
                    }
            }
            Console.WriteLine("sorting loh");
        }

        public void SortWaiting()
        {
            for (int i = 0; i < waiting.Count;)
            {
                int minSpeed = field[waiting[i]].speed;
                for (int j = i; j < waiting.Count; j++)
                {
                    if (field[waiting[j]].speed < minSpeed)
                        minSpeed = field[waiting[j]].speed;
                }
                for (int j = i; j < waiting.Count; j++)
                {
                    if (field[waiting[j]].speed == minSpeed)
                    {
                        Point swap = waiting[i];
                        waiting[i] = waiting[j];
                        waiting[j] = swap;
                        i++;
                    }
                }
            }
        }

        public void TestSort()
        {
            Console.WriteLine("-=LOHHHH=-");
            foreach (Point p in hoding)
            {
                Console.WriteLine(p.x + ";\t" + p.y + "\t" + field[p.x, p.y].speed);
            }
        }
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

                            if (players.Count == 0)
                            {
                                Console.WriteLine("players count = " + players.Count);
                                players.Add(new Player(await context.WebSockets.AcceptWebSocketAsync(),
                                new List<Objects>()
                                {
                                    new Objects("https://i.ibb.co/j8qr53X/pess.png", 3, false, false, players.Count, false),
                                    new Objects("https://i.ibb.co/smx7dvv/Archer.png", 5, false, false, players.Count, false),
                                    new Objects("https://i.ibb.co/zV0VBTQ/Champion.png", 11, false, false, players.Count, false),
                                    new Objects("https://i.ibb.co/4ZJBdXN/Halberdier.png", 4, false, false, players.Count, false),
                                    new Objects("https://i.ibb.co/263MvmM/Angel.png", 15, false, false, players.Count, false),
                                    new Objects("https://i.ibb.co/s5CCHZj/Royal-Griffin.png", 12, false, false, players.Count, false),
                                }));
                            }
                            else if (players.Count == 1)
                            {
                                Console.WriteLine("players count = " + players.Count);
                                players.Add(new Player(await context.WebSockets.AcceptWebSocketAsync(),
                                new List<Objects>()
                                {
                                    new Objects("https://i.ibb.co/sQFZ3PV/Royal-Griffin-Left.png", 12, false, false, players.Count, false),
                                    new Objects("https://i.ibb.co/kG9r7vm/Angel-Left.png", 15, false, false, players.Count, false),
                                    new Objects("https://i.ibb.co/SsL9fLx/Champion-Left.png", 11, false, false, players.Count, false),
                                    new Objects("https://i.ibb.co/n714GqW/pessLeft.png", 3, false, false, players.Count, false),
                                    new Objects("https://i.ibb.co/5cHr7yp/Halberdier-Left.png", 4, false, false, players.Count, false),
                                    new Objects("https://i.ibb.co/FKRm0Qb/Archer-Left.png", 5, false, false, players.Count, false),
                                }));
                            }
                            if (players.Count == 2)
                            {
                                field = new Field();
                                int y = 0;
                                foreach (Objects obj in players[0].squad)
                                {
                                    field[0, y] = obj;
                                    y += 2;
                                }
                                y = 0;
                                foreach (Objects obj in players[1].squad)
                                {
                                    field[14, y] = obj;
                                    y += 2;
                                }
                                Console.WriteLine("loh after cycle");
                                StartRound();
                                SortHoding();
                                TestSort();
                                turn = field[hoding[0]].playerId;
                                field.CanMove();
                                for (int i = 0; i < players.Count; i++)
                                {
                                    await players[i].connection.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(
                                        new ConnectorInitialState(field.Base, i), typeof(ConnectorInitialState))),
                                        result.MessageType, result.EndOfMessage, CancellationToken.None);
                                    await players[i].connection.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(
                                        new ConnectorTurn(hoding[0], i == turn), typeof(ConnectorTurn))),
                                        result.MessageType, result.EndOfMessage, CancellationToken.None);
                                    Console.WriteLine("sent initial " + i);
                                }
                            }
                            await Game(players.Last(), players.Count - 1);//strashno
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
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
        private async Task Game(Player player, int n)
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


            //List<Objects> fpa

            var buffer = new byte[1024 * 4];
            /*await player.connection.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(
                        n, typeof(int))),
                        result.MessageType, result.EndOfMessage, CancellationToken.None);*/




            while (true)
            {
                if (turn == n && free)
                {
                    free = false;
                    /*Console.WriteLine("turn = " + turn);
                    
                    cn++;

                    Console.WriteLine("cn " + cn);

                    Console.WriteLine("turn was " + turn);

                    Console.WriteLine("turn now " + turn);
                    Console.WriteLine("game " + n);*/
                    for (int i = 0; i < players.Count; i++)
                    {
                        Point hod;
                        if (hoding.Count > 0)
                            hod = hoding[0];
                        else
                            hod = waiting[0];

                        await players[i].connection.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(
                            new ConnectorTurn(hod, i == turn), typeof(ConnectorTurn))),
                            result.MessageType, result.EndOfMessage, CancellationToken.None);
                        await players[i].connection.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(
                            new ConnectorInitialState(field.Base, i), typeof(ConnectorInitialState))),
                            result.MessageType, result.EndOfMessage, CancellationToken.None);
                    }
                    Console.WriteLine("ready to get");
                    result = await player.connection.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    Console.WriteLine("got " + n);
                    if (result.CloseStatus.HasValue)
                    {
                        Console.WriteLine("break");
                        break;
                    }

                    //Console.WriteLine("endpoint = " + context.WebSockets.);

                    //result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);




                    Console.WriteLine("deserizl");
                    //List<Connector> conns = new List<Connector>();
                    Console.WriteLine(((Connector)JsonSerializer.Deserialize(
                        new ReadOnlySpan<byte>(buffer, 0, result.Count), typeof(Connector))).conType);
                    switch (((Connector)JsonSerializer.Deserialize(
                        new ReadOnlySpan<byte>(buffer, 0, result.Count), typeof(Connector))).conType)
                    {
                        case "Move":
                            /*conns.Add(new ConnectorMove((Point)JsonSerializer.Deserialize(
                            new ReadOnlySpan<byte>(buffer, 0, result.Count), typeof(Point))));*/
                            Point hod;
                            if (hoding.Count > 0)
                                hod = hoding[0];
                            else
                                hod = waiting[0];

                            p = ((ConnectorMove)JsonSerializer.Deserialize(
                                new ReadOnlySpan<byte>(buffer, 0, result.Count), typeof(ConnectorMove))).point;
                            field[p.x, p.y] = field[hod.x, hod.y];
                            field[hod.x, hod.y] = null;

                            if (hoding.Count > 0)
                                hoding.RemoveAt(0);
                            else
                                waiting.RemoveAt(0);
                            SortHoding();
                            field.CanMove();
                            TestSort();
                            Console.WriteLine("in move");

                            for (int i = 0; i < players.Count; i++)
                            {
                                /*foreach (Connector conn in conns)
                                {
                                    await players[i].connection.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(
                                                        conn, conn.GetType())),
                                                        result.MessageType, result.EndOfMessage, CancellationToken.None);
                                }*/
                                await players[i].connection.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(
                                 new ConnectorMove(p), typeof(ConnectorMove))),
                                 result.MessageType, result.EndOfMessage, CancellationToken.None);
                            }
                            break;
                        case "Wait":
                            Console.WriteLine("in wait");
                            if (hoding.Count > 0)
                            {
                                waiting.Add(hoding[0]);
                                hoding.RemoveAt(0);
                                SortWaiting();
                            }
                            break;
                        case "Hold":
                            Console.WriteLine("in hold");
                            if (hoding.Count > 0)
                            {
                                hoding.RemoveAt(0);
                            }
                            else
                            {
                                waiting.RemoveAt(0);
                            }
                            break;
                    }
                    if (hoding.Count > 0) turn = field[hoding[0]].playerId;
                    else if (waiting.Count > 0) turn = field[waiting[0]].playerId;
                    else
                    {
                        StartRound();
                        SortHoding();
                        turn = field[hoding[0]].playerId;
                    }

                    //Console.WriteLine(p.x + "; " + p.y);
                    //hexArray[p.x, p.y] = new Objects(2, "https://i.ibb.co/j8qr53X/pess.png", true);
                    /*await webSocket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(
                        new Connector[1]{new Connector(hexArray[0, 0, 0], 0, 0, 0)}, typeof(Connector[]))),
                        result.MessageType, result.EndOfMessage, CancellationToken.None);
                        Console.WriteLine("sent");*/


                    Console.WriteLine("sent");
                    free = true;
                }
            }
            Console.WriteLine("closed");
            await player.connection.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
        #endregion
    }
}