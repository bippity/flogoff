﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Terraria;
using TerrariaApi;
using TShockAPI;
using TerrariaApi.Server;
using System.Text;
using System.Linq;

namespace Flogoff
{
    [ApiVersion(1, 15)]

    public class FLogoff : TerrariaPlugin
    {
        protected List<string> offline = new List<string>();
        protected static bool[] offlineindex = new bool[256];

        public override Version Version
        {
            get { return new Version("1.4"); }
        }

        public override string Name
        {
            get { return "FLogoff"; }
        }

        public override string Author
        {
            get { return "Darkvengance aka Sildaekar"; }
        }

        public override string Description
        {
            get { return "Performs a fake logoff"; }
        }

        public FLogoff(Main game)
            : base(game)
        {
            Order = 1;
        }

        public override void Initialize()
        {
            ServerApi.Hooks.ServerChat.Register(this, OnChat);
            ServerApi.Hooks.NetSendData.Register(this, OnSendData);
            Commands.ChatCommands.Add(new Command("flogoff", flogon, "flogon"));
            Commands.ChatCommands.Add(new Command("flogoff", flogoff, "flogoff"));
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
                ServerApi.Hooks.NetSendData.Deregister(this, OnSendData);
            }
            base.Dispose(disposing);
        }

        public void OnSendData(SendDataEventArgs e)
        {
            try
            {
                List<int> list = new List<int>();
                for (int i = 0; i < 256; i++)
                {
                    if (FLogoff.offlineindex[i])
                    {
                        list.Add(i);
                    }
                }
                PacketTypes msgID = e.MsgId;
                if (msgID <= PacketTypes.DoorUse)
                {
                    if (msgID != PacketTypes.PlayerSpawn && msgID != PacketTypes.DoorUse)
                    {
                        goto IL_D2;
                    }
                }
                else
                {
                    switch (msgID)
                    {
                        case PacketTypes.PlayerDamage:
                            break;

                        case PacketTypes.ProjectileNew:
                        case PacketTypes.ProjectileDestroy:
                            if (list.Contains(e.ignoreClient) && FLogoff.offlineindex[e.ignoreClient])
                            {
                                e.Handled = true;
                                goto IL_D2;
                            }
                            goto IL_D2;

                        case PacketTypes.NpcStrike:
                            goto IL_D2;

                        default:
                            switch (msgID)
                            {
                                case PacketTypes.EffectHeal:
                                case PacketTypes.Zones:
                                    break;

                                default:
                                    switch (msgID)
                                    {
                                        case PacketTypes.PlayerAnimation:
                                        case PacketTypes.EffectMana:
                                        case PacketTypes.PlayerTeam:
                                            break;

                                        case PacketTypes.PlayerMana:
                                        case PacketTypes.PlayerKillMe:
                                            goto IL_D2;

                                        default:
                                            goto IL_D2;
                                    }
                                    break;
                            }
                            break;
                    }
                }
                if (list.Contains(e.number) && FLogoff.offlineindex[e.number])
                {
                    e.Handled = true;
                }
            IL_D2:
                if (e.number >= 0 && e.number <= 255 && FLogoff.offlineindex[e.number] && e.MsgId == PacketTypes.PlayerUpdate)
                {
                    e.Handled = true;
                }
            }
            catch (Exception)
            {
            }
        }

        protected void OnChat(ServerChatEventArgs args)
        {
            TSPlayer player = TShock.Players[args.Who];

            var argz = new CommandArgs(args.Text, player, ParseParameters(args.Text));
            argz.Parameters.RemoveAt(0);

            if (args.Text.StartsWith("/tp ", StringComparison.OrdinalIgnoreCase))
            {
                args.Handled = true;

                if (argz.Parameters.Count < 1)
                {
                    argz.Player.SendErrorMessage("Invalid syntax! Proper syntax: /tp <player>");
                    argz.Player.SendErrorMessage("                               /tp <x> <y>");
                    return;
                }

                if (argz.Parameters.Count == 2)
                {
                    float x, y;
                    if (float.TryParse(argz.Parameters[0], out x) && float.TryParse(argz.Parameters[1], out y))
                    {
                        argz.Player.Teleport(x, y);
                        argz.Player.SendSuccessMessage("Teleported!");
                    }
                }
                else
                {
                    string plStr = String.Join(" ", argz.Parameters);
                    var players = TShock.Utils.FindPlayer(plStr);

                    string result = offline.Find(delegate(string off) { return off.ToLower() == players[0].Name.ToLower(); });

                    if (players.Count == 0 || result!=null)
                    {
                        argz.Player.SendErrorMessage("Invalid user name.");
                        argz.Player.SendErrorMessage("Proper syntax: /tp <player>");
                        argz.Player.SendErrorMessage("               /tp <x> <y>");
                    }

                    else if (players.Count > 1)
                        TShock.Utils.SendMultipleMatchError(argz.Player, players.Select(p => p.Name));
                    else if (!players[0].TPAllow && !argz.Player.Group.HasPermission(Permissions.tpall))
                    {
                        var plr = players[0];
                        argz.Player.SendErrorMessage(plr.Name + " has prevented users from teleporting to them.");
                        plr.SendInfoMessage(argz.Player.Name + " attempted to teleport to you.");
                    }
                    else
                    {
                        var plr = players[0];
                        if (argz.Player.Teleport(plr.TileX * 16, plr.TileY * 16))
                        {
                            argz.Player.SendSuccessMessage(string.Format("Teleported to {0}.", plr.Name));
                            if (!argz.Player.Group.HasPermission(Permissions.tphide))
                                plr.SendInfoMessage(argz.Player.Name + " teleported to you.");
                        }
                    }
                }
            }
            else if (args.Text.StartsWith("/playing", StringComparison.OrdinalIgnoreCase) || args.Text.StartsWith("/who", StringComparison.OrdinalIgnoreCase))
            {
                args.Handled = true;

                bool invalidUsage = (argz.Parameters.Count > 2);

                bool displayIdsRequested = false;
                int pageNumber = 1;
                if (!invalidUsage)
                {
                    foreach (string parameter in argz.Parameters)
                    {
                        if (parameter.Equals("-i", StringComparison.InvariantCultureIgnoreCase))
                        {
                            displayIdsRequested = true;
                            continue;
                        }

                        if (!int.TryParse(parameter, out pageNumber))
                        {
                            invalidUsage = true;
                            break;
                        }
                    }
                }
                if (invalidUsage)
                {
                    player.SendErrorMessage("Invalid usage, proper usage: /who [-i] [pagenumber]");
                    return;
                }
                if (displayIdsRequested && !player.Group.HasPermission(Permissions.seeids))
                {
                    player.SendErrorMessage("You don't have the required permission to list player ids.");
                    return;
                }

                var listplayers = TShock.Utils.GetPlayers(displayIdsRequested);
                string[] players = TShock.Utils.GetPlayers(false).ToArray();

                int i = 0;
                foreach (string playername in players)
                {
                    string result = offline.Find(delegate(string off) { return off.ToLower() == playername.ToLower(); });
                    if (result != null)
                    {
                        listplayers.Remove(playername);
                    }
                    i++;
                }

                player.SendSuccessMessage("Online Players ({0}/{1})", listplayers.Count, TShock.Config.MaxSlots);

                PaginationTools.SendPage(
                    player, pageNumber, PaginationTools.BuildLinesFromTerms(listplayers),
                    new PaginationTools.Settings
                    {
                        IncludeHeader = false,
                        FooterFormat = string.Format("Type /who {0}{{0}} for more.", displayIdsRequested ? "-i " : string.Empty)
                    }
                );
            }
        }

        protected void flogoff(CommandArgs args)
        {
            TSPlayer player = args.Player;
            offline.Add(player.Name);
            offlineindex[player.Index] = true;

            player.mute = true; //Just for saftey ;)

            //Team Update
            int team = player.TPlayer.team;
            player.TPlayer.team = 0;
            NetMessage.SendData(45, -1, -1, "", player.Index, 0f, 0f, 0f, 0);
            player.TPlayer.team = team;

            //Player Update
            player.TPlayer.position.X = 0f;
            player.TPlayer.position.Y = 0f;
            NetMessage.SendData(13, -1, -1, "", player.Index, 0f, 0f, 0f, 0);

            TSPlayer.All.SendMessage(string.Format("{0} left", player.Name), Color.Yellow);
        }

        protected void flogon(CommandArgs args)
        {
            TSPlayer player = args.Player;
            player.mute = false;
            offline.Remove(player.Name);
            offlineindex[player.Index] = false;
            TSPlayer.All.SendMessage(string.Format("{0} has joined", player.Name), Color.Yellow);
        }

        private static List<String> ParseParameters(string str)
        {
            var ret = new List<string>();
            var sb = new StringBuilder();
            bool instr = false;
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];

                if (instr)
                {
                    if (c == '\\')
                    {
                        if (i + 1 >= str.Length)
                            break;
                        c = GetEscape(str[++i]);
                    }
                    else if (c == '"')
                    {
                        ret.Add(sb.ToString());
                        sb.Clear();
                        instr = false;
                        continue;
                    }
                    sb.Append(c);
                }
                else
                {
                    if (IsWhiteSpace(c))
                    {
                        if (sb.Length > 0)
                        {
                            ret.Add(sb.ToString());
                            sb.Clear();
                        }
                    }
                    else if (c == '"')
                    {
                        if (sb.Length > 0)
                        {
                            ret.Add(sb.ToString());
                            sb.Clear();
                        }
                        instr = true;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }
            if (sb.Length > 0)
                ret.Add(sb.ToString());

            return ret;
        }

        private static char GetEscape(char c)
        {
            switch (c)
            {
                case '\\':
                    return '\\';
                case '"':
                    return '"';
                case 't':
                    return '\t';
                default:
                    return c;
            }
        }

        private static bool IsWhiteSpace(char c)
        {
            return c == ' ' || c == '\t' || c == '\n';
        }

    }
}
